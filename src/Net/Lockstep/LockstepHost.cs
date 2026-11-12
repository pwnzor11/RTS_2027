// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Net.Lockstep;

/// <summary>
/// The host side of LAN lockstep (M9-03, design 20 §4.2-§4.8 and §9). Paces ticks at 20 Hz from the caller's clock; before stepping
/// tick T it runs the host's AIs (<see cref="CollectAi"/>, their commands go through the <see cref="TurnScheduler"/> like any input),
/// seals and broadcasts every bundle up to T + delay, then steps its own sim with bundle T (<c>stepSim</c>; the Game passes
/// SimHost.StepWithBundle). It never runs more than <see cref="MaxLead"/> ticks ahead of the slowest active client, shows waiting,
/// offers and performs drops (an AI takes the slot, <see cref="PeerDropped"/>), relays chat, compares every client hash with its own
/// (<see cref="HashAt"/>) and pauses on a mismatch. Pausing stops sealing: every peer then runs out of bundles on the same tick.
/// Game-agnostic (Sim and Net only); no wall clock, all times come from <see cref="Update"/>. Single-threaded.
/// </summary>
public sealed class LockstepHost
{
    /// <summary>Peer id of the host.</summary>
    public const int HostPeer = 0;

    /// <summary>Longest chat line relayed; longer text is cut.</summary>
    public const int MaxChatChars = 200;

    /// <summary>Most commands one Input message may carry; a bigger message is refused whole.</summary>
    public const int MaxCommandsPerInput = 64;

    private readonly INetTransport _transport;
    private readonly TurnScheduler _scheduler;
    private readonly TurnBuffer _buffer;
    private readonly Action<int, IReadOnlyList<Command>> _stepSim;
    private readonly List<Command> _aiScratch = new();
    private readonly SortedDictionary<int, int> _progress = new();   // active-or-dropped clients: last stepped tick
    private readonly HashSet<int> _dropped = new();
    private readonly HashSet<int> _spectators = new();
    private readonly Dictionary<int, long> _lagSince = new();
    private readonly List<(int Peer, int Tick, ulong Value, ulong[] Sub)> _pendingHashes = new();
    private readonly List<int> _blockers = new();
    private bool _started;
    private long _now;
    private long _nextTickUs;
    private long? _resumeAtUs;
    private bool _waitingShown;
    private long _waitingSentUs;
    private bool _desyncOpen;
    private int _oversizeInputs;

