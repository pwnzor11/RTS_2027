// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Sidebar;
using Rts.Game.Screens;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Ui;

public sealed class SidebarLogicTests
{
    private static PlayerHud Hud(int readyStructure = -1, params QueueItemView[] structureItems)
    {
        var queues = new List<QueueView>();
        foreach (ProductionCategory c in new[] { ProductionCategory.Infantry, ProductionCategory.Vehicle, ProductionCategory.Aircraft,
                     ProductionCategory.Structure, ProductionCategory.Defence })
            queues.Add(c == ProductionCategory.Structure
                ? new QueueView(c, structureItems, readyStructure)
                : new QueueView(c, Array.Empty<QueueItemView>(), -1));
        return new PlayerHud(100, 0, 1000, 0, 0, queues, Array.Empty<(ProductionCategory, int)>(), false);
    }

    private static BuildOption Opt(ProductionCategory c, int t, QueueBlockReason r = QueueBlockReason.None) => new(c, t, true, r, 100);

    [Fact]
    public void Tabs_map_to_categories_and_back()
    {
        Assert.Equal(ProductionCategory.Structure, SidebarTabs.CategoryOf(SidebarTab.Structures));
        Assert.Equal(ProductionCategory.Defence, SidebarTabs.CategoryOf(SidebarTab.Defences));
        Assert.Equal(ProductionCategory.Infantry, SidebarTabs.CategoryOf(SidebarTab.Infantry));
        Assert.Equal(ProductionCategory.Vehicle, SidebarTabs.CategoryOf(SidebarTab.Vehicles));
        for (int i = 0; i < SidebarTabs.Count; i++)
            Assert.Equal((SidebarTab)i, SidebarTabs.TabOf(SidebarTabs.CategoryOf((SidebarTab)i)));
        Assert.Null(SidebarTabs.TabOf(ProductionCategory.Aircraft));
        Assert.Equal("QWERT", new string(Enumerable.Range(0, 5).Select(i => SidebarTabs.KeyLetter((SidebarTab)i)).ToArray()));
    }

    [Fact]
    public void Left_click_builds_an_unblocked_option()
    {
        PlayerHud hud = Hud();
        BuildOption o = Opt(ProductionCategory.Vehicle, 2);
        Assert.Equal(CameoClick.Build, SidebarCommands.LeftClick(hud, o));
        Command c = SidebarCommands.Build(0, o)!.Value;
        Assert.Equal(CommandKind.Build, c.Kind);
        Assert.Equal((int)ProductionCategory.Vehicle, c.Arg);
        Assert.Equal(2, c.Arg2);
        Assert.Equal(0, c.Player);
    }

    [Theory]
    [InlineData(QueueBlockReason.QueueFull)]
    [InlineData(QueueBlockReason.NoFactory)]
    [InlineData(QueueBlockReason.StructureWaiting)]
    [InlineData(QueueBlockReason.Defeated)]
    public void Left_click_on_a_blocked_option_does_nothing(QueueBlockReason reason)
    {
        BuildOption o = Opt(ProductionCategory.Structure, 1, reason);
        Assert.Equal(CameoClick.None, SidebarCommands.LeftClick(Hud(), o));
        Assert.Null(SidebarCommands.Build(0, o));
    }

    [Fact]
    public void Left_click_on_the_finished_structure_enters_placement_even_though_the_tab_is_blocked()
    {
        PlayerHud hud = Hud(readyStructure: 1);
        Assert.Equal(CameoClick.EnterPlacement, SidebarCommands.LeftClick(hud, Opt(ProductionCategory.Structure, 1, QueueBlockReason.StructureWaiting)));
        Assert.Equal(CameoClick.None, SidebarCommands.LeftClick(hud, Opt(ProductionCategory.Structure, 2, QueueBlockReason.StructureWaiting)));
        Assert.Equal(1, SidebarCommands.ReadyType(hud, ProductionCategory.Structure));
        Assert.Equal(-1, SidebarCommands.ReadyType(hud, ProductionCategory.Defence));
    }

    [Fact]
    public void Right_click_cancels_the_front_most_queued_item_of_that_type()
    {
        PlayerHud hud = Hud(-1, new QueueItemView(5, 300, false), new QueueItemView(3, 0, false), new QueueItemView(5, 0, false));
        Command c = SidebarCommands.Cancel(0, hud, Opt(ProductionCategory.Structure, 5))!.Value;
        Assert.Equal(CommandKind.Cancel, c.Kind);
        Assert.Equal((int)ProductionCategory.Structure, c.Arg);
        Assert.Equal(0, c.Arg2);
        Assert.Equal(1, SidebarCommands.Cancel(0, hud, Opt(ProductionCategory.Structure, 3))!.Value.Arg2);
        Assert.Null(SidebarCommands.Cancel(0, hud, Opt(ProductionCategory.Structure, 9)));
        Assert.Null(SidebarCommands.Cancel(0, hud, Opt(ProductionCategory.Vehicle, 5)));
    }

