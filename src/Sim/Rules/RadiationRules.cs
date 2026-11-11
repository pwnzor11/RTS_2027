// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// Radiation constants (game.toml <c>[radiation]</c>, design 05 §3, D-077). All (tune). Levels are 0..255 per cell.
/// DecayTicks = ticks per one level of decay; DamageIntervalTicks = how often units on hot cells are hurt;
/// DamageThreshold = lowest level that hurts; DamagePermille = damage per hit is level × DamagePermille / 1000, then scaled
/// by the unit's RadResistPermille; NoBuildThreshold = lowest level on which the Authority may not place structures.
/// S19 (D-028, M13-12): AshfallDamage HP every AshfallIntervalTicks to each soldier standing on an ashfall or pitchblende field cell,
/// scaled by RadResistPermille (PENDING owner number).
/// </summary>
public sealed record RadiationRules(int DecayTicks, int DamageIntervalTicks, int DamageThreshold, int DamagePermille,
    int NoBuildThreshold, int AshfallDamage = 2, int AshfallIntervalTicks = 30)
{
    /// <summary>Design 05 starting values; DamagePermille 100 = a level-100 cell deals 10 HP every second (tune).</summary>
    public static RadiationRules Default { get; } = new(40, 20, 32, 100, 64);
}
