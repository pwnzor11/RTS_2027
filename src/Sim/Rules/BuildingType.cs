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

    private CellCoord DefaultExit() => new(FootprintW / 2, FootprintH);
}
