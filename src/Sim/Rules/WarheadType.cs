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
}
