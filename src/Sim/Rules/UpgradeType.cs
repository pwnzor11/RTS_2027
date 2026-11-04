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
    string Effect)
{
    /// <summary>M4: tech tier 1..3 (TOML <c>tier</c>, default 1). Tier sugar appends the faction's tier provides to the prerequisites.</summary>
    public int Tier { get; init; } = 1;

    /// <summary>M4: techs.toml <c>[[effect]]</c> ids applied to the owner's rules once researched (TOML <c>effects</c>, default
    /// empty; M4-04 applies them in list order).</summary>
    public IReadOnlyList<string> Effects { get; init; } = Array.Empty<string>();
}
