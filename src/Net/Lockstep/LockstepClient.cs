// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Net.Lockstep;

/// <summary>
/// The client side of LAN lockstep (M9-03, design 20 §4.2 and §9). Steps every tick it holds a bundle for, paced at 20 Hz from the
/// caller's clock, catching up (at most <see cref="LockstepTiming.MaxStepsPerUpdate"/> ticks per update) when it holds more than
/// delay ticks of bundles. Sends Progress after every step (and as a heartbeat while stalled), its state hash every
/// <see cref="SimConstants.HashIntervalTicks"/> ticks (<c>hashOf</c>), and local commands through <see cref="Submit"/> (the Game wires
/// SimHost.Outbox to it). Pause, resume countdown, waiting, drops, desync and chat arrive as state and events. Never stamps a tick
/// itself: the host does. No wall clock; single-threaded.
/// </summary>
public sealed class LockstepClient
{
    private readonly INetTransport _transport;
    private readonly TurnBuffer _buffer;
    private readonly Action<int, IReadOnlyList<Command>> _stepSim;
    private readonly Func<int, (ulong Value, ulong[] SubHashes)> _hashOf;
    private readonly List<int> _droppedPeers = new();
    private bool _started;
    private long _now;
    private long _nextTickUs;
    private long _progressSentUs = long.MinValue;
    private long? _resumeAtUs;

