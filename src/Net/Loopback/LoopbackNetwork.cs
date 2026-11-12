// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Net.Loopback;

/// <summary>
/// An in-process network of <see cref="INetTransport"/> endpoints (M9-03): one host (peer 0) and any number of clients (1, 2, ... in
/// the order <see cref="AddClient"/> is called). Messages are handed over as <see cref="NetMessage"/> objects (no encoding). Every
/// message gets a delivery time of <c>now + LatencyMs + jitter</c> in virtual milliseconds; "now" is the latest clock any endpoint
/// was polled with, so the caller's clock drives everything and nothing reads the wall clock. Reliable messages keep send order per
/// direction; unreliable ones may overtake each other and are lost with <see cref="UnreliableLossPercent"/>. Jitter and loss come
/// from a seeded generator, so the same calls in the same order give the same deliveries. Single-threaded, like the game loop.
/// </summary>
public sealed class LoopbackNetwork
{
    private readonly List<Endpoint> _endpoints = new();
    private ulong _rng;
    private long _sendOrder;

    /// <summary>Creates a network with its host endpoint. <paramref name="seed"/> drives jitter and loss.</summary>
    public LoopbackNetwork(ulong seed = 1)
    {
        _rng = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        _endpoints.Add(new Endpoint(this, 0));
    }

    /// <summary>Fixed one-way latency in milliseconds added to every message.</summary>
    public int LatencyMs { get; set; }

    /// <summary>Extra one-way latency, uniform in 0..JitterMs (inclusive), drawn per message.</summary>
    public int JitterMs { get; set; }

    /// <summary>Chance in percent (0..100) that an unreliable message is lost.</summary>
    public int UnreliableLossPercent { get; set; }

    /// <summary>The latest clock (microseconds) any endpoint was polled with.</summary>
    public long NowMicroseconds { get; private set; }

    /// <summary>Messages handed to a receiver so far.</summary>
    public long Delivered { get; private set; }

    /// <summary>Unreliable messages dropped by the loss setting so far.</summary>
    public long Lost { get; private set; }

    /// <summary>The host endpoint (peer 0).</summary>
    public INetTransport Host => _endpoints[0];

    /// <summary>Adds a client endpoint, connected at once; the host sees a connect event on its next poll.</summary>
    public INetTransport AddClient()
    {
        var client = new Endpoint(this, _endpoints.Count);
        _endpoints.Add(client);
        client.Connect(0);
        _endpoints[0].Connect(client.Id);
        return client;
    }

    /// <summary>Cuts a client off (a vanished player): both sides get a disconnect event and messages in flight are discarded.</summary>
    public void Disconnect(int peer, string reason)
    {
        if (peer <= 0 || peer >= _endpoints.Count) throw new ArgumentOutOfRangeException(nameof(peer));
        _endpoints[peer].Cut(0, reason);
        _endpoints[0].Cut(peer, reason);
    }

    private void Route(Endpoint from, int to, NetMessage message, bool reliable)
    {
        if (to < 0 || to >= _endpoints.Count) return;
        Endpoint target = _endpoints[to];
        if (!from.IsLinked(to) || !target.IsLinked(from.Id)) return;
        if (!reliable && UnreliableLossPercent > 0 && (int)(Next() % 100) < UnreliableLossPercent)
        {
            Lost++;
            return;
        }
        long due = NowMicroseconds + (LatencyMs + (JitterMs > 0 ? (int)(Next() % (ulong)(JitterMs + 1)) : 0)) * 1000L;
        if (reliable)
        {
            // reliable messages arrive in send order per direction: never before the previous reliable one
            due = Math.Max(due, from.LastReliableDue(to));
            from.SetLastReliableDue(to, due);
        }
        target.Enqueue(new InFlight(due, _sendOrder++, from.Id, message));
    }

