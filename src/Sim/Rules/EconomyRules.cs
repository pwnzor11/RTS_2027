// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// Economy and production constants (game.toml [economy], M2). Credits are whole integers; ticks are sim ticks;
/// permille values are per thousand. LowPowerSlowdownPermille has three entries: the production slowdown
/// when the power ratio is below 100 %, 75 % and 50 %. Each entry IS the multiplier on build ticks (1000 = x1, 1500 = x1.5),
/// not an amount added to 1000.
/// </summary>
public sealed record EconomyRules(
    int StartingCash,
    int CreditsAshfall,
    int CreditsPitchblende,
    int HarvestTicksPerUnit,
    int UnloadTicksPerUnit,
    int RegrowthIntervalTicks,
    int SellRefundPermille,
    int RepairCostPermille,
    int RepairHpPerTick,
    int BuildRadius,
    int MaxQueue,
    int BuildTicksPer100Credits,
    IReadOnlyList<int> LowPowerSlowdownPermille)
{
    /// <summary>Placeholder values (not balance).</summary>
    public static EconomyRules Default { get; } =
        new(5000, 25, 60, 4, 2, 0, 500, 500, 2, 2, 5, 20, new[] { 1500, 2500, 4000 });
}
