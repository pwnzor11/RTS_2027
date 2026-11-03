// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>
/// Loads data/ai/difficulty.toml into the five <see cref="DifficultyProfile"/> tiers. Same style as
/// <see cref="RulesLoader"/>: integers only (flags are 0/1), unknown keys are problems, all problems are collected
/// and thrown together as a <see cref="RulesException"/>. All five tiers are required.
/// </summary>
public static class DifficultyLoader
{
    /// <summary>File name inside data/ai.</summary>
    public const string FileName = "difficulty.toml";

    /// <summary>Tier ids in tier order (tier = index + 1).</summary>
    public static readonly IReadOnlyList<string> TierIds = ["recruit", "regular", "veteran", "elite", "commissar"];

    private static readonly string[] Keys =
    [
        "tier", "decision_interval_ticks", "command_delay_ticks", "scout_info_age_ticks", "attack_threshold",
        "focus_fire", "retreat_health_pct", "expansion_tick", "superweapons", "multi_front", "build_repertoire",
    ];

    /// <summary>The data/ai/difficulty.toml beside the nearest data/rules above <paramref name="startDirectory"/>, or null.</summary>
    public static string? FindFile(string startDirectory)
    {
        string? rules = RulesLocator.FindRulesDir(startDirectory);
        return rules is null ? null : PathBesideRules(rules);
    }

    /// <summary>The difficulty file path for a rules directory (data/ai/difficulty.toml next to data/rules).</summary>
    public static string PathBesideRules(string rulesDir) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rulesDir)) ?? ".", "ai", FileName);

    /// <summary>Loads the five tiers from <paramref name="path"/>, ordered by tier.</summary>
    /// <exception cref="RulesException">The file is missing, unparseable or invalid.</exception>
    public static IReadOnlyList<DifficultyProfile> Load(string path)
    {
        string file = FileName;
        if (!File.Exists(path)) throw new RulesException(file, "<file>", "file not found");
        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!;
        }
        catch (TomlException ex)
        {
            throw new RulesException(file, "<parse>", ex.Message);
        }

        var problems = new List<RulesProblem>();
        foreach (var kv in root)
            if (Array.IndexOf(TierIds.ToArray(), kv.Key) < 0)
                problems.Add(new RulesProblem(file, kv.Key, "unknown tier id (expected recruit, regular, veteran, elite or commissar)"));

        var result = new List<DifficultyProfile>();
        for (int t = 0; t < TierIds.Count; t++)
        {
            string id = TierIds[t];
            if (!root.TryGetValue(id, out object? raw)) { problems.Add(new RulesProblem(file, id, "tier is required")); continue; }
            if (raw is not TomlTable table) { problems.Add(new RulesProblem(file, id, "expected a [table]")); continue; }
            DifficultyProfile? p = Parse(id, t + 1, table, file, problems);
            if (p is not null) result.Add(p);
        }

        if (problems.Count > 0) throw new RulesException(problems);
        return result;
    }

    private static DifficultyProfile? Parse(string id, int expectedTier, TomlTable table, string file, List<RulesProblem> problems)
    {
        int before = problems.Count;
        foreach (var kv in table)
            if (Array.IndexOf(Keys, kv.Key) < 0) problems.Add(new RulesProblem(file, id + "." + kv.Key, "unknown key"));

        int? tier = Int(table, id, "tier", 1, 5, file, problems);
        if (tier is int tv && tv != expectedTier) problems.Add(new RulesProblem(file, id + ".tier", $"must be {expectedTier} for '{id}', got {tv}"));
        int? interval = Int(table, id, "decision_interval_ticks", 1, 6000, file, problems);
        int? delay = Int(table, id, "command_delay_ticks", 1, 200, file, problems);
        int? infoAge = Int(table, id, "scout_info_age_ticks", 0, 100000, file, problems);
        int? threshold = Int(table, id, "attack_threshold", 0, 10000, file, problems);
        int? focus = Int(table, id, "focus_fire", 0, 1, file, problems);
        int? retreat = Int(table, id, "retreat_health_pct", 0, 100, file, problems);
        int? expansion = Int(table, id, "expansion_tick", 0, 10000000, file, problems);
        int? supers = Int(table, id, "superweapons", 0, 1, file, problems);
        int? multi = Int(table, id, "multi_front", 0, 1, file, problems);
        List<string>? repertoire = StrList(table, id, "build_repertoire", file, problems);

        if (problems.Count > before) return null;
        return new DifficultyProfile(id, tier!.Value, interval!.Value, delay!.Value, infoAge!.Value, threshold!.Value,
            focus!.Value == 1, retreat!.Value, expansion!.Value, supers!.Value == 1, multi!.Value == 1, repertoire!);
    }

    private static int? Int(TomlTable t, string id, string key, int min, int max, string file, List<RulesProblem> problems)
    {
        if (!t.TryGetValue(key, out object? raw)) { problems.Add(new RulesProblem(file, id + "." + key, "missing required key")); return null; }
        if (raw is not long v) { problems.Add(new RulesProblem(file, id + "." + key, $"expected an integer, got {TypeName(raw)}")); return null; }
        if (v < min || v > max) { problems.Add(new RulesProblem(file, id + "." + key, $"must be between {min} and {max}, got {v}")); return null; }
        return (int)v;
    }

    private static List<string>? StrList(TomlTable t, string id, string key, string file, List<RulesProblem> problems)
    {
        if (!t.TryGetValue(key, out object? raw)) { problems.Add(new RulesProblem(file, id + "." + key, "missing required key")); return null; }
        if (raw is not TomlArray array) { problems.Add(new RulesProblem(file, id + "." + key, $"expected an array, got {TypeName(raw)}")); return null; }
        var list = new List<string>();
        foreach (object? item in array)
        {
            if (item is not string s) { problems.Add(new RulesProblem(file, id + "." + key, "expected an array of strings")); return null; }
            list.Add(s);
        }
        return list;
    }

    private static string TypeName(object? raw) => raw switch
    {
        null => "nothing", bool => "a boolean", double => "a float", string => "a string", TomlTable => "a table", TomlArray => "an array", _ => raw.GetType().Name,
    };
}
