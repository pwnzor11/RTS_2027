// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>The sidebar tabs, in order; the keys Q, W, E, R and T select the first five (T is the M3 Upgrades tab) and Y the
/// M4 Aircraft tab, which is only shown while the player can build aircraft.</summary>
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
    /// <summary>Aircraft (Y), M4: shown only when the player has an unlocked aircraft type.</summary>
    Aircraft = 5,
}

/// <summary>Tab helpers: the production category a tab lists and its caption.</summary>
public static class SidebarTabs
{
    /// <summary>Number of always-present tabs (the Aircraft tab, index 5, is extra and conditional).</summary>
    public const int Count = 5;

    /// <summary>Number of tabs including the conditional Aircraft tab.</summary>
    public const int CountWithAircraft = 6;

    /// <summary>Tabs shown: <see cref="Count"/>, or <see cref="CountWithAircraft"/> when the player can build aircraft.</summary>
    public static int CountFor(bool hasAircraft) => hasAircraft ? CountWithAircraft : Count;

    /// <summary>The production category whose options a tab lists.</summary>
    public static ProductionCategory CategoryOf(SidebarTab tab) => tab switch
    {
        SidebarTab.Structures => ProductionCategory.Structure,
        SidebarTab.Defences => ProductionCategory.Defence,
        SidebarTab.Infantry => ProductionCategory.Infantry,
        SidebarTab.Upgrades => ProductionCategory.Upgrade,
        SidebarTab.Aircraft => ProductionCategory.Aircraft,
        _ => ProductionCategory.Vehicle,
    };

    /// <summary>The tab that lists a category (every category has one since M4).</summary>
    public static SidebarTab? TabOf(ProductionCategory category) => category switch
    {
        ProductionCategory.Structure => SidebarTab.Structures,
        ProductionCategory.Defence => SidebarTab.Defences,
        ProductionCategory.Infantry => SidebarTab.Infantry,
        ProductionCategory.Vehicle => SidebarTab.Vehicles,
        ProductionCategory.Upgrade => SidebarTab.Upgrades,
        ProductionCategory.Aircraft => SidebarTab.Aircraft,
        _ => null,
    };

    /// <summary>Three-letter caption, sized to fit a narrow tab.</summary>
    public static string Caption(SidebarTab tab) => tab switch
    {
        SidebarTab.Structures => "STR",
        SidebarTab.Defences => "DEF",
        SidebarTab.Infantry => "INF",
        SidebarTab.Upgrades => "UPG",
        SidebarTab.Aircraft => "AIR",
        _ => "VEH",
    };

    /// <summary>Full tab name for tooltips.</summary>
    public static string Title(SidebarTab tab) => tab switch
    {
        SidebarTab.Structures => "Structures",
        SidebarTab.Defences => "Defences",
        SidebarTab.Infantry => "Infantry",
        SidebarTab.Upgrades => "Upgrades",
        SidebarTab.Aircraft => "Aircraft",
        _ => "Vehicles",
    };

    /// <summary>The default hot key letter of the tab (Q W E R T Y). The UI shows <see cref="KeyName"/>, which follows rebinding.</summary>
    public static char KeyLetter(SidebarTab tab) => "QWERTY"[(int)tab];

    /// <summary>The name of the key currently bound to the tab (S18 GAMEUI-03: read from the bindings, not hard-coded).</summary>
    public static string KeyName(SidebarTab tab) => Rts.Game.Controls.KeyNames.Of(Rts.Game.Controls.GameKeys.Active.Get(tab switch
    {
        SidebarTab.Structures => Rts.Game.Controls.GameAction.TabStructures,
        SidebarTab.Defences => Rts.Game.Controls.GameAction.TabDefences,
        SidebarTab.Infantry => Rts.Game.Controls.GameAction.TabInfantry,
        SidebarTab.Vehicles => Rts.Game.Controls.GameAction.TabVehicles,
        SidebarTab.Upgrades => Rts.Game.Controls.GameAction.TabUpgrades,
        _ => Rts.Game.Controls.GameAction.TabAircraft,
    }));
}
