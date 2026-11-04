// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Rules;

/// <summary>Rules schema v3 (M3-01): every new optional key, its default and an explicit value; game.toml M3 tables;
/// terrain [soft]; upgrades.toml; lazy start-kit validation; hash coverage of every new field.</summary>
public sealed class SchemaV3Tests
{
    private static RuleSet Load() => RulesLoader.Load(TestData.RulesDir);
    private static RuleSet Load(params (string File, string? Content)[] overrides)
    {
        using var temp = new TempRules(overrides);
        return RulesLoader.Load(temp.Dir);
    }

    private static string GameWithoutM3() =>
        TempRules.Real("game.toml")[..TempRules.Real("game.toml").IndexOf("# M3 (design 05)", StringComparison.Ordinal)];

    private static string TerrainWithoutSoft() =>
        TempRules.Real("terrain.toml")[..TempRules.Real("terrain.toml").IndexOf("# M3: soft ground", StringComparison.Ordinal)];

    // ---- defaults -----------------------------------------------------------------------------

    [Fact]
    public void MobileDefaults_KeepOldBehaviour()
    {
        RuleSet r = Load();
        VehicleType tank = r.Sim.Vehicles.Single(v => v.Id == "test_tank");
        Assert.Equal(Faction.Authority, tank.Faction);
        Assert.Equal(TunnelSize.None, tank.TunnelSize);
        Assert.Equal(0, tank.AuraRadius);
        Assert.Equal(1000, tank.AuraRofPermille);
        Assert.Equal(1000, tank.AuraSpeedPermille);
        Assert.Empty(tank.AuraTargets);
        Assert.Equal(0, tank.RadResistPermille);
        Assert.False(tank.Irradiated);
        Assert.False(tank.Stealth);
        Assert.Equal(-1, tank.DeathWeapon);
        Assert.Equal(0, tank.JamRadius);
        Assert.Equal(0, tank.SeismicRadius);
        Assert.Equal(0, tank.Passengers);
        Assert.Equal(-1, tank.DeploysInto);

        InfantryType eng = r.Sim.Infantry.Single(i => i.Id == "test_engineer");
        Assert.Equal(TunnelSize.S, eng.TunnelSize);       // infantry default S
        Assert.False(eng.Engineer);
        Assert.Equal(-1, eng.DeathWeapon);
        Assert.Equal(1000, eng.AuraRofPermille);
    }

    [Fact]
    public void BuildingWeaponWarheadDefaults_KeepOldBehaviour()
    {
        RuleSet r = Load();
        BuildingType hq = r.Sim.Buildings.Single(b => b.Id == "test_hq");
        Assert.Equal(Faction.Authority, hq.Faction);
        Assert.Equal(TunnelSize.None, hq.Entrance);
        Assert.Equal(20, hq.TicksPerPoint);
        Assert.False(hq.Researches);
        Assert.False(hq.Gate);
        Assert.Equal(0, hq.SeismicRadius);
        Assert.False(hq.SeesThroughBlackout);
        Assert.Equal(0, hq.JamRadius);
        Assert.Equal("", hq.Superweapon);
        Assert.Equal(0, hq.SuperweaponChargeTicks);
        Assert.Equal(0, hq.SuperweaponEntranceRange);
        Assert.All(r.Sim.Weapons, w => Assert.Equal(WeaponTargets.Ground, w.Targets));
        WarheadType ap = r.Sim.Warheads.Single(w => w.Id == "test_ap");
        Assert.Equal(0, ap.Contaminate);
        Assert.Equal(0, ap.Decontaminate);
        Assert.Equal(100, ap.BonusVsIrradiatedPercent);
        Assert.Equal(0, ap.BlackoutTicks);
    }

