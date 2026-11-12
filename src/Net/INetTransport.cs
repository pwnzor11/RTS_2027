// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Net;

/// <summary>
/// A message transport between the host (peer 0) and its clients (M9 contract, design 20 §9). Host-star: a client only ever talks to
/// peer 0; the host talks to every client. Reliable messages arrive exactly once and in send order per peer; unreliable ones may be
/// lost (used for <see cref="NetMessage.Progress"/>, <see cref="NetMessage.Ping"/>, <see cref="NetMessage.Pong"/>).
/// Single-threaded: call <see cref="Poll"/> from the game loop; nothing is raised from other threads. Implementations:
/// UdpTransport (real sockets, own reliability layer, Q-M9-a) and LoopbackNetwork (in-process, tests and the soak tool).
/// </summary>
public interface INetTransport : IDisposable
{
    /// <summary>This side's peer id: 0 on the host, the Welcome id on a client (-1 until welcomed).</summary>
    int LocalPeer { get; }

    /// <summary>Connected remote peers (on a client: just 0 once connected).</summary>
    IReadOnlyList<int> Peers { get; }

    /// <summary>Pumps the socket: reads packets, sends acks, resends overdue reliable messages, detects timeouts.</summary>
    /// <param name="nowMicroseconds">A monotonic clock from the caller (the transport never reads the wall clock itself).</param>
    void Poll(long nowMicroseconds);

    /// <summary>Queues a message to one peer.</summary>
    void Send(int peer, NetMessage message, bool reliable = true);

    /// <summary>Queues a message to every connected peer (host only; on a client it is the same as Send(0, ...)).</summary>
    void Broadcast(NetMessage message, bool reliable = true);

    /// <summary>Takes the next received message, in arrival order.</summary>
    bool TryReceive(out int fromPeer, out NetMessage message);

    /// <summary>Takes the next connect/disconnect event.</summary>
    bool TryGetEvent(out PeerEvent peerEvent);

    /// <summary>Smoothed round-trip time to a peer in milliseconds (0 if unknown).</summary>
    int RoundTripMs(int peer);
}

/// <summary>A peer connected or disconnected (timeout, Bye, or socket error). <paramref name="Reason"/> is for logs and the UI.</summary>
public readonly record struct PeerEvent(int Peer, bool Connected, string Reason);
