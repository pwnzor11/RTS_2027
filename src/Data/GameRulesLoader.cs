// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>Parses the global constants in game.toml.</summary>
internal static class GameRulesLoader
{
    private const string Section = "game";

    /// <summary>Parses the [game] table. Returns null when any problem was recorded.</summary>
    public static GameRules? Parse(TomlTable root, string file, List<RulesProblem> problems)
    {
        foreach (var kv in root)
        {
            if (kv.Key != Section)
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
}