    [Fact]
    public void GameTablesAbsent_UseDefaults_AndHashLikeExplicitDefaults()
    {
        RuleSet explicitDefaults = Load();
        RuleSet absent = Load(("game.toml", GameWithoutM3()), ("terrain.toml", TerrainWithoutSoft()));
        Assert.Equal(TunnelRules.Default, absent.Sim.Tunnels);
        Assert.Equal(RadiationRules.Default, absent.Sim.Radiation);
        Assert.Equal(StealthRules.Default, absent.Sim.Stealth);
        Assert.Equal("auth_conyard", absent.Sim.StartKits[(int)Faction.Authority].Conyard);
        Assert.Empty(absent.Sim.StartKits[(int)Faction.Authority].Units);
        Assert.Equal("dir_shaft", absent.Sim.StartKits[(int)Faction.Directorate].Conyard);
        Assert.Equal(explicitDefaults.RulesHash, absent.RulesHash);   // the fixture's M3 tables equal the defaults
        Assert.Equal(TunnelRules.Default, explicitDefaults.Sim.Tunnels);
        Assert.Equal(RadiationRules.Default, explicitDefaults.Sim.Radiation);
    }

    [Fact]
    public void DefaultSoft_IsClearRoughAsh()
    {
        TerrainTable t = Load(("terrain.toml", TerrainWithoutSoft())).Sim.Terrain;
        Assert.True(t.Soft(LandType.Clear));
        Assert.True(t.Soft(LandType.Rough));
        Assert.True(t.Soft(LandType.Ash));
        Assert.False(t.Soft(LandType.Road));
        Assert.False(t.Soft(LandType.Rock));
        Assert.False(t.Soft(LandType.Cliff));
        Assert.False(t.Soft(LandType.Water));
    }

    [Fact]
    public void UpgradesFileAbsent_MeansNoUpgrades()
    {
        RuleSet r = Load(("upgrades.toml", null));
        Assert.Empty(r.Sim.Upgrades);
    }

    [Fact]
    public void FactionFromIdPrefix()
    {
        Assert.Equal(Faction.Directorate, SimRules.FactionFromId("dir_shaft"));
        Assert.Equal(Faction.Authority, SimRules.FactionFromId("auth_tank"));
        Assert.Equal(Faction.Authority, SimRules.FactionFromId("test_tank"));
    }

    // ---- explicit values ----------------------------------------------------------------------

    [Fact]
    public void ExplicitMobileKeys_Parse()
    {
        RuleSet r = Load(
            ("vehicles.toml", TempRules.RealWith("vehicles.toml", "[test_tank]", "[test_tank]\nfaction = \"directorate\"\ntunnel_size = \"M\"\n"
                + "aura_radius = 3\naura_rof_permille = 800\naura_speed_permille = 1200\naura_targets = [\"test_jeep\", \"class:infantry\"]\n"
                + "rad_resist_permille = 600\nirradiated = true\nstealth = true\ndeath_weapon = \"test_cannon\"\njam_radius = 4\n"
                + "seismic_radius = 5\npassengers = 5\ndeploys_into = \"test_silo\"")),
            ("infantry.toml", TempRules.RealWith("infantry.toml", "[test_engineer]", "[test_engineer]\nengineer = true\ntunnel_size = \"none\"")));
        VehicleType t = r.Sim.Vehicles.Single(v => v.Id == "test_tank");
        Assert.Equal(Faction.Directorate, t.Faction);
        Assert.Equal(TunnelSize.M, t.TunnelSize);
        Assert.Equal(3, t.AuraRadius);
        Assert.Equal(800, t.AuraRofPermille);
        Assert.Equal(1200, t.AuraSpeedPermille);
        Assert.Equal(new[] { "test_jeep", "class:infantry" }, t.AuraTargets);
        Assert.Equal(600, t.RadResistPermille);
        Assert.True(t.Irradiated);
        Assert.True(t.Stealth);
        Assert.Equal("test_cannon", r.Sim.Weapons[t.DeathWeapon].Id);
        Assert.Equal(4, t.JamRadius);
        Assert.Equal(5, t.SeismicRadius);
        Assert.Equal(5, t.Passengers);
        Assert.Equal("test_silo", r.Sim.Buildings[t.DeploysInto].Id);
        InfantryType e = r.Sim.Infantry.Single(i => i.Id == "test_engineer");
        Assert.True(e.Engineer);
        Assert.Equal(TunnelSize.None, e.TunnelSize);
    }

