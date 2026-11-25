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

    // ---- schema v5 (M6-02, docs/design/10_m6_plan.md §3.2). Every default is "off". -------------------

    /// <summary>Added to the owner's rig pack/unpack speed, permille (TOML <c>pack_ticks_permille</c>, 0 = none).</summary>
    public int PackTicksPermille { get; init; }

    /// <summary>Added to the owner's salvage rate, permille (TOML <c>salvage_permille</c>, 0 = none).</summary>
    public int SalvagePermille { get; init; }

    /// <summary>Multiplies the credits a salvaged wreck pays the owner's salvagers, permille (TOML <c>wreck_value_permille</c>, 0 = none, 1500 = x1.5). S44 M-07.</summary>
    public int WreckValuePermille { get; init; }

    /// <summary>Added to the owner's plunder yield, permille (TOML <c>plunder_permille</c>, 0 = none).</summary>
    public int PlunderPermille { get; init; }

    /// <summary>Repair auras of the owner's units also reach vehicles beyond their usual radius (TOML <c>repairs_vehicles_at_range</c>).</summary>
    public bool RepairsVehiclesAtRange { get; init; }

    /// <summary>Cells added to the repair radius of the owner's repair auras (TOML <c>repair_range_cells</c>; 0 = the old +2 of
    /// <c>repairs_vehicles_at_range</c>). S44 M-09.</summary>
    public int RepairRangeCells { get; init; }

    /// <summary>Multiplies the heal amount of the owner's repair auras, permille (TOML <c>repair_rate_permille</c>, 0 = none). S44 M-09.</summary>
    public int RepairRatePermille { get; init; }

    /// <summary>S44 M-06: ticks an enemy wall segment stays open to the owner's team after one of its raider soldiers (Hook Runner) reaches it (TOML <c>breach_ticks</c>, 0 = no breaching).</summary>
    public int BreachTicks { get; init; }

    /// <summary>S44 M-06: ticks after a breach closes before the same wall segment can be breached again (TOML <c>breach_cooldown_ticks</c>).</summary>
    public int BreachCooldownTicks { get; init; }
}
