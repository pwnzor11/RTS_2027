// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Net;

namespace Rts.Game.Lan;

/// <summary>
/// Everything a peer needs to start a LAN match (M9 wave-2 contract between the lobby and the in-game LAN code; do not change without the
/// lead). Built by the lobby on the host and on every client from the host's Start message, then handed to
/// <see cref="LanMatch.CreateScreen"/>. Player ids follow <see cref="SkirmishSettings.PlayerSlots"/>: a peer's player id is the index of
/// its slot in that list.
/// </summary>
/// <param name="Transport">The connected transport (host: peer 0 with its clients; client: connected to peer 0). The match owns it from here.</param>
/// <param name="IsHost">True on the host, which runs the turn scheduler and every AI slot (Q-M9-b, Q-M9-f).</param>
/// <param name="LocalPeer">This side's peer id (0 on the host).</param>
/// <param name="Settings">The final skirmish settings, seed included; several slots may be Human.</param>
/// <param name="PeerSlots">Peer id to slot index (into <see cref="SkirmishSettings.Slots"/>) for every human peer, the host included.</param>
/// <param name="Spectators">Peer ids that watch without a slot (at most <see cref="NetProtocol.MaxSpectators"/>).</param>
/// <param name="PeerNames">Display name of every peer (players and spectators).</param>
/// <param name="CommandDelay">Ticks between issuing a command and running it (2, 3 or 4; Q-M9-c).</param>
public sealed record LanMatchSetup(
    INetTransport Transport,
    bool IsHost,
    int LocalPeer,
    SkirmishSettings Settings,
    IReadOnlyDictionary<int, int> PeerSlots,
    IReadOnlyList<int> Spectators,
    IReadOnlyDictionary<int, string> PeerNames,
    int CommandDelay);
