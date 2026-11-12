// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Net;

/// <summary>
/// Every message of the LAN protocol (design 20 §3 and §9; M9 contract, do not change shapes without the lead). The codec lives in
/// <see cref="NetCodec"/>. Lobby and setup payloads are opaque byte blobs that the Game layer encodes (the Net project never sees
/// Game types). Peer ids: the host is always 0; clients get 1.. in join order; spectators are ordinary peers with no slot.
/// </summary>
public abstract record NetMessage
{
    /// <summary>Client to host, first message. <paramref name="ContentHash"/> covers rules, maps and scripts (join check, design 20 §6).</summary>
    public sealed record Hello(int Protocol, string BuildVersion, ulong ContentHash, string PlayerName, bool Spectator) : NetMessage;

    /// <summary>Host to client: accepted, with the peer id the client now has.</summary>
    public sealed record Welcome(int PeerId) : NetMessage;

    /// <summary>Host to client: refused (version or content mismatch, lobby full, game running). The connection closes after it.</summary>
    public sealed record Reject(string Reason) : NetMessage;

    /// <summary>Host to all: the whole lobby (slots, map, options, delay), encoded by the Game layer. Sent on every change.</summary>
    public sealed record LobbyState(byte[] Blob) : NetMessage;

    /// <summary>Client to host: a requested lobby change for the sender's own slot (faction, colour, team, slot move), Game-encoded.</summary>
    public sealed record SlotRequest(byte[] Blob) : NetMessage;

    /// <summary>Client to host: ready flag.</summary>
    public sealed record Ready(bool IsReady) : NetMessage;

    /// <summary>Host to all: the match starts. <paramref name="SetupBlob"/> is the Game-encoded final settings (map, slots, seed, options).</summary>
    public sealed record Start(int CommandDelay, byte[] SetupBlob) : NetMessage;

    /// <summary>Client to host: commands the local player issued (ExecTick and Sequence are ignored; the host stamps them).</summary>
    public sealed record Input(Command[] Commands) : NetMessage;

    /// <summary>Host to all: the sealed commands that run on <paramref name="Tick"/> (often empty). A peer steps tick T only when it holds bundle T.</summary>
    public sealed record TurnBundle(int Tick, Command[] Commands) : NetMessage;

    /// <summary>Client to host, unreliable, every tick: the last tick the client has stepped (lag tracking, waiting overlay).</summary>
    public sealed record Progress(int SteppedTick) : NetMessage;

    /// <summary>Client to host: the full state hash after <paramref name="Tick"/> (every SimConstants.HashIntervalTicks) and its named sub-hashes.</summary>
    public sealed record Hash(int Tick, ulong Value, ulong[] SubHashes) : NetMessage;

    /// <summary>Chat outside the sim (D-110 rule; Q-M9-g). Audience 0 = all, 1 = team. Host relays; <paramref name="FromPeer"/> is set by the host.</summary>
    public sealed record Chat(int FromPeer, byte Audience, string Text) : NetMessage;

    /// <summary>Pause request (client to host) or pause notice (host to all). The host stops sealing bundles.</summary>
    public sealed record Pause(int ByPeer) : NetMessage;

    /// <summary>Resume request or notice; play resumes after <paramref name="CountdownMs"/> (3 s countdown, Q-M9-h).</summary>
    public sealed record Resume(int ByPeer, int CountdownMs) : NetMessage;

    /// <summary>Host to all: peers the game is waiting for and how long (waiting overlay, Q-M9-d). Empty list = not waiting.</summary>
    public sealed record Waiting(int[] Peers, int WaitedMs) : NetMessage;

    /// <summary>Host to all: a peer left or was dropped at <paramref name="Tick"/>; an AI takes its slot from that tick (Q-M9-d).</summary>
    public sealed record PeerDropped(int Peer, int Tick, string Reason) : NetMessage;

    /// <summary>Host to all: hashes disagree at <paramref name="Tick"/>; every peer writes a desync report and the game pauses (Q-M9-e).</summary>
    public sealed record Desync(int Tick, int[] Peers) : NetMessage;

    /// <summary>Host to all: a resync starts; the save for <paramref name="Tick"/> follows as FileChunk transfer <paramref name="TransferId"/>.</summary>
    public sealed record ResyncBegin(int Tick, int TransferId) : NetMessage;

    /// <summary>One piece of a file transfer. Kind 0 = custom map, 1 = resync save. Chunks fit one packet.</summary>
    public sealed record FileChunk(byte Kind, int TransferId, int Index, int Count, byte[] Data) : NetMessage;

    /// <summary>Round-trip probe; the peer answers with <see cref="Pong"/> carrying the same value.</summary>
    public sealed record Ping(long Stamp) : NetMessage;

    /// <summary>Answer to <see cref="Ping"/>.</summary>
    public sealed record Pong(long Stamp) : NetMessage;

    /// <summary>Orderly leave (either direction). The host leaving ends the match (D-034); clients may continue vs AI (Q-M9-j).</summary>
    public sealed record Bye(string Reason) : NetMessage;
}