    private ulong Next()
    {
        // xorshift64*: fixed algorithm, so deliveries do not depend on the runtime's Random implementation
        _rng ^= _rng >> 12;
        _rng ^= _rng << 25;
        _rng ^= _rng >> 27;
        return _rng * 0x2545F4914F6CDD1DUL;
    }

    private readonly record struct InFlight(long Due, long Order, int From, NetMessage Message);

    private sealed class Endpoint : INetTransport
    {
        private readonly LoopbackNetwork _net;
        private readonly List<int> _peers = new();
        private readonly List<InFlight> _inFlight = new();
        private readonly Queue<(int From, NetMessage Message)> _received = new();
        private readonly Queue<PeerEvent> _events = new();
        private readonly Dictionary<int, long> _lastReliableDue = new();
        private bool _disposed;

        public Endpoint(LoopbackNetwork net, int id)
        {
            _net = net;
            Id = id;
        }

        public int Id { get; }

        public int LocalPeer => Id;

        public IReadOnlyList<int> Peers => _peers;

        public bool IsLinked(int peer) => !_disposed && _peers.Contains(peer);

        public long LastReliableDue(int to) => _lastReliableDue.TryGetValue(to, out long d) ? d : 0;

        public void SetLastReliableDue(int to, long due) => _lastReliableDue[to] = due;

        public void Connect(int peer)
        {
            _peers.Add(peer);
            _events.Enqueue(new PeerEvent(peer, true, "connected"));
        }

        public void Cut(int peer, string reason)
        {
            if (!_peers.Remove(peer)) return;
            _inFlight.RemoveAll(m => m.From == peer);
            _events.Enqueue(new PeerEvent(peer, false, reason));
        }

        public void Enqueue(InFlight message) => _inFlight.Add(message);

        public void Poll(long nowMicroseconds)
        {
            if (nowMicroseconds > _net.NowMicroseconds) _net.NowMicroseconds = nowMicroseconds;
            long now = _net.NowMicroseconds;
            // deliver everything due, earliest first (ties in send order)
            while (true)
            {
                int best = -1;
                for (int i = 0; i < _inFlight.Count; i++)
                {
                    InFlight m = _inFlight[i];
                    if (m.Due > now) continue;
                    if (best < 0 || m.Due < _inFlight[best].Due || (m.Due == _inFlight[best].Due && m.Order < _inFlight[best].Order)) best = i;
                }
                if (best < 0) break;
                InFlight next = _inFlight[best];
                _inFlight.RemoveAt(best);
                _received.Enqueue((next.From, next.Message));
                _net.Delivered++;
            }
        }

        public void Send(int peer, NetMessage message, bool reliable = true)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (_disposed) return;
            _net.Route(this, peer, message, reliable);
        }

        public void Broadcast(NetMessage message, bool reliable = true)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (_disposed) return;
            if (Id != 0)
            {
                _net.Route(this, 0, message, reliable);
                return;
            }
            foreach (int peer in _peers.ToArray()) _net.Route(this, peer, message, reliable);
        }

        public bool TryReceive(out int fromPeer, out NetMessage message)
        {
            if (_received.Count > 0)
            {
                (fromPeer, message) = _received.Dequeue();
                return true;
            }
            fromPeer = -1;
            message = null!;
            return false;
        }

        public bool TryGetEvent(out PeerEvent peerEvent) => _events.TryDequeue(out peerEvent);

        public int RoundTripMs(int peer) => _peers.Contains(peer) ? 2 * _net.LatencyMs + _net.JitterMs : 0;

        public void Dispose()
        {
            if (_disposed) return;
            if (Id == 0)
            {
                foreach (int peer in _peers.ToArray()) { _net._endpoints[peer].Cut(0, "host closed"); Cut(peer, "closed"); }
            }
            else if (_peers.Contains(0))
            {
                _net._endpoints[0].Cut(Id, "client closed");
                Cut(0, "closed");
            }
            _disposed = true;
        }
    }
}
