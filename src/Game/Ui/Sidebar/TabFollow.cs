// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// S47 U-13 (cnc#25): the sidebar tab that follows a selected production building, as in the classic games: click the Barracks and the
/// Infantry tab opens. Pure; the game screen applies it when the setting "Sidebar follows selection" is on.
/// </summary>
public static class TabFollow
{
    /// <summary>The tab that lists what a building with this production category makes, or null for a building that produces nothing.</summary>
    public static SidebarTab? TabFor(ProductionCategory? produces) => produces switch
    {
        ProductionCategory.Structure => SidebarTab.Structures,
        ProductionCategory.Defence => SidebarTab.Defences,
        ProductionCategory.Infantry => SidebarTab.Infantry,
        ProductionCategory.Vehicle => SidebarTab.Vehicles,
        ProductionCategory.Upgrade => SidebarTab.Upgrades,
        ProductionCategory.Aircraft => SidebarTab.Aircraft,
        _ => null,
    };
}
