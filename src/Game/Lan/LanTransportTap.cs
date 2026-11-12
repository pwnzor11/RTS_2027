// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Net;

namespace Rts.Game.Lan;

/// <summary>
/// Sits between a LAN transport and the lockstep driver (M9-10). The lockstep classes take every received message and ignore the
/// resync transfer, so the tap pulls <see cref="NetMessage.ResyncBegin"/> and <see cref="NetMessage.FileChunk"/> out of the stream for
/// the session, drops clients' resume requests while the host decides a desync, and drops state hashes for ticks at or before a resync tick in both directions (they were taken on the old state and
/// would raise a second, false desync). Everything else passes through unchanged and in order.
/// </summary>
internal sealed class LanTransportTap : INetTransport
{
    private readonly INetTransport _inner;

    public LanTransportTap(INetTransport inner) => _inner = inner;

    /// <summary>Resync transfer messages received, in order, for the session to take.</summary>
    public Queue<NetMessage> Transfers { get; } = new();

    /// <summary>Hash messages for ticks at or before this one are dropped (received and sent); -1 = none.</summary>
    public int DropHashesThrough { get; set; } = -1;

    /// <summary>Host: while set (a desync is open and the host has not chosen), clients' resume requests are dropped.</summary>
    public bool BlockResume { get; set; }

    public int LocalPeer => _inner.LocalPeer;

    public IReadOnlyList<int> Peers => _inner.Peers;

    public void Poll(long nowMicroseconds) => _inner.Poll(nowMicroseconds);

    public void Send(int peer, NetMessage message, bool reliable = true)
    {
        if (message is NetMessage.Hash h && h.Tick <= DropHashesThrough) return;
        _inner.Send(peer, message, reliable);
    }

    public void Broadcast(NetMessage message, bool reliable = true) => _inner.Broadcast(message, reliable);

    public bool TryReceive(out int fromPeer, out NetMessage message)
    {
        while (_inner.TryReceive(out fromPeer, out message))
        {
            switch (message)
            {
                case NetMessage.ResyncBegin or NetMessage.FileChunk { Kind: LanResync.ChunkKind }:
                    if (fromPeer == 0) Transfers.Enqueue(message);
                    continue;
                case NetMessage.Hash h when h.Tick <= DropHashesThrough:
                    continue;
                case NetMessage.Resume when BlockResume:
                    continue;
                default:
                    return true;
            }
        }
        return false;
    }

    public bool TryGetEvent(out PeerEvent peerEvent) => _inner.TryGetEvent(out peerEvent);

    public int RoundTripMs(int peer) => _inner.RoundTripMs(peer);

    public void Dispose() => _inner.Dispose();
}