    /// <summary>Creates a host on <paramref name="transport"/> (peer 0) with a fixed command delay.</summary>
    /// <param name="transport">The host transport; the clients already connected are the match's peers.</param>
    /// <param name="delay">Command delay in ticks (lobby choice, 2..4).</param>
    /// <param name="stepSim">Steps the host's sim once with the bundle of the given tick.</param>
    public LockstepHost(INetTransport transport, int delay, Action<int, IReadOnlyList<Command>> stepSim)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _stepSim = stepSim ?? throw new ArgumentNullException(nameof(stepSim));
        _scheduler = new TurnScheduler(delay);
        _buffer = new TurnBuffer(delay);
        MaxLead = LockstepTiming.DefaultMaxLead(delay);
        foreach (int peer in transport.Peers) if (peer != HostPeer) _progress.TryAdd(peer, -1);
    }

    /// <summary>Runs the host's AIs for the tick about to be stepped and appends their commands (stamps are replaced). Optional.</summary>
    public Action<int, List<Command>>? CollectAi { get; set; }

    /// <summary>The host sim's state hash after the given tick, if it still has it (Simulation.RecentHashes). Null disables checking.</summary>
    public Func<int, ulong?>? HashAt { get; set; }

    /// <summary>Team of a peer for team chat; null means team chat goes to everyone.</summary>
    public Func<int, int>? TeamOf { get; set; }

    /// <summary>When set, no tick at or after this one is sealed or stepped (soak runs and tests).</summary>
    public int? EndTick { get; set; }

    /// <summary>How many ticks the host may run ahead of the slowest active client (default delay * 2 + 2).</summary>
    public int MaxLead { get; set; }

    /// <summary>The command delay in ticks.</summary>
    public int Delay => _scheduler.Delay;

    /// <summary>The next tick the host will step (its sim's Tick).</summary>
    public int Tick => _buffer.NextTick;

    /// <summary>The last tick whose bundle has been sealed and sent.</summary>
    public int SealedThrough => _scheduler.SealedThrough;

    /// <summary>True from a pause until the resume countdown ends. Bundles already sealed still run.</summary>
    public bool Paused { get; private set; }

    /// <summary>Peer that paused last (-1 before any pause).</summary>
    public int PausedBy { get; private set; } = -1;

    /// <summary>Milliseconds left of the resume countdown, or 0 when none runs.</summary>
    public int ResumeCountdownMs => _resumeAtUs is long at ? (int)Math.Max(0, (at - _now + 999) / 1000) : 0;

    /// <summary>Peers the host is waiting for right now (ascending), empty when it is not waiting.</summary>
    public IReadOnlyList<int> WaitingFor => _blockers;

    /// <summary>Client hashes compared with the host's.</summary>
    public int HashesCompared { get; private set; }

    /// <summary>Client hashes that differed from the host's.</summary>
    public int HashMismatches { get; private set; }

    /// <summary>Client hashes that could not be checked (the host no longer had that tick).</summary>
    public int HashesUnchecked { get; private set; }

    /// <summary>Commands refused: wrong slot, no slot, dropped sender, or an oversized Input.</summary>
    public int RejectedCommands => _scheduler.Rejected + _oversizeInputs;

    /// <summary>The first unacknowledged desync: tick, peers, and the sub-hashes the first differing client sent.</summary>
    public (int Tick, int[] Peers, ulong[] SubHashes)? Desync { get; private set; }

    /// <summary>Raised for each bundle sealed (after it is sent): replay recording and tests.</summary>
    public event Action<NetMessage.TurnBundle>? BundleSealed;

    /// <summary>Raised when a peer is dropped; the Game gives the slot to an AI that issues from the drop tick.</summary>
    public event Action<LockstepDrop>? PeerDropped;

    /// <summary>Raised on the first hash mismatch (tick, peers); the game is already paused. See <see cref="ContinueAfterDesync"/>.</summary>
    public event Action<int, int[]>? Desynced;

    /// <summary>Raised for every chat line the host sees (its own included), with the sender filled in.</summary>
    public event Action<NetMessage.Chat>? ChatReceived;

    /// <summary>Raised when the pause state changes (true = paused).</summary>
    public event Action<bool>? PauseChanged;

    /// <summary>Lets <paramref name="peer"/> (the host itself is peer 0) command <paramref name="player"/>.</summary>
    public void SetSlot(int peer, byte player) => _scheduler.SetSlot(peer, player);

    /// <summary>Marks a client as a spectator: it gets every bundle but nobody waits for it, and it may not command.</summary>
    public void SetSpectator(int peer)
    {
        _spectators.Add(peer);
        _scheduler.ClearSlot(peer);
    }

    /// <summary>The last tick a client reported as stepped (-1 if none yet or unknown).</summary>
    public int ProgressOf(int peer) => _progress.TryGetValue(peer, out int p) ? p : -1;

    /// <summary>How many ticks a client is behind the host.</summary>
    public int LagOf(int peer) => Tick - 1 - ProgressOf(peer);

    /// <summary>True once a peer has held the game up for <see cref="NetProtocol.HostMayDropMs"/> (the Drop button).</summary>
    public bool CanDrop(int peer) =>
        _lagSince.TryGetValue(peer, out long since) && _now - since >= NetProtocol.HostMayDropMs * 1000L;

    /// <summary>True when the peer was dropped.</summary>
    public bool IsDropped(int peer) => _dropped.Contains(peer);

    /// <summary>Queues a command issued by the host's own player (slot of peer 0).</summary>
    public bool Submit(Command command) => _scheduler.Submit(HostPeer, command, Tick);

    /// <summary>Pauses the match on behalf of the host player.</summary>
    public void RequestPause() => PauseBy(HostPeer);

    /// <summary>Starts the resume countdown on behalf of the host player.</summary>
    public void RequestResume() => ResumeBy(HostPeer);

    /// <summary>Sends a chat line from the host player. Audience 0 = all, 1 = team.</summary>
    public void SendChat(byte audience, string text) => Relay(HostPeer, audience, text);

    /// <summary>Clears the open desync so a later mismatch is reported again (the host chose Continue; resume separately).</summary>
    public void ContinueAfterDesync()
    {
        _desyncOpen = false;
        Desync = null;
    }

    /// <summary>
    /// Drops a client: its slot goes to an AI from the current tick (<see cref="PeerDropped"/>), everyone is told, and nobody waits
    /// for it any more. False when the peer is unknown, the host, or already dropped.
    /// </summary>
    public bool Drop(int peer, string reason)
    {
        if (peer == HostPeer || !_progress.ContainsKey(peer) || !_dropped.Add(peer)) return false;
        int player = _scheduler.TryGetSlot(peer, out byte p) ? p : -1;
        _scheduler.ClearSlot(peer);
        _lagSince.Remove(peer);
        _blockers.Remove(peer);
        _pendingHashes.RemoveAll(h => h.Peer == peer);
        int tick = Tick;
        _transport.Broadcast(new NetMessage.PeerDropped(peer, tick, reason));
        PeerDropped?.Invoke(new LockstepDrop(peer, player, tick, reason));
        return true;
    }

    /// <summary>Pumps the transport, handles messages, steps every tick that is due and allowed, and updates waiting and drops.</summary>
    /// <param name="nowMicroseconds">The caller's monotonic clock.</param>
    public void Update(long nowMicroseconds)
    {
        _now = nowMicroseconds;
        _transport.Poll(nowMicroseconds);
        while (_transport.TryGetEvent(out PeerEvent e))
        {
            if (e.Peer == HostPeer) continue;
            if (!e.Connected) Drop(e.Peer, e.Reason.Length > 0 ? e.Reason : "disconnected");
            else if (!_started) _progress.TryAdd(e.Peer, -1);
            else if (!_progress.ContainsKey(e.Peer)) _spectators.Add(e.Peer);   // nobody joins a running match as a player (Q-M9-m)
        }
        while (_transport.TryReceive(out int from, out NetMessage message)) Handle(from, message);

        if (_resumeAtUs is long at && _now >= at)
        {
            _resumeAtUs = null;
            Paused = false;
            _nextTickUs = _now;
            PauseChanged?.Invoke(false);
        }
        if (!_started)
        {
            _started = true;
            _nextTickUs = _now;
        }

        int steps = 0;
        bool stalled = false;
        while (steps < LockstepTiming.MaxStepsPerUpdate && _now >= _nextTickUs)
        {
            int t = Tick;
            if ((EndTick is int end && t >= end) || (Paused ? !_buffer.CanStep(t) : IsBlocked(t)))
            {
                stalled = true;
                break;
            }
            StepOne(t);
            steps++;
            _nextTickUs += LockstepTiming.TickMicroseconds;
        }
        if (stalled) _nextTickUs = Math.Max(_nextTickUs, _now);
        else if (_now - _nextTickUs > LockstepTiming.MaxDebtMicroseconds) _nextTickUs = _now - LockstepTiming.MaxDebtMicroseconds;

        UpdateWaiting();
    }

    private bool IsActive(int peer) => !_dropped.Contains(peer) && !_spectators.Contains(peer);

    private bool IsBlocked(int tick)
    {
        foreach ((int peer, int stepped) in _progress)
            if (IsActive(peer) && tick > stepped + MaxLead) return true;
        return false;
    }

    private void StepOne(int t)
    {
        if (CollectAi is { } collect)
        {
            _aiScratch.Clear();
            collect(t, _aiScratch);
            foreach (Command c in _aiScratch) _scheduler.SubmitTrusted(c, t);
        }
        if (!Paused)
        {
            int last = t + Delay;
            if (EndTick is int end) last = Math.Min(last, end - 1);
            while (_scheduler.SealedThrough < last)
            {
                int k = _scheduler.SealedThrough + 1;
                var bundle = new NetMessage.TurnBundle(k, _scheduler.Seal(k));
                _transport.Broadcast(bundle);
                _buffer.Add(k, bundle.Commands);
                BundleSealed?.Invoke(bundle);
            }
        }
        Command[] commands = _buffer.Take();
        _stepSim(t, commands);
        CheckHashes();
    }

    private void Handle(int from, NetMessage message)
    {
        if (from == HostPeer || _dropped.Contains(from)) return;   // a dropped peer stays dropped (Q-M9-m)
        switch (message)
        {
            case NetMessage.Input input:
                if (input.Commands.Length > MaxCommandsPerInput) { _oversizeInputs += input.Commands.Length; break; }
                foreach (Command c in input.Commands) _scheduler.Submit(from, c, Tick);
                break;
            case NetMessage.Progress progress:
                if (_progress.TryGetValue(from, out int old) && progress.SteppedTick > old) _progress[from] = progress.SteppedTick;
                break;
            case NetMessage.Hash hash:
                if (_progress.ContainsKey(from)) _pendingHashes.Add((from, hash.Tick, hash.Value, hash.SubHashes));
                CheckHashes();
                break;
            case NetMessage.Chat chat:
                Relay(from, chat.Audience, chat.Text);
                break;
            case NetMessage.Pause:
                PauseBy(from);
                break;
            case NetMessage.Resume:
                ResumeBy(from);
                break;
            case NetMessage.Bye bye:
                Drop(from, bye.Reason.Length > 0 ? bye.Reason : "left");
                break;
            case NetMessage.Ping ping:
                _transport.Send(from, new NetMessage.Pong(ping.Stamp), reliable: false);
                break;
        }
    }

    private void CheckHashes()
    {
        if (_pendingHashes.Count == 0) return;
        int reached = Tick;   // the host's sim has stepped through tick reached - 1, so its hash for tick `reached` exists
        for (int i = 0; i < _pendingHashes.Count; i++)
        {
            (int peer, int tick, ulong value, ulong[] sub) = _pendingHashes[i];
            if (tick > reached) continue;
            _pendingHashes.RemoveAt(i--);
            ulong? own = HashAt?.Invoke(tick);
            if (own is null) { HashesUnchecked++; continue; }
            HashesCompared++;
            if (own.Value == value) continue;
            HashMismatches++;
            if (_desyncOpen) continue;
            _desyncOpen = true;
            int[] peers = { peer };
            Desync = (tick, peers, sub);
            _transport.Broadcast(new NetMessage.Desync(tick, peers));
            PauseBy(HostPeer);
            Desynced?.Invoke(tick, peers);
        }
    }

    private void PauseBy(int peer)
    {
        if (Paused && _resumeAtUs is null) return;
        bool was = Paused;
        Paused = true;
        _resumeAtUs = null;
        PausedBy = peer;
        _transport.Broadcast(new NetMessage.Pause(peer));
        if (!was) PauseChanged?.Invoke(true);
    }

    private void ResumeBy(int peer)
    {
        if (!Paused || _resumeAtUs is not null) return;
        _resumeAtUs = _now + NetProtocol.ResumeCountdownMs * 1000L;
        _transport.Broadcast(new NetMessage.Resume(peer, NetProtocol.ResumeCountdownMs));
    }

    private void Relay(int from, byte audience, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > MaxChatChars) text = text[..MaxChatChars];
        var chat = new NetMessage.Chat(from, audience == 1 ? (byte)1 : (byte)0, text);
        Func<int, int>? teamOf = TeamOf;
        bool everyone = chat.Audience == 0 || teamOf is null;
        int team = everyone ? 0 : teamOf!(from);
        foreach (int peer in _transport.Peers)
            if (peer != HostPeer && (everyone || teamOf!(peer) == team)) _transport.Send(peer, chat);
        if (everyone || teamOf!(HostPeer) == team) ChatReceived?.Invoke(chat);
    }

    private void UpdateWaiting()
    {
        _blockers.Clear();
        if (!Paused && !(EndTick is int end && Tick >= end))
            foreach ((int peer, int stepped) in _progress)
                if (IsActive(peer) && Tick > stepped + MaxLead) _blockers.Add(peer);

        foreach (int peer in _lagSince.Keys.ToArray())
            if (!_blockers.Contains(peer)) _lagSince.Remove(peer);
        if (_blockers.Count == 0)
        {
            if (_waitingShown)
            {
                _waitingShown = false;
                _transport.Broadcast(new NetMessage.Waiting(Array.Empty<int>(), 0));
            }
            return;
        }

        long since = long.MaxValue;
        var timedOut = new List<int>();
        foreach (int peer in _blockers)
        {
            if (!_lagSince.TryGetValue(peer, out long s)) _lagSince[peer] = s = _now;
            since = Math.Min(since, s);
            if (_now - s >= NetProtocol.AutoDropMs * 1000L) timedOut.Add(peer);
        }
        foreach (int peer in timedOut) Drop(peer, "timed out");
        if (_blockers.Count == 0) return;

        int waitedMs = (int)Math.Min(int.MaxValue, (_now - since) / 1000);
        if (waitedMs < NetProtocol.WaitOverlayMs) return;
        if (_waitingShown && _now - _waitingSentUs < LockstepTiming.WaitingRepeatMicroseconds) return;
        _waitingShown = true;
        _waitingSentUs = _now;
        _transport.Broadcast(new NetMessage.Waiting(_blockers.ToArray(), waitedMs));
    }
}
