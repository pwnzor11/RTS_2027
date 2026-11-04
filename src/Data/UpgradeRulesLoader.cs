// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>Parses upgrade entries (upgrades.toml, M3): <c>[&lt;id&gt;]</c> with faction (default from the id prefix), cost,
/// build_time (optional, 0 = from cost), prerequisites (optional; type ids, provides names or upgrade ids) and effect (optional
/// string key, default "").</summary>
internal static class UpgradeRulesLoader
{
    private static readonly string[] UpgradeKeys = ["faction", "cost", "build_time", "prerequisites", "effect"];

    /// <summary>Parses one upgrade.</summary>
    public static UpgradeType? ParseUpgrade(RulesFields f, RulesReferences refs)
    {
        f.CheckKeys(UpgradeKeys);
        Faction? faction = f.FactionOrDefault();
        int? cost = f.Int("cost", 0, int.MaxValue);
        int? buildTime = f.OptInt("build_time", 0, int.MaxValue, 0);
        List<string>? prerequisites = f.OptStrList("prerequisites");
        string? effect = f.OptStr("effect", "");
        if (faction is null || cost is null || buildTime is null || prerequisites is null || effect is null)
        {
            return null;
        }
        refs.AddAll(f, "prerequisites", prerequisites);
        return new UpgradeType(f.Id, faction.Value, cost.Value, buildTime.Value, prerequisites, effect);
    }
}
