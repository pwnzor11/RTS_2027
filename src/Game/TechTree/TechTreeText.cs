// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.TechTree;

/// <summary>The words of the tech-tree viewer (U-12): tooltip lines and names, pure so they are unit-tested. All text comes from <see cref="UiText"/>.</summary>
public static class TechTreeText
{
    /// <summary>The faction's name for the overlay title.</summary>
    public static string FactionName(Faction f) => UiText.Get(f switch
    {
        Faction.Authority => UiText.FactionAuthority,
        Faction.Directorate => UiText.FactionDirectorate,
        Faction.LeftBehind => UiText.FactionLeftBehind,
        Faction.Expedition => UiText.FactionExpedition,
        _ => UiText.FactionAuthority,
    });

    /// <summary>The status word.</summary>
    public static string Status(TechNodeStatus s) => UiText.Get(s switch
    {
        TechNodeStatus.Built => UiText.TechTreeBuilt,
        TechNodeStatus.Available => UiText.TechTreeAvailable,
        TechNodeStatus.MissionLocked => UiText.TechTreeMissionLocked,
        _ => UiText.TechTreeLocked,
    });

    /// <summary>The kind of node (structure, unit, upgrade).</summary>
    public static string Kind(ProductionCategory c) => UiText.Get(c switch
    {
        ProductionCategory.Structure => UiText.TechTreeKindStructure,
        ProductionCategory.Defence => UiText.TechTreeKindDefence,
        ProductionCategory.Infantry => UiText.TechTreeKindInfantry,
        ProductionCategory.Vehicle => UiText.TechTreeKindVehicle,
        ProductionCategory.Aircraft => UiText.TechTreeKindAircraft,
        _ => UiText.TechTreeKindUpgrade,
    });

    /// <summary>Build time as seconds ("12s"), from ticks.</summary>
    public static string Seconds(int ticks) => ((ticks + SimConstants.TicksPerSecond - 1) / SimConstants.TicksPerSecond).ToString(System.Globalization.CultureInfo.InvariantCulture) + "s";

    /// <summary>The tooltip lines of a node: name, kind and tier, status, cost, build time, needs, unlocks.</summary>
    public static IReadOnlyList<string> Lines(TechNode n, TechNodeStatus status, Func<string, string> name)
    {
        var lines = new List<string>
        {
            name(n.Id),
            Kind(n.Category) + ", " + UiText.Format(UiText.TechTreeColumn, "tier", n.Tier.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Status(status),
            UiText.Format(UiText.TechTreeCost, "cost", n.Cost.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            UiText.Format(UiText.TechTreeTime, "time", Seconds(n.BuildTicks)),
            n.Needs.Count == 0 ? UiText.Get(UiText.TechTreeNeedsNothing) : UiText.Format(UiText.TechTreeNeeds, "list", string.Join(", ", n.Needs.Select(name))),
            n.Unlocks.Count == 0 ? UiText.Get(UiText.TechTreeUnlocksNothing) : UiText.Format(UiText.TechTreeUnlocks, "list", string.Join(", ", n.Unlocks.Select(name))),
        };
        return lines;
    }
}
