// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13 (cnc#25): the sidebar tab that follows a selected production building, and its setting.</summary>
public sealed class S47QolTabFollowTests
{
    [Theory]
    [InlineData(ProductionCategory.Infantry, SidebarTab.Infantry)]
    [InlineData(ProductionCategory.Vehicle, SidebarTab.Vehicles)]
    [InlineData(ProductionCategory.Aircraft, SidebarTab.Aircraft)]
    [InlineData(ProductionCategory.Structure, SidebarTab.Structures)]
    [InlineData(ProductionCategory.Defence, SidebarTab.Defences)]
    [InlineData(ProductionCategory.Upgrade, SidebarTab.Upgrades)]
    public void A_producing_building_opens_the_tab_of_its_category(ProductionCategory category, SidebarTab tab)
    {
        Assert.Equal(tab, TabFollow.TabFor(category));
    }

    [Fact]
    public void A_building_that_produces_nothing_leaves_the_tab_alone()
    {
        Assert.Null(TabFollow.TabFor(null));
    }

    [Fact]
    public void The_setting_is_on_by_default_and_round_trips()
    {
        Assert.True(Settings.Default.SidebarFollowsSelection);
        Settings off = Settings.Default with { SidebarFollowsSelection = false };
        Assert.False(Settings.Parse(off.ToToml()).SidebarFollowsSelection);
        Assert.True(Settings.Parse(Settings.Default.ToToml()).SidebarFollowsSelection);
    }
}
