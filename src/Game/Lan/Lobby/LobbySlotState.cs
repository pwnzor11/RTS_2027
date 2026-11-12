// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;

namespace Rts.Game.Lan.Lobby;

/// <summary>
/// One lobby slot (M9-05): the skirmish slot choices plus who sits there. A Human slot with <paramref name="Peer"/> -1 is open (waiting for a
/// player to join). The host sits in slot 0 as peer 0.
/// </summary>
/// <param name="Slot">Kind, faction, AI tier, colour, team, handicap, credits and start units, as in skirmish setup.</param>
/// <param name="Peer">Peer id of the player in a Human slot, or -1 when nobody is there (open).</param>
/// <param name="Name">Display name of that player (empty when open).</param>
/// <param name="Ready">True once the player pressed Ready (the host counts as always ready).</param>
public sealed record LobbySlotState(SkirmishSlot Slot, int Peer = -1, string Name = "", bool Ready = false)
{
    /// <summary>The peer value of a slot nobody sits in.</summary>
    public const int NoPeer = -1;

    /// <summary>True for a Human slot nobody has taken.</summary>
    public bool IsOpen => Slot.Kind == SlotKind.Human && Peer == NoPeer;

    /// <summary>True for a Human slot with a player in it.</summary>
    public bool IsTaken => Slot.Kind == SlotKind.Human && Peer != NoPeer;
}
