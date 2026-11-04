// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>Parses upgrade entries (upgrades.toml, M3): <c>[&lt;id&gt;]</c> with faction (default from the id prefix), cost,
/// build_time (optional, 0 = from cost), prerequisites (optional; type ids, provides names or upgrade ids) and effect (optional
/// string key, default ""). Schema v4 (M4-01): <c>tier</c> (1..3, default 1) and <c>effects</c> (techs.toml effect ids, default []).</summary>
internal static class UpgradeRulesLoader
{
    private static readonly string[] UpgradeKeys = ["faction", "cost", "build_time", "prerequisites", "effect",
        // schema v4 (M4-01)
        "tier", "effects"];

    /// <summary>Parses one upgrade.</summary>
    public static UpgradeType? ParseUpgrade(RulesFields f, RulesReferences refs)
    {
        f.CheckKeys(UpgradeKeys);
        Faction? faction = f.FactionOrDefault();
        int? cost = f.Int("cost", 0, int.MaxValue);
        int? buildTime = f.OptInt("build_time", 0, int.MaxValue, 0);
        List<string>? prerequisites = f.OptStrList("prerequisites");
        string? effect = f.OptStr("effect", "");
        int? tier = f.OptInt("tier", 1, RulesLoader.MaxTier, 1);
        List<string>? effects = f.OptStrList("effects");   // techs.toml [[effect]] ids; checked by RulesLoader once effects are parsed
        if (faction is null || cost is null || buildTime is null || prerequisites is null || effect is null || tier is null || effects is null)
        {
            return null;
        }
        refs.AddAll(f, "prerequisites", prerequisites);
        return new UpgradeType(f.Id, faction.Value, cost.Value, buildTime.Value, prerequisites, effect) { Tier = tier.Value, Effects = effects };
    }
}
