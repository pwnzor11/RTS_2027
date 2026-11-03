using Rts.Sim.Rules;
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>
/// Parses terrain.toml: <c>[speed.&lt;land&gt;]</c> tables of locomotion to speed percent, and a
/// <c>[buildable]</c> table of land to bool. Every land type and locomotion must be present.
/// </summary>
internal static class TerrainRulesLoader
{
    /// <summary>Largest allowed speed percent (0 = impassable).</summary>
    public const int MaxSpeedPercent = 1000;

    private const string SpeedTable = "speed";
    private const string BuildableTable = "buildable";

    /// <summary>Parses the terrain table. Returns null when any problem was recorded.</summary>
    public static TerrainTable? Parse(TomlTable root, string file, List<RulesProblem> problems)
    {
        int landCount = RuleCounts.LandTypes;
        int locoCount = RuleCounts.Locomotions;
        var speed = new int[landCount * locoCount];
        var buildable = new bool[landCount];
        var speedSeen = new bool[landCount];
        bool ok = true;

        foreach (var kv in root)
        {
            if (kv.Key is not (SpeedTable or BuildableTable))
            {
                problems.Add(new RulesProblem(file, kv.Key, "unknown key"));
                ok = false;
            }
        }

        if (!root.TryGetValue(SpeedTable, out object? rawSpeed) || rawSpeed is not TomlTable speedRoot)
        {
            problems.Add(new RulesProblem(file, SpeedTable, "missing required [speed] tables"));
            ok = false;
        }
        else
        {
            foreach (var kv in speedRoot)
            {
                string key = SpeedTable + "." + kv.Key;
                LandType? land = RulesEnums.ParseLand(kv.Key);
                if (land is null)
                {
                    problems.Add(new RulesProblem(file, key, $"unknown land type; must be one of {RulesEnums.Join(RulesEnums.LandKeys)}"));
                    ok = false;
                    continue;
                }
                if (kv.Value is not TomlTable table)
                {
                    problems.Add(new RulesProblem(file, key, "expected a table of locomotion percents"));
                    ok = false;
                    continue;
                }

                int l = (int)land.Value;
                speedSeen[l] = true;
                var f = new RulesFields(RulesEntry.FromTable(key, file, table), problems);
                f.CheckKeys(RulesEnums.LocomotionKeys);
                for (int loco = 0; loco < locoCount; loco++)
                {
                    int? percent = f.Int(RulesEnums.LocomotionKeys[loco], 0, MaxSpeedPercent);
                    if (percent is null)
                    {
                        ok = false;
                        continue;
                    }
                    speed[l * locoCount + loco] = percent.Value;
                }
            }
        }

        if (!root.TryGetValue(BuildableTable, out object? rawBuildable) || rawBuildable is not TomlTable buildRoot)
        {
            problems.Add(new RulesProblem(file, BuildableTable, "missing required [buildable] table"));
            ok = false;
        }
        else
        {
            var f = new RulesFields(RulesEntry.FromTable(BuildableTable, file, buildRoot), problems);
            f.CheckKeys(RulesEnums.LandKeys);
            foreach (string name in RulesEnums.LandKeys)
            {
                bool? value = f.Bool(name);
                if (value is null)
                {
                    ok = false;
                    continue;
                }
                int l = Array.IndexOf(RulesEnums.LandKeys, name);
                buildable[l] = value.Value;
            }
        }

        for (int l = 0; l < landCount; l++)
        {
            if (!speedSeen[l])
            {
                problems.Add(new RulesProblem(file, SpeedTable + "." + RulesEnums.LandKeys[l], "missing required land type"));
                ok = false;
            }
        }

        return ok ? new TerrainTable(speed, buildable) : null;
    }
}
