// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Rules;

/// <summary>Rules schema v2 (M2-02): optional building/unit keys, the [economy] table, hash coverage.</summary>
public sealed class SchemaV2Tests
{
    private static RuleSet Load() => RulesLoader.Load(TestData.RulesDir);

    private static BuildingType Building(RuleSet r, string id) => r.Sim.Buildings.Single(b => b.Id == id);

    [Fact]
    public void FixturesLoad_WithNewKeys()
    {
        RuleSet r = Load();
        BuildingType refinery = Building(r, "test_refinery");
        Assert.True(refinery.Refinery);
        Assert.Equal(1000, refinery.Storage);
        Assert.Equal(new CellCoord(1, 3), refinery.Dock);
        Assert.Equal(new CellCoord(1, 3), refinery.ExitOrDefault()); // 3x3 default: (3/2, 3)
        Assert.Equal("test_harvester", r.Sim.Vehicles[refinery.FreeUnit].Id);
        Assert.Equal(20, r.Sim.Vehicles.Single(v => v.Id == "test_harvester").HarvestCapacity);
        Assert.Equal(1500, Building(r, "test_silo").Storage);
        BuildingType wall = Building(r, "test_wall");
        Assert.True(wall.Wall);
        Assert.Equal(ProductionCategory.Defence, wall.Category);
        BuildingType turret = Building(r, "test_turret");
        Assert.True(turret.NeedsPower);
        Assert.Equal(ProductionCategory.Defence, turret.Category);
        Assert.Equal(ProductionCategory.Infantry, Building(r, "test_barracks").Produces);
        Assert.Equal(ProductionCategory.Vehicle, Building(r, "test_factory").Produces);
        Assert.Equal(100, Building(r, "test_power").Power);
    }

    [Fact]
    public void Defaults_ApplyWhenKeysAbsent()
    {
        BuildingType hq = Building(Load(), "test_hq");
        Assert.Equal(ProductionCategory.Structure, hq.Category);
        Assert.Null(hq.Produces);
        Assert.Equal(0, hq.BuildTime);
        Assert.Equal(0, hq.Storage);
        Assert.False(hq.Refinery);
        Assert.Equal(new CellCoord(0, 0), hq.Dock);
        Assert.Equal(new CellCoord(1, 3), hq.Exit);
        Assert.Equal(-1, hq.FreeUnit);
        Assert.False(hq.Wall);
        Assert.False(hq.NeedsPower);
    }

    [Fact]
    public void ExplicitExitAndBuildTimes_Parse()
    {
        using var temp = new TempRules(
            ("buildings.toml", TempRules.RealWith("buildings.toml", "[test_silo]", "[test_silo]\nexit = [0, -1]\nbuild_time = 77")),
            ("vehicles.toml", TempRules.RealWith("vehicles.toml", "harvest_capacity = 20", "harvest_capacity = 20\nbuild_time = 55")),
            ("infantry.toml", TempRules.RealWith("infantry.toml", "[test_engineer]", "[test_engineer]\nbuild_time = 33")));
        RuleSet r = RulesLoader.Load(temp.Dir);
        BuildingType silo = Building(r, "test_silo");
        Assert.Equal(new CellCoord(0, -1), silo.Exit);
        Assert.Equal(77, silo.BuildTime);
        Assert.Equal(55, r.Sim.Vehicles.Single(v => v.Id == "test_harvester").BuildTime);
        Assert.Equal(33, r.Sim.Infantry.Single(i => i.Id == "test_engineer").BuildTime);
    }

    [Fact]
    public void Economy_ParsesFixtureValues()
    {
        EconomyRules e = Load().Sim.Economy;
        Assert.Equal(5000, e.StartingCash);
        Assert.Equal(60, e.CreditsPitchblende);
        Assert.Equal(new[] { 1500, 2500, 4000 }, e.LowPowerSlowdownPermille.ToArray());
        Assert.Equal(40, SimRules.BuildTicks(200, 0, e));
        Assert.Equal(1, SimRules.BuildTicks(0, 0, e));
        Assert.Equal(9, SimRules.BuildTicks(200, 9, e));
    }

