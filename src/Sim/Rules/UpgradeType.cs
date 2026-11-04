// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// One upgrade (upgrades.toml, design 05 §4). Researched with Build(category Upgrade, Arg2 = index) at a building with
/// <see cref="BuildingType.Researches"/>; finishing sets bit <c>index</c> of the player's upgrade mask
/// (<see cref="World.PlayerStore.Upgrades"/>). Prerequisites may name type ids, provides names or other upgrade ids.
/// Effect is a key the sim interprets (e.g. "grouting" = Foundation Grouting, M3-04). BuildTime 0 = derive from cost.
/// </summary>
public sealed record UpgradeType(string Id, Faction Faction, int Cost, int BuildTime, IReadOnlyList<string> Prerequisites,
    string Effect);
