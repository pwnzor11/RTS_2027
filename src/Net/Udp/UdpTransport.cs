// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;
using System.Net.Sockets;

namespace Rts.Net.Udp;

/// <summary>
/// <see cref="INetTransport"/> over one non-blocking UDP socket with the <see cref="ReliableChannel"/> reliability layer per peer.
/// No threads: <see cref="Poll"/> does all I/O. Host side: <see cref="Host"/> binds a port; a datagram from an unknown endpoint
/// creates a PENDING peer with a negative id (-2, -3, ...). Its first message must be a <see cref="NetMessage.Hello"/>, which is
/// surfaced through <see cref="TryReceive"/> with that pending id; the lobby layer then calls <see cref="Accept"/> (the peer gets its
/// real id, a Connected event, and the lobby sends Welcome itself) or <see cref="Reject"/> (a Reject message is sent, then the
/// peer is closed once it is acknowledged or after two seconds). Anything else from a pending peer is ignored. Client side:
/// <see cref="Connect"/> queues the Hello as the first reliable message; <see cref="LocalPeer"/> becomes the Welcome id and the
/// Connected event fires when Welcome arrives. A Reject or Bye ends the connection after being surfaced.
/// </summary>
public sealed class UdpTransport : INetTransport
{
    private const int MaxPending = 16;
    private const int MaxReceivesPerPoll = 1024;
    private const int MaxPacketsPerPeerPerPoll = 16;
    private const long ClosingGraceMicros = 2_000_000;

    private sealed class Peer
    {
        public int Id;
        public IPEndPoint EndPoint = null!;
        public ReliableChannel Channel = null!;
        public bool Pending, Connected, HelloSurfaced, Closing, Started;
        public long CloseDeadline;
    }

    private readonly Socket _socket;
    private readonly bool _isHost;
    private readonly long _timeoutMicros;
    private readonly List<Peer> _peers = new();
    private readonly List<int> _connected = new();
    private readonly Queue<(int From, NetMessage Message)> _inbox = new();
    private readonly Queue<PeerEvent> _events = new();
    private readonly byte[] _recvBuf = new byte[2048];
    private readonly byte[] _sendBuf = new byte[NetProtocol.MaxPacketBytes];
    private int _nextPendingId = -2;
    private long _lastNow;
    private bool _clockStarted, _disposed;

    private UdpTransport(Socket socket, bool isHost, int timeoutMs)
    {
        _socket = socket;
        _isHost = isHost;
        _timeoutMicros = (long)timeoutMs * 1000;
        LocalPeer = isHost ? 0 : -1;
    }

    /// <inheritdoc />
    public int LocalPeer { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<int> Peers => _connected;

    /// <summary>The bound local endpoint (use it to learn an ephemeral port chosen with port 0).</summary>
    public IPEndPoint LocalEndPoint => (IPEndPoint)_socket.LocalEndPoint!;

    /// <summary>Opens a host on <paramref name="port"/> (0 = any free port). <paramref name="timeoutMs"/> is the silence limit.</summary>
    public static UdpTransport Host(int port, int timeoutMs = NetProtocol.TimeoutMs, IPAddress? bindAddress = null)
    {
        var socket = NewSocket(AddressFamily.InterNetwork);
        try { socket.Bind(new IPEndPoint(bindAddress ?? IPAddress.Any, port)); }
        catch { socket.Dispose(); throw; }
        return new UdpTransport(socket, true, timeoutMs);
    }

    /// <summary>Starts connecting to a host; <paramref name="hello"/> is sent as the first reliable message.</summary>
    public static UdpTransport Connect(IPEndPoint host, NetMessage.Hello hello, int timeoutMs = NetProtocol.TimeoutMs)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(hello);
        var socket = NewSocket(host.AddressFamily);
        try { socket.Bind(new IPEndPoint(host.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0)); }
        catch { socket.Dispose(); throw; }
        var t = new UdpTransport(socket, false, timeoutMs);
        var peer = new Peer { Id = 0, EndPoint = host, Channel = new ReliableChannel(0), Pending = false };
        t._peers.Add(peer);
        peer.Channel.QueueReliable(NetCodec.Encode(hello));
        return t;
    }