    [Fact]
    public void Place_sell_repair_rally_payloads_match_the_command_doc()
    {
        var id = new EntityId(7, 2);
        Command place = SidebarCommands.Place(0, 4, new CellCoord(9, 8));
        Assert.Equal((CommandKind.Place, 4, new CellCoord(9, 8)), (place.Kind, place.Arg, place.Cell));
        Command sell = SidebarCommands.Sell(0, id);
        Assert.Equal((CommandKind.Sell, id), (sell.Kind, sell.Subject));
        Assert.Equal((int)ActorKind.Building, sell.Arg);
        Command repair = SidebarCommands.Repair(0, id);
        Assert.Equal((CommandKind.Repair, id), (repair.Kind, repair.Subject));
        Command rally = SidebarCommands.Rally(0, id, new CellCoord(3, 4));
        Assert.Equal((CommandKind.Rally, id, new CellCoord(3, 4)), (rally.Kind, rally.Subject, rally.Cell));
    }

    [Fact]
    public void Footprint_is_centred_on_the_cursor_cell()
    {
        Assert.Equal(new CellCoord(9, 9), SidebarCommands.TopLeftFor(new CellCoord(10, 10), 3, 3));
        Assert.Equal(new CellCoord(9, 9), SidebarCommands.TopLeftFor(new CellCoord(10, 10), 2, 2));
        Assert.Equal(new CellCoord(10, 10), SidebarCommands.TopLeftFor(new CellCoord(10, 10), 1, 1));
    }

    [Fact]
    public void Every_block_reason_has_its_own_readable_text()
    {
        Assert.Equal("", BlockReasonText.Of(QueueBlockReason.None));
        var reasons = Enum.GetValues<QueueBlockReason>().Where(r => r != QueueBlockReason.None).ToList();
        var texts = reasons.Select(BlockReasonText.Of).ToList();
        Assert.All(texts, t => Assert.False(string.IsNullOrWhiteSpace(t)));
        Assert.Equal(texts.Count, texts.Distinct().Count());
        Assert.Contains("full", BlockReasonText.Of(QueueBlockReason.QueueFull), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("factory", BlockReasonText.Of(QueueBlockReason.NoFactory), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("place", BlockReasonText.Of(QueueBlockReason.StructureWaiting), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("auth_power", "Power Plant")]
    [InlineData("auth_tank", "Medium Tank")]
    [InlineData("auth_big_thing", "Big Thing")]
    [InlineData("plain", "Plain")]
    public void Cameo_names_come_from_the_rules_id(string id, string expected) => Assert.Equal(expected, CameoNames.Display(id));

    [Fact]
    public void Credit_counter_runs_toward_the_target_without_overshooting()
    {
        Assert.Equal(1, SidebarMetrics.CreditStep(0, 1));
        Assert.Equal(100 + 900 / 8, SidebarMetrics.CreditStep(100, 1000));
        Assert.Equal(2000 - 1000 / 8, SidebarMetrics.CreditStep(2000, 1000));
        int shown = 0;
        for (int i = 0; i < 200; i++) shown = SidebarMetrics.CreditStep(shown, 5000);
        Assert.Equal(5000, shown);
        for (int i = 0; i < 200; i++) shown = SidebarMetrics.CreditStep(shown, 40);
        Assert.Equal(40, shown);
        Assert.Equal(7, SidebarMetrics.CreditStep(7, 7));
    }

    [Fact]
    public void Power_bar_scales_to_the_larger_of_output_and_use()
    {
        Assert.Equal((100, 50, false), SidebarMetrics.PowerBar(200, 100, 100));
        (int fill, int use, bool low) = SidebarMetrics.PowerBar(40, 100, 200);
        Assert.Equal((80, 200, true), (fill, use, low));
        (fill, use, low) = SidebarMetrics.PowerBar(0, 0, 200);
        Assert.Equal((0, 0, false), (fill, use, low));
        (fill, use, low) = SidebarMetrics.PowerBar(100, 100, 50);
        Assert.Equal((50, 50, false), (fill, use, low));
    }

    [Theory]
    [InlineData(false, 0, 0, "Victory")]
    [InlineData(true, 1, 0, "Defeat")]
    [InlineData(false, 1, 0, "Defeat")]
    [InlineData(true, -1, 0, "Defeat")]
    [InlineData(false, -1, 0, "Defeat")]
    public void Game_over_headline(bool localDefeated, int winningTeam, int localTeam, string expected) =>
        Assert.Equal(expected, GameOverScreen.Headline(localDefeated, winningTeam, localTeam));
}