    /// <summary>Creates a client on a connected transport.</summary>
    /// <param name="transport">The client transport (talks to peer 0 only).</param>
    /// <param name="delay">Command delay in ticks, from the Start message.</param>
    /// <param name="stepSim">Steps the local sim once with the bundle of the given tick.</param>
    /// <param name="hashOf">The local sim's full hash and sub-hashes after the given tick (called right after that step).</param>
    public LockstepClient(INetTransport transport, int delay, Action<int, IReadOnlyList<Command>> stepSim,
        Func<int, (ulong Value, ulong[] SubHashes)> hashOf)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _stepSim = stepSim ?? throw new ArgumentNullException(nameof(stepSim));
        _hashOf = hashOf ?? throw new ArgumentNullException(nameof(hashOf));
        _buffer = new TurnBuffer(delay);
    }

    /// <summary>The command delay in ticks.</summary>
    public int Delay => _buffer.Delay;

    /// <summary>The next tick this client will step (its sim's Tick).</summary>
    public int Tick => _buffer.NextTick;

    /// <summary>The last tick this client could step right now without waiting.</summary>
    public int ReadyThrough => _buffer.ReadyThrough;

    /// <summary>True from a pause notice until the resume countdown ends (held bundles still run, so all peers stop on one tick).</summary>
    public bool Paused { get; private set; }

    /// <summary>Peer that paused last (-1 before any pause).</summary>
    public int PausedBy { get; private set; } = -1;

    /// <summary>Milliseconds left of the resume countdown, or 0 when none runs.</summary>
    public int ResumeCountdownMs => _resumeAtUs is long at ? (int)Math.Max(0, (at - _now + 999) / 1000) : 0;

    /// <summary>Peers the host says everyone is waiting for (empty when not waiting), and for how long.</summary>
    public (int[] Peers, int WaitedMs) Waiting { get; private set; } = (Array.Empty<int>(), 0);

    /// <summary>Peers dropped so far, in the order the host announced them.</summary>
    public IReadOnlyList<int> DroppedPeers => _droppedPeers;

    /// <summary>True once the host dropped this client; it keeps stepping what it receives but its input is ignored.</summary>
    public bool DroppedSelf { get; private set; }

    /// <summary>True once the host left or the connection to it was lost (Q-M9-j: the Game offers "Continue against AI").</summary>
    public bool HostLost { get; private set; }

    /// <summary>The open desync notice (tick and peers), or null; cleared when the host resumes the match.</summary>
    public (int Tick, int[] Peers)? Desync { get; private set; }

    /// <summary>Raised for each relayed chat line.</summary>
    public event Action<NetMessage.Chat>? ChatReceived;

    /// <summary>Raised when the host announces a drop (peer, tick, reason).</summary>
    public event Action<NetMessage.PeerDropped>? PeerDropped;

    /// <summary>Raised on a desync notice (tick, peers); a pause notice comes with it.</summary>
    public event Action<int, int[]>? Desynced;

    /// <summary>Raised when the pause state changes (true = paused).</summary>
    public event Action<bool>? PauseChanged;

    /// <summary>Raised once when the host leaves or the connection drops; the string is the reason.</summary>
    public event Action<string>? HostLeft;

    /// <summary>Sends a locally issued command to the host, which stamps its tick and sequence.</summary>
    public void Submit(Command command)
    {
        if (HostLost || DroppedSelf) return;
        _transport.Send(LockstepHost.HostPeer, new NetMessage.Input(new[] { command }));
    }

    /// <summary>Asks the host to pause (anyone may, Q-M9-h).</summary>
    public void RequestPause() => _transport.Send(LockstepHost.HostPeer, new NetMessage.Pause(_transport.LocalPeer));

    /// <summary>Asks the host to start the resume countdown (anyone may, Q-M9-h).</summary>
    public void RequestResume() =>
        _transport.Send(LockstepHost.HostPeer, new NetMessage.Resume(_transport.LocalPeer, NetProtocol.ResumeCountdownMs));

    /// <summary>Sends a chat line. Audience 0 = all, 1 = team; the host fills in the sender.</summary>
    public void SendChat(byte audience, string text) =>
        _transport.Send(LockstepHost.HostPeer, new NetMessage.Chat(_transport.LocalPeer, audience, text));

    /// <summary>Leaves the match politely.</summary>
    public void Leave(string reason) => _transport.Send(LockstepHost.HostPeer, new NetMessage.Bye(reason));

    /// <summary>Pumps the transport, handles messages and steps every tick that is held and due.</summary>
    /// <param name="nowMicroseconds">The caller's monotonic clock.</param>
    public void Update(long nowMicroseconds)
    {
        _now = nowMicroseconds;
        _transport.Poll(nowMicroseconds);
        while (_transport.TryGetEvent(out PeerEvent e))
            if (e.Peer == LockstepHost.HostPeer && !e.Connected) LoseHost(e.Reason);
        while (_transport.TryReceive(out int from, out NetMessage message))
            if (from == LockstepHost.HostPeer) Handle(message);

        if (_resumeAtUs is long at && _now >= at)
        {
            _resumeAtUs = null;
            Paused = false;
            PauseChanged?.Invoke(false);
        }
        if (!_started)
        {
            _started = true;
            _nextTickUs = _now;
        }

        int steps = 0;
        bool stalled = false;
        while (steps < LockstepTiming.MaxStepsPerUpdate)
        {
            int t = Tick;
            if (!_buffer.CanStep(t)) { stalled = true; break; }
            bool due = _now >= _nextTickUs;
            bool behind = _buffer.ReadyThrough - t + 1 > Delay;   // holding more than delay ticks: the host is ahead, catch up
            if (!due && !behind) break;
            StepOne(t);
            steps++;
            if (due) _nextTickUs += LockstepTiming.TickMicroseconds;
        }
        if (stalled) _nextTickUs = Math.Max(_nextTickUs, _now);
        else if (_now - _nextTickUs > LockstepTiming.MaxDebtMicroseconds) _nextTickUs = _now - LockstepTiming.MaxDebtMicroseconds;

        // heartbeat: Progress is unreliable, so repeat it while stalled or the host may wait on a lost report forever
        if (steps > 0 || _now - _progressSentUs >= LockstepTiming.TickMicroseconds)
        {
            _progressSentUs = _now;
            if (!HostLost) _transport.Send(LockstepHost.HostPeer, new NetMessage.Progress(Tick - 1), reliable: false);
        }
    }

    private void StepOne(int t)
    {
        Command[] commands = _buffer.Take();
        _stepSim(t, commands);
        int after = t + 1;
        if (after % SimConstants.HashIntervalTicks == 0 && !HostLost)
        {
            (ulong value, ulong[] sub) = _hashOf(after);
            _transport.Send(LockstepHost.HostPeer, new NetMessage.Hash(after, value, sub));
        }
    }

    private void Handle(NetMessage message)
    {
        switch (message)
        {
            case NetMessage.TurnBundle bundle:
                _buffer.Add(bundle.Tick, bundle.Commands);
                break;
            case NetMessage.Pause pause:
                PausedBy = pause.ByPeer;
                _resumeAtUs = null;
                if (!Paused) { Paused = true; PauseChanged?.Invoke(true); }
                break;
            case NetMessage.Resume resume:
                if (Paused)
                {
                    _resumeAtUs = _now + Math.Max(0, resume.CountdownMs) * 1000L;
                    Desync = null;   // the host only resumes once it has answered a desync (Continue or a resync)
                }
                break;
            case NetMessage.Waiting waiting:
                Waiting = (waiting.Peers, waiting.WaitedMs);
                break;
            case NetMessage.PeerDropped dropped:
                if (!_droppedPeers.Contains(dropped.Peer)) _droppedPeers.Add(dropped.Peer);
                if (dropped.Peer == _transport.LocalPeer) DroppedSelf = true;
                PeerDropped?.Invoke(dropped);
                break;
            case NetMessage.Desync desync:
                Desync = (desync.Tick, desync.Peers);
                Desynced?.Invoke(desync.Tick, desync.Peers);
                break;
            case NetMessage.Chat chat:
                ChatReceived?.Invoke(chat);
                break;
            case NetMessage.Ping ping:
                _transport.Send(LockstepHost.HostPeer, new NetMessage.Pong(ping.Stamp), reliable: false);
                break;
            case NetMessage.Bye bye:
                LoseHost(bye.Reason);
                break;
        }
    }

    private void LoseHost(string reason)
    {
        if (HostLost) return;
        HostLost = true;
        HostLeft?.Invoke(reason);
    }
}
