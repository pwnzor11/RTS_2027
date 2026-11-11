// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// Radiation constants (game.toml <c>[radiation]</c>, design 05 §3, D-077). All (tune). Levels are 0..255 per cell.
/// DecayTicks = ticks per one level of decay; DamageIntervalTicks = how often units on hot cells are hurt;
/// DamageThreshold = lowest level that hurts; DamagePermille = damage per hit is level × DamagePermille / 1000, then scaled
/// by the unit's RadResistPermille; NoBuildThreshold = lowest level on which the Authority may not place structures.
/// S19 (D-028, M13-12): AshfallDamage HP every AshfallIntervalTicks to each soldier standing on an ashfall or pitchblende field cell,
/// scaled by RadResistPermille (owner-confirmed, D-207).
/// S19 (M13-12, owner option a): AshPathPenalty = how many times a normal cell's path cost a Foot unit pays to enter a field cell, so
/// soldiers walk around a field when the detour is short and still cross when it is long; 1 = no penalty.
/// </summary>
public sealed record RadiationRules(int DecayTicks, int DamageIntervalTicks, int DamageThreshold, int DamagePermille,
    int NoBuildThreshold, int AshfallDamage = 2, int AshfallIntervalTicks = 30, int AshPathPenalty = RadiationRules.DefaultAshPathPenalty)
{
    /// <summary>S19 M13-12: an ash cell costs a soldier's path four normal cells (ASSUMED, tune).</summary>
    public const int DefaultAshPathPenalty = 4;

    /// <summary>Design 05 starting values; DamagePermille 100 = a level-100 cell deals 10 HP every second (tune).</summary>
    public static RadiationRules Default { get; } = new(40, 20, 32, 100, 64);
}
