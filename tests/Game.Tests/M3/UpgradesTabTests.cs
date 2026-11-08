// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M3;

/// <summary>M3-08: the Upgrades tab lists the right entries and issues Build / Cancel with Arg = Upgrade.</summary>
public sealed class UpgradesTabTests
{
    private static readonly IReadOnlyList<UpgradeType> Upgrades = new[]
    {
        new UpgradeType("auth_grouting", Faction.Authority, 800, 0, Array.Empty<string>(), "grouting"),
        new UpgradeType("auth_hardened_armour", Faction.Authority, 600, 0, Array.Empty<string>(), "armour"),
        new UpgradeType("auth_zeal", Faction.Authority, 500, 0, new[] { "auth_grouting" }, "zeal"),
        new UpgradeType("dir_static", Faction.Directorate, 700, 0, Array.Empty<string>(), "static"),
    };

    private static PlayerHud Hud(ulong owned, int[] buildable, params QueueView[] queues) =>
        new(1000, 0, 2000, 100, 50, queues, Array.Empty<(ProductionCategory, int)>(), false)
        {
            Upgrades = owned,
            BuildableUpgrades = buildable,
        };

    private static QueueView UpgradeQueue(params QueueItemView[] items) => new(ProductionCategory.Upgrade, items, -1);

    [Fact]
    public void The_fifth_tab_is_Upgrades_on_key_T_and_maps_to_the_Upgrade_category()
    {
        Assert.Equal(5, SidebarTabs.Count);
        Assert.Equal(ProductionCategory.Upgrade, SidebarTabs.CategoryOf(SidebarTab.Upgrades));
        Assert.Equal(SidebarTab.Upgrades, SidebarTabs.TabOf(ProductionCategory.Upgrade));
        Assert.Equal('T', SidebarTabs.KeyLetter(SidebarTab.Upgrades));
        Assert.Equal("UPG", SidebarTabs.Caption(SidebarTab.Upgrades));
    }

    [Fact]
    public void Lists_buildable_researching_and_owned_upgrades_and_nothing_else()
    {
        // index 0 owned, 1 buildable, 2 queued (45% done), 3 is another faction's and not buildable.
        PlayerHud hud = Hud(owned: 1UL << 0, buildable: new[] { 1 }, UpgradeQueue(new QueueItemView(2, 450, false)));
        IReadOnlyList<UpgradeEntry> e = UpgradeList.Entries(hud, Upgrades);

        Assert.Equal(3, e.Count);
        Assert.Equal(new UpgradeEntry(0, "auth_grouting", 800, UpgradeState.Owned, 0), e[0]);
        Assert.Equal(new UpgradeEntry(1, "auth_hardened_armour", 600, UpgradeState.Available, 0), e[1]);
        Assert.Equal(new UpgradeEntry(2, "auth_zeal", 500, UpgradeState.Researching, 450), e[2]);
    }

    [Fact]
    public void An_upgrade_that_is_both_queued_and_still_buildable_shows_as_researching()
    {
        PlayerHud hud = Hud(0, new[] { 1 }, UpgradeQueue(new QueueItemView(1, 10, false)));
        Assert.Equal(UpgradeState.Researching, Assert.Single(UpgradeList.Entries(hud, Upgrades)).State);
    }

    [Fact]
    public void Nothing_is_listed_when_nothing_is_available()
    {
        Assert.Empty(UpgradeList.Entries(Hud(0, Array.Empty<int>()), Upgrades));
    }

    [Fact]
    public void Left_click_on_an_available_upgrade_is_a_Build_in_the_Upgrade_category_with_the_index_in_Arg2()
    {
        PlayerHud hud = Hud(0, new[] { 1, 2 });
        UpgradeEntry entry = UpgradeList.Entries(hud, Upgrades)[1];   // index 2

        Command? c = UpgradeList.LeftClick(0, entry);

        Assert.NotNull(c);
        Assert.Equal(CommandKind.Build, c!.Value.Kind);
        Assert.Equal((int)ProductionCategory.Upgrade, c.Value.Arg);
        Assert.Equal(2, c.Value.Arg2);
        Assert.Equal((byte)0, c.Value.Player);
        Assert.Equal(0, c.Value.ExecTick);   // SimHost.Issue stamps it
    }

    [Fact]
    public void Left_click_on_an_owned_or_queued_upgrade_issues_nothing()
    {
        PlayerHud hud = Hud(1UL << 0, new[] { 1 }, UpgradeQueue(new QueueItemView(1, 0, false)));
        foreach (UpgradeEntry e in UpgradeList.Entries(hud, Upgrades)) Assert.Null(UpgradeList.LeftClick(0, e));
    }

    [Fact]
    public void Right_click_on_a_queued_upgrade_cancels_it_by_its_queue_position()
    {
        // Two upgrades queued: index 1 at the front, index 2 behind it.
        PlayerHud hud = Hud(0, Array.Empty<int>(), UpgradeQueue(new QueueItemView(1, 300, false), new QueueItemView(2, 0, false)));
        UpgradeEntry second = UpgradeList.Entries(hud, Upgrades).Single(x => x.Index == 2);

        Command? c = UpgradeList.RightClick(0, hud, second);

        Assert.NotNull(c);
        Assert.Equal(CommandKind.Cancel, c!.Value.Kind);
        Assert.Equal((int)ProductionCategory.Upgrade, c.Value.Arg);
        Assert.Equal(1, c.Value.Arg2);   // position 1
    }

    [Fact]
    public void Right_click_on_an_available_or_owned_upgrade_cancels_nothing()
    {
        PlayerHud hud = Hud(1UL << 0, new[] { 1 });
        foreach (UpgradeEntry e in UpgradeList.Entries(hud, Upgrades)) Assert.Null(UpgradeList.RightClick(0, hud, e));
    }

    [Fact]
    public void Cancel_of_an_upgrade_that_is_not_queued_is_null()
    {
        Assert.Null(SidebarCommands.CancelUpgrade(0, Hud(0, new[] { 1 }), 1));
    }

    [Fact]
    public void BuildUpgrade_uses_the_acting_player()
    {
        Command c = SidebarCommands.BuildUpgrade(1, 3);
        Assert.Equal((byte)1, c.Player);
        Assert.Equal(3, c.Arg2);
        Assert.True(c.Subject.IsNone);
    }

    [Fact]
    public void Upgrade_right_click_holds_then_cancels_and_left_click_resumes()
    {
        PlayerHud running = Hud(0, Array.Empty<int>(), UpgradeQueue(new QueueItemView(1, 300, false)));
        UpgradeEntry e = Assert.Single(UpgradeList.Entries(running, Upgrades));
        Command hold = UpgradeList.RightClick(0, running, e)!.Value;
        Assert.Equal((CommandKind.Hold, (int)ProductionCategory.Upgrade, 1), (hold.Kind, hold.Arg, hold.Arg2));
        Assert.Null(UpgradeList.LeftClick(0, running, e));

        PlayerHud held = Hud(0, Array.Empty<int>(), UpgradeQueue(new QueueItemView(1, 300, false, true)));
        UpgradeEntry he = Assert.Single(UpgradeList.Entries(held, Upgrades));
        Assert.Equal(CommandKind.Cancel, UpgradeList.RightClick(0, held, he)!.Value.Kind);
        Command resume = UpgradeList.LeftClick(0, held, he)!.Value;
        Assert.Equal((CommandKind.Hold, 0), (resume.Kind, resume.Arg2));
    }
}