    private static Socket NewSocket(AddressFamily family)
    {
        var s = new Socket(family, SocketType.Dgram, ProtocolType.Udp) { Blocking = false };
        if (OperatingSystem.IsWindows())
        {
            try { s.IOControl(-1744830452, new byte[4], null); } catch (SocketException) { }   // SIO_UDP_CONNRESET off
        }
        return s;
    }

    /// <inheritdoc />
    public void Poll(long nowMicroseconds)
    {
        if (_disposed) return;
        _lastNow = nowMicroseconds;
        if (!_clockStarted)
        {
            _clockStarted = true;
            foreach (Peer p in _peers) StartPeer(p, nowMicroseconds);
        }
        ReceiveAll(nowMicroseconds);
        foreach (Peer p in _peers.ToArray())
        {
            if (!_peers.Contains(p)) continue;
            if (p.Channel.Faulted) { Drop(p, "protocol violation"); continue; }
            if (p.Closing)
            {
                if (p.CloseDeadline == 0) p.CloseDeadline = nowMicroseconds + ClosingGraceMicros;
                if (p.Channel.AllAcknowledged || nowMicroseconds > p.CloseDeadline) { Drop(p, "closed"); continue; }
            }
            else if (nowMicroseconds - p.Channel.LastReceivedMicros > _timeoutMicros)
            {
                Drop(p, "timeout");
                continue;
            }
            Flush(p, nowMicroseconds);
        }
    }

    private static void StartPeer(Peer p, long now)
    {
        if (p.Started) return;
        p.Started = true;
        p.Channel.Restart(now);
    }

    /// <inheritdoc />
    public void Send(int peer, NetMessage message, bool reliable = true)
    {
        Peer? p = Find(peer);
        if (p == null || p.Closing) return;
        byte[] bytes = NetCodec.Encode(message);
        if (reliable || bytes.Length > ReliableChannel.MaxUnitBytes) p.Channel.QueueReliable(bytes);
        else p.Channel.QueueUnreliable(bytes);
    }

    /// <inheritdoc />
    public void Broadcast(NetMessage message, bool reliable = true)
    {
        if (!_isHost) { Send(0, message, reliable); return; }
        foreach (int id in _connected.ToArray()) Send(id, message, reliable);
    }