    [Fact]
    public void ExplicitBuildingWeaponWarheadKeys_Parse()
    {
        RuleSet r = Load(
            ("buildings.toml", TempRules.RealWith("buildings.toml", "[test_hq]", "[test_hq]\nfaction = \"directorate\"\nentrance = \"M\"\n"
                + "ticks_per_point = 13\nresearches = true\nseismic_radius = 6\nsees_through_blackout = true\njam_radius = 7\n"
                + "superweapon = \"sunrise\"\nsuperweapon_charge_ticks = 9000\nsuperweapon_entrance_range = 8")
                .Replace("[test_wall]", "[test_wall]\ngate = true")),
            ("weapons.toml", TempRules.RealWith("weapons.toml", "warhead = \"test_ap\"", "warhead = \"test_ap\"\ntargets = \"both\"")),
            ("warheads.toml", TempRules.RealWith("warheads.toml", "[test_ap]", "[test_ap]\ncontaminate = 40\ndecontaminate = 10\n"
                + "bonus_vs_irradiated_percent = 300\nblackout_ticks = 200")));
        BuildingType hq = r.Sim.Buildings.Single(b => b.Id == "test_hq");
        Assert.Equal(Faction.Directorate, hq.Faction);
        Assert.Equal(TunnelSize.M, hq.Entrance);
        Assert.Equal(13, hq.TicksPerPoint);
        Assert.True(hq.Researches);
        Assert.Equal(6, hq.SeismicRadius);
        Assert.True(hq.SeesThroughBlackout);
        Assert.Equal(7, hq.JamRadius);
        Assert.Equal("sunrise", hq.Superweapon);
        Assert.Equal(9000, hq.SuperweaponChargeTicks);
        Assert.Equal(8, hq.SuperweaponEntranceRange);
        BuildingType wall = r.Sim.Buildings.Single(b => b.Id == "test_wall");
        Assert.True(wall.Gate);
        Assert.True(wall.Wall);
        Assert.Equal(WeaponTargets.Both, r.Sim.Weapons.Single(w => w.Id == "test_cannon").Targets);
        WarheadType ap = r.Sim.Warheads.Single(w => w.Id == "test_ap");
        Assert.Equal(40, ap.Contaminate);
        Assert.Equal(10, ap.Decontaminate);
        Assert.Equal(300, ap.BonusVsIrradiatedPercent);
        Assert.Equal(200, ap.BlackoutTicks);
    }

