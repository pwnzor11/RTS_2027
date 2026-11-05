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

    // ---- schema v5 (M6-02, docs/design/10_m6_plan.md §3.2; game.toml [economy], all optional). ---------------

    /// <summary>Ticks a wreck lasts (TOML <c>wreck_ticks</c>). 0 = wrecks are off (the default; the plan's value is 1200, set by M6-08).</summary>
    public int WreckTicks { get; init; }

    /// <summary>Wreck value = cost x this / 100 (TOML <c>salvage_percent</c>, default 25).</summary>
    public int SalvagePercent { get; init; } = 25;

    /// <summary>Lowest wreck value (TOML <c>wreck_min_value</c>, default 25).</summary>
    public int WreckMinValue { get; init; } = 25;

    /// <summary>Highest wreck value (TOML <c>wreck_max_value</c>, default 1000).</summary>
    public int WreckMaxValue { get; init; } = 1000;

    /// <summary>Percent of a stored/refined load a raider steals (TOML <c>plunder_percent</c>, default 20).</summary>
    public int PlunderPercent { get; init; } = 20;

    /// <summary>Most credits one plunder may take (TOML <c>plunder_cap</c>, default 1000).</summary>
    public int PlunderCap { get; init; } = 1000;

    /// <summary>Spoils paid to a raider's owner for killing a harvester, permille of its cost (TOML <c>spoils_permille</c>, 0 = none).</summary>
    public int SpoilsPermille { get; init; }
}