    [Theory]
    [InlineData("buildings.toml", "wall = true", "wall = true\nproduces = \"infantry\"", "test_wall.wall", "must not produce")]
    [InlineData("buildings.toml", "[test_wall]\nfootprint_w = 1\nfootprint_h = 1", "[test_wall]\nfootprint_w = 2\nfootprint_h = 2", "test_wall.wall", "1x1")]
    [InlineData("buildings.toml", "free_unit = \"test_harvester\"", "free_unit = \"test_nope\"", "test_refinery.free_unit", "unknown vehicle")]
    [InlineData("buildings.toml", "dock = [1, 3]", "dock = [1, 9]", "test_refinery.dock", "must be between -1 and 6")]
    [InlineData("buildings.toml", "storage = 1500", "storage = -1", "test_silo.storage", "must be between")]
    [InlineData("buildings.toml", "refinery = true", "refinery = 1", "test_refinery.refinery", "expected a boolean")]
    [InlineData("buildings.toml", "produces = \"infantry\"", "produces = \"navy\"", "test_barracks.produces", "must be one of")]
    [InlineData("buildings.toml", "needs_power = true", "needs_power = true\ncategory = \"hq\"", "test_turret.category", "must be one of")]
    [InlineData("buildings.toml", "needs_power = true", "needs_power = true\nbogus = 1", "test_turret.bogus", "unknown key")]
    [InlineData("vehicles.toml", "harvest_capacity = 20", "harvest_capacity = 1001", "test_harvester.harvest_capacity", "must be between 0 and 1000")]
    [InlineData("game.toml", "max_queue = 5", "max_queue = 0", "economy.max_queue", "must be between")]
    [InlineData("game.toml", "[1500, 2500, 4000]", "[1500, 2500]", "economy.low_power_slowdown_permille", "expected 3 values")]
    [InlineData("game.toml", "build_radius = 2", "build_radius = 2\nbogus = 1", "economy.bogus", "unknown key")]
    public void BadValuesAreProblems(string file, string old, string @new, string key, string message)
    {
        using var temp = new TempRules((file, TempRules.RealWith(file, old, @new)));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        RulesProblem p = Assert.Single(ex.Problems, x => x.Key == key);
        Assert.Equal(file, p.File);
        Assert.Contains(message, p.Message);
    }

    [Fact]
    public void MissingEconomyTable_IsAProblem()
    {
        string game = TempRules.Real("game.toml");
        game = game[..game.IndexOf("[economy]", StringComparison.Ordinal)];
        using var temp = new TempRules(("game.toml", game));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        Assert.Contains(ex.Problems, p => p.Key == "economy" && p.Message.Contains("missing required"));
    }

    [Theory]
    [InlineData("game.toml", "starting_cash = 5000", "starting_cash = 5001")]
    [InlineData("game.toml", "[1500, 2500, 4000]", "[1500, 2500, 4001]")]
    [InlineData("game.toml", "regrowth_interval_ticks = 0", "regrowth_interval_ticks = 9")]
    [InlineData("buildings.toml", "[test_silo]", "[test_silo]\nbuild_time = 5")]
    [InlineData("buildings.toml", "[test_silo]", "[test_silo]\nexit = [0, 0]")]
    [InlineData("buildings.toml", "storage = 1500", "storage = 1501")]
    [InlineData("buildings.toml", "dock = [1, 3]", "dock = [2, 3]")]
    [InlineData("buildings.toml", "produces = \"infantry\"", "produces = \"aircraft\"")]
    [InlineData("buildings.toml", "category = \"defence\"\nneeds_power", "category = \"structure\"\nneeds_power")]
    [InlineData("buildings.toml", "needs_power = true", "needs_power = false")]
    [InlineData("buildings.toml", "wall = true", "wall = false")]
    [InlineData("buildings.toml", "refinery = true", "refinery = false")]
    [InlineData("buildings.toml", "free_unit = \"test_harvester\"", "free_unit = \"\"")]
    [InlineData("vehicles.toml", "harvest_capacity = 20", "harvest_capacity = 21")]
    [InlineData("vehicles.toml", "harvest_capacity = 20", "harvest_capacity = 20\nbuild_time = 9")]
    [InlineData("infantry.toml", "[test_engineer]", "[test_engineer]\nbuild_time = 9")]
    public void HashChangesWhenAnyNewFieldChanges(string file, string old, string @new)
    {
        ulong original = Load().RulesHash;
        using var temp = new TempRules((file, TempRules.RealWith(file, old, @new)));
        Assert.NotEqual(original, RulesLoader.Load(temp.Dir).RulesHash);
    }

    [Fact]
    public void HashFormatVersionIs6() => Assert.Equal(6, RulesHash.FormatVersion);   // v5: BuildingType.Buildable; v6: schema v3 (M3-01)
}
