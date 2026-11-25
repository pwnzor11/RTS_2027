// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Range in leptons; RateOfFire = ticks between shots; ProjectileSpeed leptons/tick (0 = instant hit);
/// Warhead = index into SimRules.Warheads.</summary>
public sealed record WeaponType(string Id, int Damage, int Range, int MinRange, int RateOfFire,
    int ProjectileSpeed, int Warhead)
{
    /// <summary>What this weapon may fire at (TOML <c>targets</c> ground|air|both; default ground). M3-06 enforces it.</summary>
    public WeaponTargets Targets { get; init; }

    /// <summary>S44 (M-17): naval gunfire support (TOML <c>needs_spotter</c>, default false). A ship may be ordered to fire at a ground cell with this
    /// weapon only while the owner's team sees that cell (a Pathfinder, Gyro, scout or the ship's own sight), so the cell is "designated"; the order
    /// lapses when the cell goes unseen.</summary>
    public bool NeedsSpotter { get; init; }
}
