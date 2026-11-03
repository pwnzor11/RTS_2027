// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>Parses the global constants in game.toml.</summary>
internal static class GameRulesLoader
{
    private const string Section = "game";
    private const string EconomySection = "economy";

    /// <summary>Parses the [game] table. Returns null when any problem was recorded.</summary>
    public static GameRules? Parse(TomlTable root, string file, List<RulesProblem> problems)
    {
        foreach (var kv in root)
        {
            if (kv.Key != Section && kv.Key != EconomySection)
            {
                problems.Add(new RulesProblem(file, kv.Key, "unknown key"));
            }
        }
        if (!root.TryGetValue(Section, out object? section) || section is null)
        {
            problems.Add(new RulesProblem(file, Section, "missing required [game] table"));
            return null;
        }
        if (section is not TomlTable table)
        {
            problems.Add(new RulesProblem(file, Section, "expected a [game] table"));
            return null;
        }

        var f = new RulesFields(RulesEntry.FromTable(Section, file, table), problems);
        f.CheckKeys("tick_rate", "default_map_width", "default_map_height");
        int? tickRate = f.Int("tick_rate", 1, int.MaxValue);
        if (tickRate is { } tr && tr != SimConstants.TicksPerSecond)
        {
            problems.Add(new RulesProblem(file, "game.tick_rate",
                $"must be {SimConstants.TicksPerSecond} (the sim tick rate), got {tr}"));
        }
        int? width = f.Int("default_map_width", 1, SimConstants.MaxMapCells);
        int? height = f.Int("default_map_height", 1, SimConstants.MaxMapCells);
        if (tickRate is null || width is null || height is null)
        {
            return null;
        }
        return new GameRules(tickRate.Value, width.Value, height.Value);
    }

    /// <summary>Parses the [economy] table (required). Returns null when any problem was recorded.
    /// Unknown top-level keys are reported by <see cref="Parse"/>.</summary>
    public static EconomyRules? ParseEconomy(TomlTable root, string file, List<RulesProblem> problems)
    {
        if (!root.TryGetValue(EconomySection, out object? section) || section is null)
        {
            problems.Add(new RulesProblem(file, EconomySection, "missing required [economy] table"));
            return null;
        }
        if (section is not TomlTable table)
        {
            problems.Add(new RulesProblem(file, EconomySection, "expected an [economy] table"));
            return null;
        }

        var f = new RulesFields(RulesEntry.FromTable(EconomySection, file, table), problems);
        f.CheckKeys("starting_cash", "credits_ashfall", "credits_pitchblende", "harvest_ticks_per_unit",
            "unload_ticks_per_unit", "regrowth_interval_ticks", "sell_refund_permille", "repair_cost_permille",
            "repair_hp_per_tick", "build_radius", "max_queue", "build_ticks_per_100_credits",
            "low_power_slowdown_permille");
        int? startingCash = f.Int("starting_cash", 0, 10_000_000);
        int? ashfall = f.Int("credits_ashfall", 0, 100_000);
        int? pitchblende = f.Int("credits_pitchblende", 0, 100_000);
        int? harvestTicks = f.Int("harvest_ticks_per_unit", 1, 10_000);
        int? unloadTicks = f.Int("unload_ticks_per_unit", 1, 10_000);
        int? regrowth = f.Int("regrowth_interval_ticks", 0, 1_000_000);
        int? sell = f.Int("sell_refund_permille", 0, 1000);
        int? repairCost = f.Int("repair_cost_permille", 0, 10_000);
        int? repairHp = f.Int("repair_hp_per_tick", 1, 10_000);
        int? radius = f.Int("build_radius", 0, SimConstants.MaxMapCells);
        int? maxQueue = f.Int("max_queue", 1, 99);
        int? ticksPer100 = f.Int("build_ticks_per_100_credits", 1, 100_000);
        int[]? slowdown = f.IntArray("low_power_slowdown_permille", 3, 1000, 100_000);
        if (startingCash is null || ashfall is null || pitchblende is null || harvestTicks is null
            || unloadTicks is null || regrowth is null || sell is null || repairCost is null || repairHp is null
            || radius is null || maxQueue is null || ticksPer100 is null || slowdown is null)
        {
            return null;
        }
        return new EconomyRules(startingCash.Value, ashfall.Value, pitchblende.Value, harvestTicks.Value,
            unloadTicks.Value, regrowth.Value, sell.Value, repairCost.Value, repairHp.Value, radius.Value,
            maxQueue.Value, ticksPer100.Value, slowdown);
    }
}
