// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Lan.Lobby;

/// <summary>A peer that watches the match without a slot (M9-05; at most two).</summary>
/// <param name="Peer">The peer id.</param>
/// <param name="Name">Display name.</param>
public sealed record LobbySpectator(int Peer, string Name);
