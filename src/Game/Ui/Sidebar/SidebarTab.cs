// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>The five sidebar tabs, in order; the keys Q, W, E, R and T select them (T is the M3 Upgrades tab).</summary>
public enum SidebarTab
{
    /// <summary>Structures (Q).</summary>
    Structures = 0,
    /// <summary>Defences (W).</summary>
    Defences = 1,
    /// <summary>Infantry (E).</summary>
    Infantry = 2,
    /// <summary>Vehicles (R).</summary>
    Vehicles = 3,
    /// <summary>Upgrades (T), M3: research at a building that can.</summary>
    Upgrades = 4,
}

/// <summary>Tab helpers: the production category a tab lists and its caption.</summary>
public static class SidebarTabs
{
    /// <summary>Number of tabs.</summary>
    public const int Count = 5;

    /// <summary>The production category whose options a tab lists.</summary>
    public static ProductionCategory CategoryOf(SidebarTab tab) => tab switch
    {
        SidebarTab.Structures => ProductionCategory.Structure,
        SidebarTab.Defences => ProductionCategory.Defence,
        SidebarTab.Infantry => ProductionCategory.Infantry,
        SidebarTab.Upgrades => ProductionCategory.Upgrade,
        _ => ProductionCategory.Vehicle,
    };

    /// <summary>The tab that lists a category, or null for categories with no tab (aircraft).</summary>
    public static SidebarTab? TabOf(ProductionCategory category) => category switch
    {
        ProductionCategory.Structure => SidebarTab.Structures,
        ProductionCategory.Defence => SidebarTab.Defences,
        ProductionCategory.Infantry => SidebarTab.Infantry,
        ProductionCategory.Vehicle => SidebarTab.Vehicles,
        ProductionCategory.Upgrade => SidebarTab.Upgrades,
        _ => null,
    };

    /// <summary>Three-letter caption, sized to fit a narrow tab.</summary>
    public static string Caption(SidebarTab tab) => tab switch
    {
        SidebarTab.Structures => "STR",
        SidebarTab.Defences => "DEF",
        SidebarTab.Infantry => "INF",
        SidebarTab.Upgrades => "UPG",
        _ => "VEH",
    };

    /// <summary>Full tab name for tooltips.</summary>
    public static string Title(SidebarTab tab) => tab switch
    {
        SidebarTab.Structures => "Structures",
        SidebarTab.Defences => "Defences",
        SidebarTab.Infantry => "Infantry",
        SidebarTab.Upgrades => "Upgrades",
        _ => "Vehicles",
    };

    /// <summary>The hot key letter shown on the tab.</summary>
    public static char KeyLetter(SidebarTab tab) => "QWERT"[(int)tab];
}