    [Fact]
    public void ExplicitGameTablesAndSoft_Parse()
    {
        string game = GameWithoutM3() + "[tunnels]\ntransit_ticks = 41\nnetwork_cap_points = 11\npoints_s = 2\npoints_m = 4\n"
            + "dive_ticks = 21\ntelegraph_ticks = 61\nentrance_telegraph_ticks = 101\nemerging_health_permille = 251\nsurfacing_ring = 2\n"
            + "[radiation]\ndecay_ticks = 41\ndamage_interval_ticks = 21\ndamage_threshold = 33\ndamage_permille = 101\nno_build_threshold = 65\n"
            + "[stealth]\ndelay_ticks = 61\n"
            + "[faction.authority]\nconyard = \"test_hq\"\nunits = [\"test_tank\", \"test_rifleman\"]\n";
        RuleSet r = Load(("game.toml", game), ("terrain.toml", TerrainWithoutSoft() + "[soft]\nroad = true\nclear = false\n"));
        Assert.Equal(new TunnelRules(41, 11, 2, 4, 21, 61, 101, 251, 2), r.Sim.Tunnels);
        Assert.Equal(new RadiationRules(41, 21, 33, 101, 65), r.Sim.Radiation);
        Assert.Equal(61, r.Sim.Stealth.DelayTicks);
        Assert.Equal(4, r.Sim.Tunnels.PointsOf(TunnelSize.M));
        Assert.Equal(0, r.Sim.Tunnels.PointsOf(TunnelSize.L));
        FactionStartKit kit = r.Sim.StartKits[(int)Faction.Authority];
        Assert.Equal("test_hq", kit.Conyard);
        Assert.Equal(new[] { "test_tank", "test_rifleman" }, kit.Units);
        Assert.Equal("dir_shaft", r.Sim.StartKits[(int)Faction.Directorate].Conyard);   // untouched default
        Assert.True(r.Sim.Terrain.Soft(LandType.Road));
        Assert.False(r.Sim.Terrain.Soft(LandType.Clear));
        Assert.True(r.Sim.Terrain.Soft(LandType.Ash));                                   // omitted: default
        (int conyard, var units) = r.Sim.ResolveStartKit(Faction.Authority);
        Assert.Equal("test_hq", r.Sim.Buildings[conyard].Id);
        Assert.Equal(new[] { (ActorKind.Vehicle, SimRules.IndexOf(r.Sim.Vehicles, "test_tank", v => v.Id)), (ActorKind.Infantry, SimRules.IndexOf(r.Sim.Infantry, "test_rifleman", i => i.Id)) },
            units.ToArray());
    }

    [Fact]
    public void Upgrades_ParseAndIndex()
    {
        RuleSet r = Load();
        UpgradeType g = Assert.Single(r.Sim.Upgrades);
        Assert.Equal("auth_grouting", g.Id);
        Assert.Equal(Faction.Authority, g.Faction);
        Assert.Equal(1500, g.Cost);
        Assert.Equal("grouting", g.Effect);
        Assert.Equal(0, r.Sim.UpgradeIndex("auth_grouting"));
        Assert.Equal(-1, r.Sim.UpgradeIndex("nope"));

        RuleSet two = Load(("upgrades.toml", TempRules.Real("upgrades.toml")
            + "\n[dir_alpha]\ncost = 10\nprerequisites = [\"auth_grouting\", \"test_hq\"]\n"));
        Assert.Equal(new[] { "auth_grouting", "dir_alpha" }, two.Sim.Upgrades.Select(u => u.Id));
        Assert.Equal(Faction.Directorate, two.Sim.Upgrades[1].Faction);           // from the id prefix
        Assert.Equal("", two.Sim.Upgrades[1].Effect);
    }

    [Fact]
    public void DirectorateKit_IsValidatedLazily()
    {
        RuleSet r = Load();   // the fixture names dir_shaft, which does not exist: loading still succeeds
        var ex = Assert.Throws<InvalidOperationException>(() => r.Sim.ResolveStartKit(Faction.Directorate));
        Assert.Contains("dir_shaft", ex.Message);
        RuleSet bad = Load(("game.toml", GameWithoutM3() + "[faction.authority]\nconyard = \"test_hq\"\nunits = [\"test_nope\"]\n"));
        Assert.Contains("test_nope", Assert.Throws<InvalidOperationException>(() => bad.Sim.ResolveStartKit(Faction.Authority)).Message);
    }

    // ---- problems -----------------------------------------------------------------------------

