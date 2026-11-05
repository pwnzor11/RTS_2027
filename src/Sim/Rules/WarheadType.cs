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

    // ---- schema v5 (M6-02, docs/design/10_m6_plan.md §3.2): The Second Drift storm. StormTicks 0 = not a storm. ------------

    /// <summary>Storm radius in cells (TOML <c>storm_radius</c>, 0 = off).</summary>
    public int StormRadius { get; init; }

    /// <summary>Storm duration in ticks (TOML <c>storm_ticks</c>, 0 = not a storm).</summary>
    public int StormTicks { get; init; }

    /// <summary>Radiation added per step to every cell inside a live storm (TOML <c>storm_contaminate_step</c>, 0 = none).</summary>
    public int StormContaminateStep { get; init; }

    /// <summary>Radiation level the storm stops adding at (TOML <c>storm_contaminate_cap</c>, default 64).</summary>
    public int StormContaminateCap { get; init; } = 64;

    /// <summary>Speed multiplier per mille on enemy vehicles inside the storm (TOML <c>storm_speed_permille</c>, 0 = none).</summary>
    public int StormSpeedPermille { get; init; }

    // ---- schema v8 (M7-12): the Salvo Array. SalvoMissiles 0 = not a salvo. ---------------------------------------------

    /// <summary>Missiles in the salvo (TOML <c>salvo_missiles</c>, 0 = this superweapon is not a salvo).</summary>
    public int SalvoMissiles { get; init; }

    /// <summary>Ticks over which the missiles land, after the warning (TOML <c>salvo_ticks</c>).</summary>
    public int SalvoTicks { get; init; }

    /// <summary>Radius in cells around the target cell that the missiles scatter over (TOML <c>salvo_radius</c>).</summary>
    public int SalvoRadius { get; init; }

    /// <summary>Damage of each missile (TOML <c>salvo_damage</c>); the warhead versus table applies as for any hit.</summary>
    public int SalvoDamage { get; init; }

    /// <summary>Ticks between the order and the first impact: the launch warning (TOML <c>salvo_warn_ticks</c>).</summary>
    public int SalvoWarnTicks { get; init; }

    /// <summary>Splash radius in cells of one missile; full damage on the impact cell, half beyond (TOML <c>salvo_splash</c>, default 1).</summary>
    public int SalvoSplash { get; init; } = 1;
}