    /// <inheritdoc />
    public bool TryReceive(out int fromPeer, out NetMessage message)
    {
        if (_inbox.Count > 0)
        {
            (fromPeer, message) = _inbox.Dequeue();
            return true;
        }
        fromPeer = -1;
        message = null!;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetEvent(out PeerEvent peerEvent)
    {
        if (_events.Count > 0) { peerEvent = _events.Dequeue(); return true; }
        peerEvent = default;
        return false;
    }

    /// <inheritdoc />
    public int RoundTripMs(int peer) => Find(peer) is { } p ? (int)(p.Channel.SmoothedRttMicros / 1000) : 0;

    /// <summary>Host: promotes a pending peer (id from a surfaced Hello, negative) to connected peer <paramref name="peerId"/> (1 or more, unused).</summary>
    /// <returns>False when the pending peer is gone or the id is invalid or taken.</returns>
    public bool Accept(int pendingPeer, int peerId)
    {
        if (!_isHost || peerId < 1 || Find(peerId) != null) return false;
        Peer? p = _peers.Find(x => x.Pending && x.Id == pendingPeer && !x.Closing);
        if (p == null) return false;
        p.Pending = false; p.Connected = true; p.Id = peerId;
        _connected.Add(peerId);
        _events.Enqueue(new PeerEvent(peerId, true, "accepted"));
        return true;
    }

    /// <summary>Host: refuses a pending peer. The Reject message is delivered reliably, then the peer is closed.</summary>
    public void Reject(int pendingPeer, string reason)
    {
        Peer? p = _peers.Find(x => x.Pending && x.Id == pendingPeer);
        if (p == null || p.Closing) return;
        p.Channel.QueueReliable(NetCodec.Encode(new NetMessage.Reject(reason)));
        p.Closing = true;
    }

    /// <summary>Ends the connection to a peer now (sends Bye best-effort, raises a Disconnected event).</summary>
    public void Disconnect(int peer, string reason)
    {
        Peer? p = Find(peer);
        if (p == null) return;
        p.Channel.QueueReliable(NetCodec.Encode(new NetMessage.Bye(reason)));
        Flush(p, _lastNow);
        Drop(p, "local: " + reason);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        foreach (Peer p in _peers.ToArray())
        {
            if (!p.Connected) continue;
            p.Channel.QueueReliable(NetCodec.Encode(new NetMessage.Bye("closed")));
            Flush(p, _lastNow);
        }
        _disposed = true;
        _socket.Dispose();
    }

    private Peer? Find(int id) => _peers.Find(x => x.Id == id && (x.Pending || x.Connected || !_isHost));

    private void ReceiveAll(long now)
    {
        for (int i = 0; i < MaxReceivesPerPoll; i++)
        {
            int n;
            EndPoint from = new IPEndPoint(_socket.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
            try
            {
                if (!_socket.Poll(0, SelectMode.SelectRead)) return;
                n = _socket.ReceiveFrom(_recvBuf, 0, _recvBuf.Length, SocketFlags.None, ref from);
            }
            catch (SocketException) { continue; }   // e.g. ICMP port unreachable surfaced as a reset: ignore, the timeout handles it
            catch (ObjectDisposedException) { return; }
            var ep = (IPEndPoint)from;
            Peer? peer = _peers.Find(x => x.EndPoint.Equals(ep));
            if (peer == null)
            {
                if (!_isHost || _peers.Count(x => x.Pending) >= MaxPending) continue;
                var fresh = new Peer { Id = _nextPendingId--, EndPoint = ep, Channel = new ReliableChannel(now), Pending = true, Started = true };
                if (!fresh.Channel.OnPacket(now, _recvBuf.AsSpan(0, n))) continue;
                _peers.Add(fresh);
                peer = fresh;
            }
            else if (!peer.Channel.OnPacket(now, _recvBuf.AsSpan(0, n))) continue;
            DrainMessages(peer);
        }
    }

    private void DrainMessages(Peer peer)
    {
        while (_peers.Contains(peer) && peer.Channel.TryDequeueMessage(out byte[] bytes))
        {
            NetMessage message;
            try { message = NetCodec.Decode(bytes); }
            catch (InvalidDataException) { Drop(peer, "malformed message"); return; }
            if (peer.Pending)
            {
                if (message is NetMessage.Hello && !peer.HelloSurfaced)
                {
                    peer.HelloSurfaced = true;
                    _inbox.Enqueue((peer.Id, message));
                }
                continue;
            }
            if (!_isHost && message is NetMessage.Welcome w && !peer.Connected)
            {
                peer.Connected = true;
                LocalPeer = w.PeerId;
                _connected.Add(0);
                _events.Enqueue(new PeerEvent(0, true, "welcome"));
            }
            _inbox.Enqueue((peer.Id, message));
            if (message is NetMessage.Reject r) Drop(peer, "rejected: " + r.Reason);
            else if (message is NetMessage.Bye b) Drop(peer, "bye: " + b.Reason);
        }
    }

    private void Drop(Peer peer, string reason)
    {
        if (!_peers.Remove(peer)) return;
        bool announce = peer.Connected || !_isHost;
        if (peer.Connected) _connected.Remove(peer.Id);
        peer.Connected = false;
        if (announce && !peer.Closing) _events.Enqueue(new PeerEvent(peer.Id, false, reason));
    }

    private void Flush(Peer peer, long now)
    {
        for (int i = 0; i < MaxPacketsPerPeerPerPoll; i++)
        {
            if (!peer.Channel.TryBuildPacket(now, _sendBuf, out int len)) return;
            try { _socket.SendTo(_sendBuf, 0, len, SocketFlags.None, peer.EndPoint); }
            catch (SocketException) { return; }
            catch (ObjectDisposedException) { return; }
        }
    }
}
