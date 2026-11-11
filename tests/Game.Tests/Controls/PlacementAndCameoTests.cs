// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Game.Tests.Controls;

/// <summary>M11-10f and M11-10g: placement reasons, per-cell causes, cameo tooltip text and Shift-queue counts.</summary>
public sealed class PlacementAndCameoTests
{
    [Theory]
    [InlineData(PlacementResult.Ok, true, CellProblem.None, null)]
    [InlineData(PlacementResult.Ok, false, CellProblem.None, "Too far from your base")]
    [InlineData(PlacementResult.OutOfMap, false, CellProblem.None, "Off the map")]
    [InlineData(PlacementResult.SealsRegion, false, CellProblem.None, "Would seal off an area")]
    [InlineData(PlacementResult.OutsideBeachhead, false, CellProblem.None, "Outside your beachhead")]
    [InlineData(PlacementResult.StoreFull, false, CellProblem.None, "Too many buildings")]
    [InlineData(PlacementResult.Blocked, false, CellProblem.Water, "Not on water")]
    [InlineData(PlacementResult.Blocked, false, CellProblem.Unit, "Blocked by a unit")]
    [InlineData(PlacementResult.Blocked, false, CellProblem.None, "Blocked")]
    public void Reason_text(PlacementResult result, bool inRadius, CellProblem worst, string? expected) =>
        Assert.Equal(expected, PlacementCheck.Reason(result, inRadius, worst));

    [Fact]
    public void Cell_causes_follow_the_sim_rules()
    {
        Assert.Equal(CellProblem.None, PlacementCheck.Cell(false, false, true, false, false));
        Assert.Equal(CellProblem.Water, PlacementCheck.Cell(false, true, false, false, false));
        Assert.Equal(CellProblem.Terrain, PlacementCheck.Cell(false, false, false, false, false));
        Assert.Equal(CellProblem.Building, PlacementCheck.Cell(false, false, false, true, false));
        Assert.Equal(CellProblem.Unit, PlacementCheck.Cell(false, false, true, false, true));
        Assert.Equal(CellProblem.NeedsWater, PlacementCheck.Cell(true, false, true, false, false));
        Assert.Equal(CellProblem.None, PlacementCheck.Cell(true, true, false, false, false));
    }

    [Fact]
    public void Worst_names_the_most_permanent_cause() =>
        Assert.Equal(CellProblem.Terrain, PlacementCheck.Worst(new[] { CellProblem.Unit, CellProblem.None, CellProblem.Terrain, CellProblem.Building }));

    [Fact]
    public void Shift_queues_five_units_but_one_structure()
    {
        Assert.Equal(5, SidebarCommands.QueueCount(ProductionCategory.Infantry, true));
        Assert.Equal(5, SidebarCommands.QueueCount(ProductionCategory.Vehicle, true));
        Assert.Equal(1, SidebarCommands.QueueCount(ProductionCategory.Vehicle, false));
        Assert.Equal(1, SidebarCommands.QueueCount(ProductionCategory.Structure, true));
    }

    [Fact]
    public void Tooltip_head_has_name_cost_time_power_and_the_description()
    {
        string dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        SimRules rules = RulesLoader.Load(dir).Sim;
        int power = SimRules.IndexOf(rules.Buildings, "auth_power", b => b.Id);
        Assert.True(power >= 0);
        List<string> lines = CameoTooltip.Head(rules, ProductionCategory.Structure, power, rules.Buildings[power].Cost, id => id == "auth_power" ? "Reactor and turbine hall" : null);
        Assert.Equal("Power Plant", lines[0]);
        Assert.Matches(@"^Cost \d+   Time \d+s   Power \+\d+$", lines[1]);
        Assert.Equal("Reactor and turbine hall", lines[2]);
        Assert.Equal(2, CameoTooltip.Head(rules, ProductionCategory.Structure, power, 10, null).Count);
    }

    [Fact]
    public void Archive_has_a_subtitle_for_the_authority_power_plant()
    {
        string dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        var catalog = Rts.Game.Archive.ArchiveCatalog.Load(Rts.Game.Archive.ArchiveCatalog.DirectoryFor(dir));
        Assert.False(string.IsNullOrWhiteSpace(catalog.Find("auth_power")?.Subtitle));
    }
}
