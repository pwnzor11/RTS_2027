// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Rules;

/// <summary>Validation errors (each reported with file and key), collected reporting, and <c>base</c> inheritance.</summary>
public sealed class RulesValidationTests
{
    [Theory]
    [InlineData("vehicles.toml", "max_health = 400", "max_health = 400\nbogus = 1", "test_tank.bogus", "unknown key")]
    [InlineData("vehicles.toml", "cost = 800\n", "", "test_tank.cost", "missing required key")]
    [InlineData("vehicles.toml", "speed = 20", "speed = \"20\"", "test_tank.speed", "expected an integer")]
    [InlineData("vehicles.toml", "speed = 20", "speed = 0", "test_tank.speed", "must be between")]
    [InlineData("vehicles.toml", "primary_weapon = \"test_cannon\"", "primary_weapon = \"test_nope\"", "test_tank.primary_weapon", "unknown weapon")]
    [InlineData("vehicles.toml", "cost = 800\nprerequisites = [\"test_factory\"]", "cost = 800\nprerequisites = [\"test_nope\"]", "test_tank.prerequisites", "unknown type")]
    [InlineData("buildings.toml", "armor = \"fortified\"", "armor = \"flying\"", "test_turret.armor", "must be one of unarmored")]
    [InlineData("buildings.toml", "footprint_w = 1", "footprint_w = 7", "test_turret.footprint_w", "must be between 1 and 6")]
    [InlineData("buildings.toml", "prerequisites = [\"test_barracks\"]\nprovides = []", "prerequisites = [\"class:tank\"]\nprovides = []", "test_turret.prerequisites", "unknown class 'tank'")]
    [InlineData("infantry.toml", "max_health = 50", "max_health = -1", "test_rifleman.max_health", "must be between")]
    [InlineData("weapons.toml", "warhead = \"test_he\"", "warhead = \"test_nope\"", "test_artillery.warhead", "unknown warhead")]
    [InlineData("weapons.toml", "range = 2560", "range = 768", "test_artillery.range", "must be greater than min_range")]
    [InlineData("weapons.toml", "rate_of_fire = 8", "rate_of_fire = 0", "test_mg.rate_of_fire", "must be between")]
    [InlineData("warheads.toml", "versus = [25, 75, 100, 50, 40]", "versus = [25, 75, 100, 50]", "test_ap.versus", "expected 8 values")]
    [InlineData("warheads.toml", "versus = [100, 50, 15, 10, 5]", "versus = [100, 50, 15, 10, 1001]", "test_small_arms.versus", "must be between")]
    [InlineData("warheads.toml", "splash = 1", "splash = 2", "test_he.splash", "must be between 0 and 1")]
    [InlineData("techs.toml", "target = \"test_turret\"", "target = \"class:tank\"", "test_unlock_turret.target", "unknown class 'tank'")]
    [InlineData("techs.toml", "op = \"mul\"", "op = \"divide\"", "test_armor_up.op", "must be one of enable")]
    [InlineData("game.toml", "tick_rate = 20", "tick_rate = 30", "game.tick_rate", "must be 20")]
    [InlineData("game.toml", "default_map_height = 32", "default_map_height = 32\nextra = 1", "game.extra", "unknown key")]
    public void ValidationErrorIsReportedWithFileAndKey(string file, string old, string @new, string expectedKey, string expectedMessage)
    {
        using var temp = new TempRules((file, TempRules.RealWith(file, old, @new)));

        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));

        RulesProblem problem = Assert.Single(ex.Problems, p => p.Key == expectedKey);
        Assert.Equal(file, problem.File);
        Assert.Contains(expectedMessage, problem.Message);
    }

    [Fact]
    public void DuplicateIdAcrossFilesIsReported()
    {
        string infantry = TempRules.Real("infantry.toml") + "\n[test_mg]\nspeed = 10\nmax_health = 1\narmor = \"light\"\n" +
            "sight = 1\nprimary_weapon = \"\"\ncost = 1\nprerequisites = []\n";
        using var temp = new TempRules(("infantry.toml", infantry));

        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));

        RulesProblem problem = Assert.Single(ex.Problems, p => p.Key == "test_mg");
        Assert.Equal("infantry.toml", problem.File);
        Assert.Contains("duplicate id", problem.Message);
    }

    [Fact]
    public void UnknownTerrainLandIsReported()
    {
        using var temp = new TempRules(("terrain.toml", TempRules.RealWith("terrain.toml", "[speed.cliff]", "[speed.cliffs]")));

        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));

        Assert.Contains(ex.Problems, p => p.File == "terrain.toml" && p.Key == "speed.cliffs" && p.Message.Contains("unknown land type"));
        Assert.Contains(ex.Problems, p => p.File == "terrain.toml" && p.Key == "speed.cliff" && p.Message.Contains("missing required land type"));
    }

    [Fact]
    public void ReportsAllProblemsAcrossFiles()
    {
        string weapons = TempRules.RealWith("weapons.toml", "warhead = \"test_he\"", "warhead = \"test_nope\"");
        string buildings = TempRules.RealWith("buildings.toml", "armor = \"fortified\"", "armor = \"flying\"");
        using var temp = new TempRules(("weapons.toml", weapons), ("buildings.toml", buildings));

        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));

        Assert.Contains(ex.Problems, p => p.File == "weapons.toml" && p.Key == "test_artillery.warhead");
        Assert.Contains(ex.Problems, p => p.File == "buildings.toml" && p.Key == "test_turret.armor");
    }

    [Fact]
    public void MissingFileIsReported()
    {
        using var temp = new TempRules(("vehicles.toml", null));

        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));

        Assert.Contains(ex.Problems, p => p.File == "vehicles.toml" && p.Key == "<file>");
    }

    [Fact]
    public void BaseCopiesValuesAndOverridesKeys()
    {
        string vehicles = TempRules.Real("vehicles.toml") + "\n[test_tank_mk2]\nbase = \"test_tank\"\ncost = 900\n";
        using var temp = new TempRules(("vehicles.toml", vehicles));

        RuleSet rules = RulesLoader.Load(temp.Dir);

        VehicleType mk2 = rules.Sim.Vehicles.Single(v => v.Id == "test_tank_mk2");
        VehicleType tank = rules.Sim.Vehicles.Single(v => v.Id == "test_tank");
        Assert.Equal(900, mk2.Cost);
        Assert.Equal(tank.Speed, mk2.Speed);
        Assert.Equal(tank.PrimaryWeapon, mk2.PrimaryWeapon);
        Assert.Equal(tank.Locomotion, mk2.Locomotion);
        Assert.Equal(6, rules.Sim.Vehicles.Count);
    }

    [Fact]
    public void BaseMustNotBeDerivedOneLevelOnly()
    {
        string vehicles = TempRules.Real("vehicles.toml") +
            "\n[test_tank_mk2]\nbase = \"test_tank\"\ncost = 900\n\n[test_tank_mk3]\nbase = \"test_tank_mk2\"\ncost = 950\n";
        using var temp = new TempRules(("vehicles.toml", vehicles));

        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));

        RulesProblem problem = Assert.Single(ex.Problems, p => p.Key == "test_tank_mk3.base");
        Assert.Contains("itself derived", problem.Message);
    }

    [Fact]
    public void MissingBaseIsReported()
    {
        string vehicles = TempRules.Real("vehicles.toml") + "\n[test_ghost]\nbase = \"test_nope\"\ncost = 1\n";
        using var temp = new TempRules(("vehicles.toml", vehicles));

        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));

        RulesProblem problem = Assert.Single(ex.Problems, p => p.Key == "test_ghost.base");
        Assert.Equal("vehicles.toml", problem.File);
        Assert.Contains("not found", problem.Message);
    }

    [Fact]
    public void BaseInAnotherFileIsReported()
    {
        string infantry = TempRules.Real("infantry.toml") + "\n[test_cross]\nbase = \"test_tank\"\ncost = 1\n";
        using var temp = new TempRules(("infantry.toml", infantry));

        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));

        RulesProblem problem = Assert.Single(ex.Problems, p => p.Key == "test_cross.base");
        Assert.Equal("infantry.toml", problem.File);
        Assert.Contains("defined in vehicles.toml", problem.Message);
    }
}
