// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Net;

namespace Rts.Game.Lan.Lobby;

/// <summary>
/// The final match settings the host sends in <c>NetMessage.Start</c> (M9-05): the map, the slots (open Human slots already closed), the
/// simulation seed the host chose, which peer sits in which slot, the spectators, everyone's name and the command delay.
/// </summary>
/// <param name="Map">The map.</param>
/// <param name="Slots">The slots as they play, in screen order.</param>
/// <param name="SimSeed">Seed of the simulation and the AI drivers, chosen by the host.</param>
/// <param name="PeerSlots">Peer id to slot index, for every human player.</param>
/// <param name="Spectators">Spectator peer ids.</param>
/// <param name="Names">Display name of every peer.</param>
/// <param name="CommandDelay">2, 3 or 4 ticks.</param>
public sealed record SetupData(LobbyMap Map, IReadOnlyList<SkirmishSlot> Slots, ulong SimSeed, IReadOnlyDictionary<int, int> PeerSlots,
    IReadOnlyList<int> Spectators, IReadOnlyDictionary<int, string> Names, int CommandDelay)
{
    /// <summary>The settings these choices make, with the map resolved to a path this machine can load.</summary>
    public SkirmishSettings ToSettings(MapChoice map) => new(map, Slots, SimSeed);

    /// <summary>The <see cref="LanMatchSetup"/> that hands the match to the in-game LAN code.</summary>
    public LanMatchSetup ToMatch(INetTransport transport, bool isHost, int localPeer, MapChoice map) =>
        new(transport, isHost, localPeer, ToSettings(map), PeerSlots, Spectators, Names, CommandDelay);
}
