// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>Plain-words effect lines for an upgrade tooltip, from the upgrade's <see cref="UpgradeType.Effects"/> (pure, unit-tested).</summary>
public static class UpgradeEffectText
{
    /// <summary>One line per effect id the upgrade lists (in list order); ids the rules do not define are skipped. An upgrade with
    /// only a special <see cref="UpgradeType.Effect"/> key and no effect list gets that key as one line.</summary>
    public static IReadOnlyList<string> Lines(SimRules rules, UpgradeType upgrade)
    {
        var lines = new List<string>();
        foreach (string id in upgrade.Effects)
        {
            foreach (EffectDef e in rules.Effects)
            {
                if (string.Equals(e.Id, id, StringComparison.Ordinal)) { lines.Add(Describe(e)); break; }
            }
        }

        if (lines.Count == 0 && !string.IsNullOrEmpty(upgrade.Effect) && !string.Equals(upgrade.Effect, "none", StringComparison.OrdinalIgnoreCase))
            lines.Add(CameoNames.Display("x_" + upgrade.Effect));
        return lines;
    }

    /// <summary>One effect as text, e.g. "Infantry: max health x1.15" or "Sunrise: charge time -25%".</summary>
    public static string Describe(EffectDef e)
    {
        string target = TargetText(e.Target);
        string attr = e.Attr.Replace('_', ' ');
        string change = e.Op switch
        {
            "mul" => "x" + Permille(e.Value),
            "add" => (e.Value >= 0 ? "+" : "") + e.Value.ToString(CultureInfo.InvariantCulture),
            "set" => "set to " + e.Value.ToString(CultureInfo.InvariantCulture),
            _ => e.Op + " " + e.Value.ToString(CultureInfo.InvariantCulture),
        };
        return $"{target}: {attr} {change}";
    }

    /// <summary>Per mille as a decimal factor with trailing zeros dropped: 1150 gives "1.15", 800 gives "0.8", 2000 gives "2".</summary>
    public static string Permille(int value)
    {
        int abs = Math.Abs(value);
        string text = (abs / 1000).ToString(CultureInfo.InvariantCulture);
        int frac = abs % 1000;
        if (frac != 0) text += "." + frac.ToString("000", CultureInfo.InvariantCulture).TrimEnd('0');
        return value < 0 ? "-" + text : text;
    }

    private static string TargetText(string target)
    {
        if (target.StartsWith("class:", StringComparison.Ordinal))
            return target[6..] switch { "infantry" => "All infantry", "vehicle" => "All vehicles", "building" => "All buildings", "player" => "All players", var c => "All " + c };
        if (target.StartsWith("weapon:", StringComparison.Ordinal)) return CameoNames.Display(target[7..]) + " weapon";
        if (target.StartsWith("warhead:", StringComparison.Ordinal)) return CameoNames.Display(target[8..]) + " warhead";
        return CameoNames.Display(target);
    }
}
