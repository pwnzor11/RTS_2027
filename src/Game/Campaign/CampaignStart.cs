// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;

namespace Rts.Game.Campaign;

/// <summary>A campaign mission that has been set up: the game plus what the campaign told it.</summary>
/// <param name="Game">The mission game, ready to run.</param>
/// <param name="Faction">The campaign's faction.</param>
/// <param name="AllowList">The human player's tech allow-list at start (mission starts_with + player tech + campaign tech set, sorted); empty when the mission is unrestricted.</param>
/// <param name="Difficulty">Skirmish tier 1..5 chosen for this run.</param>
public sealed record CampaignStart(MissionGame Game, string Faction, IReadOnlyList<string> AllowList, int Difficulty);
