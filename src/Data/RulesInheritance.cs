// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>
/// Resolves <c>base = "id"</c>. Inheritance is one level and stays inside one file: a derived entry starts from
/// its base's values and overrides them. Missing, derived and cross-file bases are reported.
/// </summary>
internal static class RulesInheritance
{
    /// <summary>Key that names the base entry.</summary>
    public const string BaseKey = "base";

    /// <summary>Resolves every table in <paramref name="root"/> to a <see cref="RulesEntry"/>.</summary>
    /// <param name="idFiles">Type id to the file that defines it, across all files (used to explain cross-file bases).</param>
    public static List<RulesEntry> Resolve(string file, TomlTable root, IReadOnlyDictionary<string, string> idFiles,
        List<RulesProblem> problems)
    {
        var entries = new List<RulesEntry>();
        foreach (var kv in root)
        {
            if (kv.Value is not TomlTable table)
            {
                problems.Add(new RulesProblem(file, kv.Key, "expected a [table] for each entry"));
                continue;
            }

            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (table.TryGetValue(BaseKey, out object? rawBase))
            {
                if (!TryFindBase(file, kv.Key, root, idFiles, rawBase, problems, out TomlTable? baseTable))
                {
                    continue;
                }
                foreach (var inherited in baseTable!)
                {
                    values[inherited.Key] = inherited.Value;
                }
            }

            var own = new List<string>();
            foreach (var field in table)
            {
                if (field.Key == BaseKey)
                {
                    continue;
                }
                values[field.Key] = field.Value;
                own.Add(field.Key);
            }
            entries.Add(new RulesEntry(kv.Key, file, values, own));
        }
        return entries;
    }

    private static bool TryFindBase(string file, string id, TomlTable root, IReadOnlyDictionary<string, string> idFiles,
        object? rawBase, List<RulesProblem> problems, out TomlTable? baseTable)
    {
        baseTable = null;
        string key = id + "." + BaseKey;
        if (rawBase is not string baseId)
        {
            problems.Add(new RulesProblem(file, key, "expected a string id"));
            return false;
        }
        if (root.TryGetValue(baseId, out object? found) && found is TomlTable table)
        {
            if (table.TryGetValue(BaseKey, out _))
            {
                problems.Add(new RulesProblem(file, key, $"base '{baseId}' is itself derived; inheritance is one level only"));
                return false;
            }
            baseTable = table;
            return true;
        }
        if (idFiles.TryGetValue(baseId, out string? other) && other != file)
        {
            problems.Add(new RulesProblem(file, key, $"base '{baseId}' is defined in {other}; a base must be in the same file"));
        }
        else
        {
            problems.Add(new RulesProblem(file, key, $"base '{baseId}' not found in {file}"));
        }
        return false;
    }
}
