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
            // M4-01: weapon:<id> / warhead:<id> targets are checked by ValidateTargets once weapons and warheads are known.
            if (!target.StartsWith(WeaponPrefix, StringComparison.Ordinal) && !target.StartsWith(WarheadPrefix, StringComparison.Ordinal))
                refs.Add(f, "target", target);
            effects.Add(new EffectDef(id, op, target, attr, value.Value));
        }
        return effects;
    }

    /// <summary>Prefix of a weapon effect target (schema v4, M4-01).</summary>
    public const string WeaponPrefix = "weapon:";

    /// <summary>Prefix of a warhead effect target (schema v4, M4-01).</summary>
    public const string WarheadPrefix = "warhead:";

    /// <summary>Attributes an effect may change on a vehicle or infantry type (design 08 §4).</summary>
    public static readonly string[] UnitAttrs = ["max_health", "speed", "sight", "cost", "build_time", "rad_resist_permille",
        "vet_threshold_permille"];

    /// <summary>Attributes an effect may change on a building type.</summary>
    public static readonly string[] BuildingAttrs = ["max_health", "sight", "power", "cost", "ticks_per_point", "superweapon_charge_ticks"];

    /// <summary>Attributes an effect may change on a weapon.</summary>
    public static readonly string[] WeaponAttrs = ["damage", "range", "rate_of_fire"];

    /// <summary>Attributes an effect may change on a warhead.</summary>
    public static readonly string[] WarheadAttrs = ["contaminate", "blackout_ticks"];

    /// <summary>
    /// Schema v4 (M4-01): checks every effect's target kind and attribute. <c>weapon:&lt;id&gt;</c> / <c>warhead:&lt;id&gt;</c> must name a
    /// weapon / warhead; the attribute must be in the whitelist of the target's kind (a type id or provides name of a building, a
    /// vehicle/infantry id, a class). The reserved op <c>enable</c> takes no attribute and is not checked. Unknown attribute = problem.
    /// Other target names were already checked as references.
    /// </summary>
    public static void ValidateTargets(IReadOnlyList<EffectDef> effects, SimRulesTables t, IReadOnlySet<string> provides,
        List<RulesProblem> problems)
    {
        foreach (EffectDef e in effects)
        {
            string[]? allowed;
            if (e.Target.StartsWith(WeaponPrefix, StringComparison.Ordinal))
            {
                string id = e.Target[WeaponPrefix.Length..];
                if (Rts.Sim.Rules.SimRules.IndexOf(t.Weapons, id, w => w.Id) < 0)
                {
                    problems.Add(new RulesProblem(RulesLoader.TechsFile, e.Id + ".target", $"unknown weapon '{id}'"));
                    continue;
                }
                allowed = WeaponAttrs;
            }
            else if (e.Target.StartsWith(WarheadPrefix, StringComparison.Ordinal))
            {
                string id = e.Target[WarheadPrefix.Length..];
                if (Rts.Sim.Rules.SimRules.IndexOf(t.Warheads, id, w => w.Id) < 0)
                {
                    problems.Add(new RulesProblem(RulesLoader.TechsFile, e.Id + ".target", $"unknown warhead '{id}'"));
                    continue;
                }
                allowed = WarheadAttrs;
            }
            else if (e.Target == RulesEnums.ClassPrefix + "building" || provides.Contains(e.Target)
                || Rts.Sim.Rules.SimRules.IndexOf(t.Buildings, e.Target, b => b.Id) >= 0)
            {
                allowed = BuildingAttrs;
            }
            else if (e.Target is "class:vehicle" or "class:infantry"
                || Rts.Sim.Rules.SimRules.IndexOf(t.Vehicles, e.Target, v => v.Id) >= 0
                || Rts.Sim.Rules.SimRules.IndexOf(t.Infantry, e.Target, i => i.Id) >= 0)
            {
                allowed = UnitAttrs;
            }
            else
            {
                allowed = null;   // an upgrade id or an unresolved name (already reported as a reference)
            }
            if (e.Op == "enable" || allowed is null) continue;
            if (Array.IndexOf(allowed, e.Attr) < 0)
                problems.Add(new RulesProblem(RulesLoader.TechsFile, e.Id + ".attr",
                    $"unknown attribute '{e.Attr}' for target '{e.Target}'; must be one of {RulesEnums.Join(allowed)}"));
        }
    }
}
