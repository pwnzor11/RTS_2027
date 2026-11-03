// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Collections;
using Rts.Sim.Rules;
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>Parses the <c>[[effect]]</c> entries in techs.toml.</summary>
internal static class EffectRulesLoader
{
    private const string EffectArray = "effect";
    private static readonly string[] EffectKeys = ["id", "op", "target", "attr", "value"];

    /// <summary>
    /// Parses every effect. <paramref name="ids"/> holds the type ids already registered (id to file); effect ids
    /// are added to it and checked for duplicates.
    /// </summary>
    public static List<EffectDef> Parse(TomlTable root, string file, Dictionary<string, string> ids,
        RulesReferences refs, List<RulesProblem> problems)
    {
        var effects = new List<EffectDef>();
        foreach (var kv in root)
        {
            if (kv.Key != EffectArray)
            {
                problems.Add(new RulesProblem(file, kv.Key, "unknown key"));
            }
        }
        if (!root.TryGetValue(EffectArray, out object? raw) || raw is null)
        {
            return effects;
        }
        if (raw is not IEnumerable items || raw is string || raw is TomlTable)
        {
            problems.Add(new RulesProblem(file, EffectArray, "expected [[effect]] entries"));
            return effects;
        }

        foreach (object? item in items)
        {
            if (item is not TomlTable table)
            {
                problems.Add(new RulesProblem(file, EffectArray, "expected a [[effect]] table"));
                continue;
            }
            if (!table.TryGetValue("id", out object? rawId) || rawId is not string id || id.Length == 0)
            {
                problems.Add(new RulesProblem(file, EffectArray, "each [[effect]] needs a non-empty string id"));
                continue;
            }
            if (ids.TryGetValue(id, out string? other))
            {
                problems.Add(new RulesProblem(file, id, $"duplicate id, already defined in {other}"));
                continue;
            }
            ids[id] = file;

            var f = new RulesFields(RulesEntry.FromTable(id, file, table), problems);
            f.CheckKeys(EffectKeys);
            string? op = f.OneOf("op", RulesEnums.EffectOps);
            string? target = f.Str("target");
            string? attr = f.Str("attr");
            int? value = f.Int("value", int.MinValue, int.MaxValue);
            if (op is null || target is null || attr is null || value is null)
            {
                continue;
            }
            refs.Add(f, "target", target);
            effects.Add(new EffectDef(id, op, target, attr, value.Value));
        }
        return effects;
    }
}
