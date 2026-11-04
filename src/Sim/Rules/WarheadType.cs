// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>VersusPercent[(int)ArmorClass] = damage percent (integer). Splash: 0 = target only,
/// 1 = 3×3 cell block with falloff (design 02 §4). Splash hits allies when HitsAllies.</summary>
public sealed record WarheadType(string Id, IReadOnlyList<int> VersusPercent, int Splash, bool HitsAllies)
{
    /// <summary>Radiation level (0..255) added to every cell in the splash (M3-05).</summary>
    public int Contaminate { get; init; }

    /// <summary>Radiation level (0..255) removed from every cell in the splash (M3-05).</summary>
    public int Decontaminate { get; init; }

    /// <summary>Damage percent against irradiated (reactor-cored) units; 100 = no bonus.</summary>
    public int BonusVsIrradiatedPercent { get; init; } = 100;

    /// <summary>Ticks of Blackout cloud left on every cell in the splash (0 = none).</summary>
    public int BlackoutTicks { get; init; }

    // ---- schema v4 (M4-01, design 08 §3–§4). Defaults keep M3 behaviour; M4-05 implements them. -------

    /// <summary>The splash also hurts Burrowed (underground) units (TOML <c>hits_burrowed</c>, default false).</summary>
    public bool HitsBurrowed { get; init; }

    /// <summary>Damage percent against tunnel entrance buildings; 100 = no bonus (TOML <c>bonus_vs_entrance_percent</c>).</summary>
    public int BonusVsEntrancePercent { get; init; } = 100;

    /// <summary>Quick-Set Foam ticks laid on every cell in the splash (TOML <c>foam_ticks</c>, 0 = none).</summary>
    public int FoamTicks { get; init; }

    /// <summary>Percent of each hit on a garrisoned building passed to every occupant (TOML <c>garrison_damage_percent</c>, 0 = none).</summary>
    public int GarrisonDamagePercent { get; init; }
}
