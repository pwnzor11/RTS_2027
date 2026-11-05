// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Rules;

/// <summary>
/// Sim-side building type. Footprint in cells; Power: positive = produces, negative = drains.
/// Optional M2 fields (init-only): Category, Produces (null = none), BuildTime (0 = derive from cost), Storage,
/// Refinery, Dock (cell relative to the footprint's top-left), Exit (relative cell; default just below the footprint
/// middle), FreeUnit (vehicle index spawned with a refinery, -1 = none), Wall, NeedsPower.
/// </summary>
public sealed record BuildingType(string Id, int FootprintW, int FootprintH, int MaxHealth, ArmorClass Armor,
    int Sight, int PrimaryWeapon, int Cost, int Power, IReadOnlyList<string> Prerequisites,
    IReadOnlyList<string> Provides)
{
    private readonly CellCoord? _exit;

    /// <summary>Which build queue this structure is built from.</summary>
    public ProductionCategory Category { get; init; } = ProductionCategory.Structure;

    /// <summary>The unit category this building produces, or null.</summary>
    public ProductionCategory? Produces { get; init; }

    /// <summary>Build time in ticks; 0 = derive from cost (<see cref="SimRules.BuildTicks"/>).</summary>
    public int BuildTime { get; init; }

    /// <summary>Credits of storage this building adds.</summary>
    public int Storage { get; init; }

    /// <summary>True if harvesters unload here.</summary>
    public bool Refinery { get; init; }

    /// <summary>Unload cell relative to the footprint's top-left.</summary>
    public CellCoord Dock { get; init; }

    /// <summary>Unit exit cell relative to the footprint's top-left; default is (FootprintW/2, FootprintH).</summary>
    public CellCoord Exit { get => _exit ?? DefaultExit(); init => _exit = value; }

    /// <summary>Same as <see cref="Exit"/>: the explicit exit or the default just below the footprint middle.</summary>
    public CellCoord ExitOrDefault() => Exit;

    /// <summary>True if an exit was set explicitly (rather than defaulted).</summary>
    public bool HasExplicitExit => _exit.HasValue;

    /// <summary>Vehicle type index delivered free with this building, or -1.</summary>
    public int FreeUnit { get; init; } = -1;

    /// <summary>True for wall segments (1x1, cannot produce).</summary>
    public bool Wall { get; init; }

    /// <summary>True if this building stops working on low power (defences).</summary>
    public bool NeedsPower { get; init; }

    /// <summary>False for structures that can't be produced from a queue (e.g. the starting Construction Yard). Default true.</summary>
    public bool Buildable { get; init; } = true;

    // ---- schema v3 (M3-01, design 05 §1–§4). Defaults keep pre-M3 behaviour. -------------------------

    /// <summary>Owning faction (TOML <c>faction</c>; default from the id prefix <c>auth_</c>/<c>dir_</c>, else Authority).</summary>
    public Faction Faction { get; init; }

    /// <summary>Largest tunnel size this building admits as a network entrance (TOML <c>entrance</c> none|S|M); None = not an entrance.</summary>
    public TunnelSize Entrance { get; init; }

    /// <summary>Ticks to draw in one tunnel point at this entrance (Hatch class 20, Bore Head class 13). Default 20.</summary>
    public int TicksPerPoint { get; init; } = 20;

    /// <summary>Upgrades (ProductionCategory.Upgrade) are researched here.</summary>
    public bool Researches { get; init; }

    /// <summary>Gate wall piece (1×1, also a wall): passable for the owner's team, a wall for everyone else; always part of the seal.</summary>
    public bool Gate { get; init; }

    /// <summary>Seismograph radius in cells: reveals enemy burrowed units and in-progress telegraphs to the owner (0 = none).</summary>
    public int SeismicRadius { get; init; }

    /// <summary>This building's sight reveals cells inside a Blackout cloud (Searchlight).</summary>
    public bool SeesThroughBlackout { get; init; }

    /// <summary>Static Broadcast radius in cells: hides those cells on enemy minimaps (view only; 0 = none).</summary>
    public int JamRadius { get; init; }

    /// <summary>Superweapon id fired with the Special command, or "" for none (M3-13 interprets it).</summary>
    public string Superweapon { get; init; } = "";

    /// <summary>Ticks for the superweapon to charge (0 with no superweapon).</summary>
    public int SuperweaponChargeTicks { get; init; }

    /// <summary>When positive, firing needs an own finished entrance within this many cells of the target (Sunrise Device: 8).</summary>
    public int SuperweaponEntranceRange { get; init; }

    /// <summary>Building type index this building upgrades in place into with the Upgrade command (TOML <c>morphs_into</c>, a
    /// building id; Hatch to Bore Head), or -1 for none (M3-03).</summary>
    public int MorphsInto { get; init; } = -1;

    /// <summary>Ticks the in-place upgrade takes (TOML <c>morph_ticks</c>); 0 = derive from the cost difference (M3-03).</summary>
    public int MorphTicks { get; init; }

    // ---- schema v4 (M4-01, design 08 §3–§4). Defaults keep M3 behaviour. ------------------------------

    /// <summary>Tech tier 1..3 (TOML <c>tier</c>, default 1).</summary>
    public int Tier { get; init; } = 1;

    /// <summary>Soldiers this building can hold (TOML <c>garrison</c> 0..10, default 0 = not garrisonable).</summary>
    public int Garrison { get; init; }

    /// <summary>Leptons added to an occupant's weapon range (TOML <c>garrison_range_bonus</c>, default 256 = one cell).</summary>
    public int GarrisonRangeBonus { get; init; } = 256;

    /// <summary>Occupant reload multiplier per mille (TOML <c>garrison_rof_permille</c>, default 750).</summary>
    public int GarrisonRofPermille { get; init; } = 750;

    private CellCoord DefaultExit() => new(FootprintW / 2, FootprintH);

    // ---- schema v5 (M6-02, docs/design/10_m6_plan.md §3.2). Every default is "off". -------------------

    /// <summary>Rig: vehicle type index the Pack command turns this building into, or -1 (TOML <c>packs_into</c>, a vehicle id).</summary>
    public int PacksInto { get; init; } = -1;

    /// <summary>Ticks the packing takes (TOML <c>pack_ticks</c>, 0 = off).</summary>
    public int PackTicks { get; init; }

    // ---- schema v7 (SUP-1, docs/design/13_supply_cap.md §4). ---------------------------------------------

    /// <summary>Supply cap this building adds while completed, unpacked and alive (TOML <c>supply_provided</c>, default 0).</summary>
    public int SupplyProvided { get; init; }

    /// <summary>Cap contribution of an unstocked Expedition store (TOML <c>supply_unstocked</c>); -1 = same as <see cref="SupplyProvided"/>.
    /// Parsed only: no behaviour until the Expedition lands.</summary>
    public int SupplyUnstocked { get; init; } = -1;
}
