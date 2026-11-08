// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// The tooltip text of a build cameo (M11-10g, audit P4): name, a "cost, time, power" line and a one-line description. Pure, so it is
/// unit-tested. The time is the sim's own build time (<see cref="SimRules.BuildTicks"/>) in whole seconds; power is shown for buildings
/// that make or use any (positive makes, negative uses). Cameos have no hotkeys of their own (the tab letters are on the tabs), so none is shown.
/// </summary>
public static class CameoTooltip
{
    /// <summary>The first lines: name, "Cost .. Time .. Power ..", and the description when one is known.</summary>
    /// <param name="rules">The rules.</param>
    /// <param name="category">The cameo's production category.</param>
    /// <param name="typeIndex">The type index in that category's table.</param>
    /// <param name="cost">The cost in credits.</param>
    /// <param name="blurb">Looks up the one-line description by rules id (the archive subtitle), or null.</param>
    public static List<string> Head(SimRules rules, ProductionCategory category, int typeIndex, int cost, Func<string, string?>? blurb)
    {
        string id = IdOf(rules, category, typeIndex);
        int buildTime = category switch
        {
            ProductionCategory.Infantry when (uint)typeIndex < (uint)rules.Infantry.Count => rules.Infantry[typeIndex].BuildTime,
            ProductionCategory.Vehicle or ProductionCategory.Aircraft when (uint)typeIndex < (uint)rules.Vehicles.Count => rules.Vehicles[typeIndex].BuildTime,
            ProductionCategory.Structure or ProductionCategory.Defence when (uint)typeIndex < (uint)rules.Buildings.Count => rules.Buildings[typeIndex].BuildTime,
            _ => 0,
        };
        int seconds = (SimRules.BuildTicks(cost, buildTime, rules.Economy) + SimConstants.TicksPerSecond - 1) / SimConstants.TicksPerSecond;
        string stats = string.Create(CultureInfo.InvariantCulture, $"Cost {cost}   Time {seconds}s");
        if (category is ProductionCategory.Structure or ProductionCategory.Defence && (uint)typeIndex < (uint)rules.Buildings.Count)
        {
            int power = rules.Buildings[typeIndex].Power;
            if (power != 0) stats += string.Create(CultureInfo.InvariantCulture, $"   Power {(power > 0 ? "+" : "")}{power}");
        }

        var lines = new List<string> { CameoNames.Display(id), stats };
        string? text = blurb?.Invoke(id);
        if (!string.IsNullOrWhiteSpace(text)) lines.Add(text.Trim());
        return lines;
    }

    /// <summary>The click hint line of an unblocked cameo.</summary>
    public static string ClickHint(ProductionCategory category) =>
        category is ProductionCategory.Infantry or ProductionCategory.Vehicle or ProductionCategory.Aircraft
            ? "Left: build (Shift: x5)   Right: cancel"
            : "Left click: build   Right click: cancel";

    private static string IdOf(SimRules rules, ProductionCategory category, int typeIndex) => category switch
    {
        ProductionCategory.Infantry when (uint)typeIndex < (uint)rules.Infantry.Count => rules.Infantry[typeIndex].Id,
        ProductionCategory.Vehicle or ProductionCategory.Aircraft when (uint)typeIndex < (uint)rules.Vehicles.Count => rules.Vehicles[typeIndex].Id,
        _ when (uint)typeIndex < (uint)rules.Buildings.Count => rules.Buildings[typeIndex].Id,
        _ => "",
    };
}
