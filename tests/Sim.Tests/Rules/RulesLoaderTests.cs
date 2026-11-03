using Rts.Data;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Rules;

/// <summary>Loads the real data/rules files and checks counts, reference resolution, id order and the rules hash.</summary>
public sealed class RulesLoaderTests
{
    private static RuleSet LoadReal() => RulesLoader.Load(TempRules.RealDir());

    /// <summary>
    /// The shipped game data (<c>data/rules</c>, not the fixtures) must load with no problems and have unique ids.
    /// Deliberately checks no counts or values: balance and content work must not break this test.
    /// </summary>
    [Fact]
    public void RealGameData_Validates()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);

        RuleSet rules = RulesLoader.Load(dir!);   // throws RulesException listing every problem found

        string[] ids = rules.Sim.Vehicles.Select(v => v.Id)
            .Concat(rules.Sim.Infantry.Select(i => i.Id))
            .Concat(rules.Sim.Buildings.Select(b => b.Id))
            .Concat(rules.Sim.Weapons.Select(w => w.Id))
            .Concat(rules.Sim.Warheads.Select(w => w.Id))
            .Concat(rules.Sim.Effects.Select(e => e.Id))
            .ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void LoadsRealDataWithExpectedCounts()
    {
        RuleSet rules = LoadReal();

        Assert.Equal(20, rules.Game.TickRate);
        Assert.Equal(4, rules.Sim.Vehicles.Count);
        Assert.Equal(2, rules.Sim.Infantry.Count);
        Assert.Equal(4, rules.Sim.Buildings.Count);
        Assert.Equal(4, rules.Sim.Weapons.Count);
        Assert.Equal(3, rules.Sim.Warheads.Count);
        Assert.Equal(2, rules.Sim.Effects.Count);
        Assert.NotEqual(0UL, rules.RulesHash);
    }

    [Fact]
    public void ResolvesWeaponAndWarheadReferencesToIndexes()
    {
        SimRules sim = LoadReal().Sim;

        VehicleType tank = sim.Vehicles.Single(v => v.Id == "test_tank");
        Assert.Equal(WeaponIndex(sim, "test_cannon"), tank.PrimaryWeapon);
        Assert.Equal(-1, sim.Vehicles.Single(v => v.Id == "test_crawler").PrimaryWeapon);
        Assert.Equal(WarheadIndex(sim, "test_ap"), sim.Weapons[tank.PrimaryWeapon].Warhead);
        Assert.Equal(WarheadIndex(sim, "test_he"), sim.Weapons[WeaponIndex(sim, "test_artillery")].Warhead);
        Assert.Equal(WeaponIndex(sim, "test_rifle"), sim.Infantry.Single(i => i.Id == "test_rifleman").PrimaryWeapon);
        Assert.Equal(-1, sim.Infantry.Single(i => i.Id == "test_engineer").PrimaryWeapon);
        Assert.Equal(WeaponIndex(sim, "test_mg"), sim.Buildings.Single(b => b.Id == "test_turret").PrimaryWeapon);
        Assert.Equal(new[] { "test_factory" }, tank.Prerequisites.ToArray());
    }

    [Fact]
    public void ReadsTerrainAndBuildableFromTomlValues()
    {
        TerrainTable terrain = LoadReal().Sim.Terrain;

        Assert.Equal(0, terrain.SpeedPercent(LandType.Water, Locomotion.Tracked));
        Assert.Equal(100, terrain.SpeedPercent(LandType.Water, Locomotion.Hover));
        Assert.Equal(125, terrain.SpeedPercent(LandType.Road, Locomotion.Wheeled));
        Assert.Equal(75, terrain.SpeedPercent(LandType.Rough, Locomotion.Foot));
        Assert.Equal(100, terrain.SpeedPercent(LandType.Cliff, Locomotion.Air));
        Assert.True(terrain.Buildable(LandType.Ash));
        Assert.False(terrain.Buildable(LandType.Rough));
    }

    [Fact]
    public void EveryListIsSortedByOrdinalId()
    {
        SimRules sim = LoadReal().Sim;

        AssertSorted(sim.Vehicles.Select(v => v.Id));
        AssertSorted(sim.Infantry.Select(i => i.Id));
        AssertSorted(sim.Buildings.Select(b => b.Id));
        AssertSorted(sim.Weapons.Select(w => w.Id));
        AssertSorted(sim.Warheads.Select(w => w.Id));
        AssertSorted(sim.Effects.Select(e => e.Id));
    }

    [Fact]
    public void HashIsStableUnderReformattingAndKeyOrder()
    {
        ulong original = LoadReal().RulesHash;
        using var temp = new TempRules(
            ("vehicles.toml", "# reformatted\n\n" + TempRules.RealWith("vehicles.toml", "speed = 16\nmax_health = 300", "max_health = 300\nspeed   =   16  # leptons")),
            ("buildings.toml", "\n\n" + TempRules.Real("buildings.toml").Replace("footprint_w = 3", "footprint_w =   3   # cells")));

        Assert.Equal(original, RulesLoader.Load(temp.Dir).RulesHash);
    }

    [Theory]
    [InlineData("weapons.toml", "damage = 40", "damage = 41")]
    [InlineData("warheads.toml", "versus = [25, 75, 100, 50, 40]", "versus = [25, 75, 100, 50, 41]")]
    [InlineData("warheads.toml", "splash = 1", "splash = 0")]
    [InlineData("vehicles.toml", "cost = 800", "cost = 801")]
    [InlineData("infantry.toml", "max_health = 50", "max_health = 51")]
    [InlineData("buildings.toml", "power = 20", "power = 21")]
    [InlineData("terrain.toml", "wheeled = 125", "wheeled = 126")]
    [InlineData("terrain.toml", "clear = true", "clear = false")]
    [InlineData("techs.toml", "value = 1100", "value = 1200")]
    public void HashChangesWhenAnyValueChanges(string file, string old, string @new)
    {
        ulong original = LoadReal().RulesHash;
        using var temp = new TempRules((file, TempRules.RealWith(file, old, @new)));

        Assert.NotEqual(original, RulesLoader.Load(temp.Dir).RulesHash);
    }

    private static int WeaponIndex(SimRules sim, string id) => SimRules.IndexOf(sim.Weapons, id, w => w.Id);

    private static int WarheadIndex(SimRules sim, string id) => SimRules.IndexOf(sim.Warheads, id, w => w.Id);

    private static void AssertSorted(IEnumerable<string> ids)
    {
        string[] actual = ids.ToArray();
        Assert.Equal(actual.OrderBy(s => s, StringComparer.Ordinal).ToArray(), actual);
    }
}