    [Theory]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\ntunnel_size = \"XL\"", "test_tank.tunnel_size", "must be one of")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\nfaction = \"nod\"", "test_tank.faction", "must be one of")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\ndeath_weapon = \"nope\"", "test_tank.death_weapon", "unknown weapon")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\ndeploys_into = \"nope\"", "test_tank.deploys_into", "unknown building")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\naura_targets = [\"nope\"]", "test_tank.aura_targets", "unknown type")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\nrad_resist_permille = 1001", "test_tank.rad_resist_permille", "must be between")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\nengineer = true", "test_tank.engineer", "unknown key")]
    [InlineData("infantry.toml", "[test_engineer]", "[test_engineer]\npassengers = 2", "test_engineer.passengers", "unknown key")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\nentrance = \"L\"", "test_hq.entrance", "must be one of")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\ngate = true", "test_hq.gate", "1x1")]
    [InlineData("weapons.toml", "warhead = \"test_ap\"", "warhead = \"test_ap\"\ntargets = \"space\"", "test_cannon.targets", "must be one of")]
    [InlineData("warheads.toml", "[test_ap]", "[test_ap]\ncontaminate = 256", "test_ap.contaminate", "must be between")]
    [InlineData("game.toml", "transit_ticks = 40", "transit_ticks = -1", "tunnels.transit_ticks", "must be between")]
    [InlineData("game.toml", "delay_ticks = 60", "delay_ticks = 60\nbogus = 1", "stealth.bogus", "unknown key")]
    [InlineData("game.toml", "[faction.directorate]", "[faction.nod]", "faction.nod", "unknown faction")]
    [InlineData("terrain.toml", "[soft]\nclear = true", "[soft]\nclear = 1", "soft.clear", "expected a boolean")]
    [InlineData("upgrades.toml", "effect = \"grouting\"", "effect = \"grouting\"\nbogus = 1", "auth_grouting.bogus", "unknown key")]
    [InlineData("upgrades.toml", "prerequisites = []", "prerequisites = [\"nope\"]", "auth_grouting.prerequisites", "unknown type")]
    public void BadValuesAreProblems(string file, string old, string @new, string key, string message)
    {
        using var temp = new TempRules((file, TempRules.RealWith(file, old, @new)));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        RulesProblem p = Assert.Single(ex.Problems, x => x.Key == key);
        Assert.Equal(file, p.File);
        Assert.Contains(message, p.Message);
    }

    [Fact]
    public void UpgradeIdClashingWithATypeId_IsAProblem()
    {
        using var temp = new TempRules(("upgrades.toml", TempRules.Real("upgrades.toml") + "\n[test_hq]\ncost = 1\n"));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        Assert.Contains(ex.Problems, p => p.Key == "test_hq" && p.Message.Contains("duplicate id"));
    }

    // ---- hash ---------------------------------------------------------------------------------

    [Theory]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\nfaction = \"directorate\"")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\ntunnel_size = \"M\"")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\naura_radius = 1")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\naura_rof_permille = 999")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\naura_speed_permille = 999")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\naura_targets = [\"test_jeep\"]")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\nrad_resist_permille = 1")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\nirradiated = true")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\nstealth = true")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\ndeath_weapon = \"test_cannon\"")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\njam_radius = 1")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\nseismic_radius = 1")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\npassengers = 1")]
    [InlineData("vehicles.toml", "[test_tank]", "[test_tank]\ndeploys_into = \"test_silo\"")]
    [InlineData("infantry.toml", "[test_engineer]", "[test_engineer]\nengineer = true")]
    [InlineData("infantry.toml", "[test_engineer]", "[test_engineer]\ntunnel_size = \"M\"")]
    [InlineData("infantry.toml", "[test_engineer]", "[test_engineer]\nrad_resist_permille = 600")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\nfaction = \"directorate\"")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\nentrance = \"S\"")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\nticks_per_point = 13")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\nresearches = true")]
    [InlineData("buildings.toml", "[test_wall]", "[test_wall]\ngate = true")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\nseismic_radius = 1")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\nsees_through_blackout = true")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\njam_radius = 1")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\nsuperweapon = \"x\"")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\nsuperweapon_charge_ticks = 1")]
    [InlineData("buildings.toml", "[test_hq]", "[test_hq]\nsuperweapon_entrance_range = 1")]
    [InlineData("weapons.toml", "warhead = \"test_ap\"", "warhead = \"test_ap\"\ntargets = \"air\"")]
    [InlineData("warheads.toml", "[test_ap]", "[test_ap]\ncontaminate = 1")]
    [InlineData("warheads.toml", "[test_ap]", "[test_ap]\ndecontaminate = 1")]
    [InlineData("warheads.toml", "[test_ap]", "[test_ap]\nbonus_vs_irradiated_percent = 101")]
    [InlineData("warheads.toml", "[test_ap]", "[test_ap]\nblackout_ticks = 1")]
    [InlineData("terrain.toml", "road = false\nrough = true", "road = true\nrough = true")]
    [InlineData("game.toml", "transit_ticks = 40", "transit_ticks = 41")]
    [InlineData("game.toml", "network_cap_points = 10", "network_cap_points = 11")]
    [InlineData("game.toml", "points_s = 1", "points_s = 2")]
    [InlineData("game.toml", "points_m = 3", "points_m = 4")]
    [InlineData("game.toml", "dive_ticks = 20", "dive_ticks = 21")]
    [InlineData("game.toml", "\ntelegraph_ticks = 60", "\ntelegraph_ticks = 61")]
    [InlineData("game.toml", "entrance_telegraph_ticks = 100", "entrance_telegraph_ticks = 101")]
    [InlineData("game.toml", "emerging_health_permille = 250", "emerging_health_permille = 251")]
    [InlineData("game.toml", "surfacing_ring = 1", "surfacing_ring = 2")]
    [InlineData("game.toml", "decay_ticks = 40", "decay_ticks = 41")]
    [InlineData("game.toml", "damage_interval_ticks = 20", "damage_interval_ticks = 21")]
    [InlineData("game.toml", "damage_threshold = 32", "damage_threshold = 33")]
    [InlineData("game.toml", "damage_permille = 100", "damage_permille = 101")]
    [InlineData("game.toml", "no_build_threshold = 64", "no_build_threshold = 65")]
    [InlineData("game.toml", "delay_ticks = 60", "delay_ticks = 61")]
    [InlineData("game.toml", "conyard = \"dir_shaft\"", "conyard = \"dir_other\"")]
    [InlineData("game.toml", "conyard = \"auth_conyard\"\nunits = []", "conyard = \"auth_conyard\"\nunits = [\"test_tank\"]")]
    [InlineData("upgrades.toml", "cost = 1500", "cost = 1501")]
    [InlineData("upgrades.toml", "build_time = 0", "build_time = 1")]
    [InlineData("upgrades.toml", "prerequisites = []", "prerequisites = [\"test_hq\"]")]
    [InlineData("upgrades.toml", "effect = \"grouting\"", "effect = \"other\"")]
    [InlineData("upgrades.toml", "faction = \"authority\"", "faction = \"directorate\"")]
    public void HashChangesWhenAnyV3FieldChanges(string file, string old, string @new)
    {
        ulong original = Load().RulesHash;
        using var temp = new TempRules((file, TempRules.RealWith(file, old, @new)));
        Assert.NotEqual(original, RulesLoader.Load(temp.Dir).RulesHash);
    }

    [Fact]
    public void RealRules_LoadWithAuthorityKitReproducingTheM2Start()
    {
        RuleSet r = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
        (int conyard, var units) = r.Sim.ResolveStartKit(Faction.Authority);
        Assert.Equal(SkirmishStart.StartBuildingId, r.Sim.Buildings[conyard].Id);
        Assert.Empty(units);
        Assert.Equal("auth_grouting", Assert.Single(r.Sim.Upgrades).Id);
        Assert.All(r.Sim.Vehicles, v => Assert.Equal(Faction.Authority, v.Faction));
        Assert.All(r.Sim.Infantry, i => Assert.Equal(TunnelSize.S, i.TunnelSize));
    }
}
