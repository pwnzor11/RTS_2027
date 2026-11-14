// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Garrison;
using Rts.Sim.Missions;
using Rts.Sim.Replay;
using Rts.Sim.Rules;
using Rts.Sim.Serialization;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Production;
using Rts.Sim.Tests.Rules;

namespace Rts.Sim.Tests.M4;

/// <summary>M4-01 contracts (design 08 §6): enum values, part order, format versions, schema v4 TOML keys and defaults, tier sugar,
/// map metadata v2, symmetry images, and the stubs that keep M3 behaviour.</summary>
public sealed class M4ContractTests
{
    private static readonly Command[] None = Array.Empty<Command>();

    // ---- enums ----------------------------------------------------------------------------------

    [Fact]
    public void SimEnumValues_ArePinned()
    {
        Assert.Equal(22, (int)CommandKind.Unload);
        Assert.Equal(29, Enum.GetValues<CommandKind>().Length);   // S24 CommanderOrder = 28; S19 AttackGround = 26, QueueOrder = 27; no other new CommandKind in M4; M5-01 added Directive = 23, M6-02 Pack = 24, M11-10i Hold = 25
        Assert.Equal(7, (int)ActorLayer.Garrisoned);
        Assert.Equal(15, (int)MissionKind.Garrisoned);
        Assert.Equal(14, (int)MissionKind.Capture);
        Assert.Equal(7, (int)LandType.Shallow);
        Assert.Equal(9, RuleCounts.LandTypes);   // M7-02 appended Coast = 8
        Assert.Equal(RuleCounts.LandTypes, Enum.GetValues<LandType>().Length);
        Assert.Equal(5, (int)FxKind.GarrisonExit);
        Assert.Equal(6, (int)DebugOp.GrantUpgrade);
    }

    [Fact]
    public void ActorLayerHelpers_TreatGarrisonedLikePassengerExceptUnderground()
    {
        Assert.False(ActorLayers.IsUnderground(ActorLayer.Garrisoned));
        Assert.True(ActorLayers.IsUnderground(ActorLayer.Passenger));
        Assert.True(ActorLayers.IsCarried(ActorLayer.Garrisoned));
        Assert.True(ActorLayers.IsCarried(ActorLayer.Passenger));
        Assert.False(ActorLayers.IsCarried(ActorLayer.Surface));
    }

    [Fact]
    public void DataEnumValues_ArePinned()
    {
        Assert.Equal(new[] { 0, 1, 2, 3 }, new[] { Biome.RustBelt, Biome.GlassCoast, Biome.Mountains, Biome.AshPlains }.Select(b => (int)b));
        Assert.Equal(new[] { 0, 1, 2 }, new[] { MapSize.Small, MapSize.Medium, MapSize.Large }.Select(s => (int)s));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, new[] { MapSymmetry.None, MapSymmetry.Rot2, MapSymmetry.Rot4, MapSymmetry.MirrorX,
            MapSymmetry.MirrorY, MapSymmetry.MirrorDiag }.Select(s => (int)s));
        Assert.Equal((0, 1), ((int)MapIssueSeverity.Warning, (int)MapIssueSeverity.Error));
        Assert.Equal((64, 96, 128), (MapGenerator.CellsFor(MapSize.Small), MapGenerator.CellsFor(MapSize.Medium), MapGenerator.CellsFor(MapSize.Large)));
    }

    // ---- parts and formats ----------------------------------------------------------------------

    [Fact]
    public void M4Parts_FollowEffects_ThenExtensions()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        string[] names = sim.StateParts.Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "tunnels", "ground", "effects", "garrison", "stats", "directive", "rigs", "salvage", "veterancy", "shipments", "beachhead", "naval", "commander_orders" }, names[^13..]);   // M5-01 appended "directive", M6-02 "rigs" and "salvage"
        Assert.Same(sim.Garrison, sim.StateParts[^10]);
        Assert.Same(sim.Stats, sim.StateParts[^9]);
    }

    [Fact]
    public void FormatVersions_ArePinned()
    {
        Assert.Equal(11, RulesHash.FormatVersion);   // M6-02 took 8, VET-1 9, SUP-1 10, M7-02 11
        Assert.Equal(13, SimWriter.SaveVersion);   // M5-01 took 9, M6-02 10, VET-1 11, M7-02 12
        Assert.Equal(13, ReplayWriter.Version);           // M3-19 had already taken 8 for the mission block, M6-02 took 10, VET-1 11, M7-02 12, M4-17 13
        Assert.Equal(7, ReplayWriter.OldestReadableVersion);
        Assert.Equal(4, MapFile.FormatVersion);   // M7-02: format 3 (Coast, convoy_edge, landing); M12-04: format 4 (props, decor)
        Assert.Equal(1, MapFile.OldestReadableVersion);
    }

    [Fact]
    public void NewState_IsEmptyByDefault_AndRoundTripsThroughSave()
    {
        var setup = ScriptedGame.Setup();
        var sim = ScriptedGame.NewSim(setup);
        var script = ScriptedGame.Script(60);
        for (int i = 0; i < 60; i++) sim.Step(script);
        Assert.All(sim.Garrison.HomeOwner, o => Assert.Equal(GarrisonState.NoOwner, o));
        // Stats are live since M4-06: the scripted Place at tick 5 counts as a structure built, so only the round trip is checked.
        Assert.All(sim.Ground.Foam, v => Assert.Equal(0, v));

        sim.Garrison.HomeOwner[3] = 1;
        sim.Stats.Kills[1] = 4; sim.Stats.CreditsSpent[0] = 1234; sim.Stats.UnitsBuilt[1] = 7;
        sim.Ground.Foam[200] = 99;
        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        var loaded = Simulation.Load(ms, setup);
        Assert.Equal(sim.ComputeSubHashes(), loaded.ComputeSubHashes());
        Assert.Equal(1, loaded.Garrison.HomeOwner[3]);
        Assert.Equal(sim.StatsOf(0), loaded.StatsOf(0));
        Assert.Equal(sim.StatsOf(1), loaded.StatsOf(1));
        Assert.Equal(1234, loaded.StatsOf(0).CreditsSpent);
        Assert.Equal(4, loaded.StatsOf(1).Kills);
        Assert.Equal(99, loaded.Ground.FoamAt(new CellCoord(200 % 64, 200 / 64)));
    }

    [Fact]
    public void Foam_ChangesTheGroundSubHash()
    {
        var a = ScriptedGame.NewSim(ScriptedGame.Setup());
        var b = ScriptedGame.NewSim(ScriptedGame.Setup());
        b.Ground.Foam[5] = 1;
        Assert.NotEqual(a.ComputeSubHashes().Single(h => h.Part == "ground"), b.ComputeSubHashes().Single(h => h.Part == "ground"));
    }

    [Fact]
    public void OldSaveVersion7_IsRejected()
    {
        var ms = new MemoryStream(); ScriptedGame.NewSim(ScriptedGame.Setup()).Save(ms);
        byte[] bytes = ms.ToArray();
        bytes[4] = 7;
        var ex = Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(bytes), ScriptedGame.Setup()));
        Assert.Contains("version 7", ex.Message);
    }

    [Fact]
    public void ReplayVersion8_IsStillRead()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        var ms = new MemoryStream();
        new ReplayWriter(ms, sim, "b", Array.Empty<(string, string)>()).Finish();
        byte[] bytes = ms.ToArray();
        bytes[4] = 8;   // 8 and 9 share a layout
        Assert.Equal(8, ReplayReader.Read(new MemoryStream(bytes)).Version);
    }

    // ---- stubs keep M3 behaviour ------------------------------------------------------------------

    [Fact]
    public void Stubs_ReturnDefaults()
    {
        var sim = ProductionFixture.Base();
        EntityId hq = sim.Buildings.Slots.IdOf(1);
        Assert.Equal(0, sim.OccupantsIn(hq));
        Assert.Equal(0, sim.OccupantsIn(EntityId.None));
        Assert.Equal(1, sim.TierOf(0));
        Assert.Equal(1, sim.ViewFor(0).Tier);
        Assert.Equal(0, sim.ViewFor(0).OccupantsOf(hq));
        Assert.Equal(-1, sim.ViewFor(1).OccupantsOf(hq));    // an enemy's building: unknown
        Assert.All(sim.Snapshot().Buildings, b => Assert.Equal(0, b.Occupants));
        Assert.Equal(default, sim.StatsOf(200));             // out of range: zeros
        Assert.DoesNotContain(MapValidator.Validate(Rts.Sim.Map.MapData.CreateFlat(32, 32), null, null), i => i.Severity == MapIssueSeverity.Error); // M4-02 filled
    }

    [Fact]
    public void UnloadCommand_AndGarrisonedMission_DoNotChangeState()
    {
        var sim = ProductionFixture.Base();
        EntityId tank = sim.SpawnVehicle(0, ProductionFixture.Veh("test_tank"), new CellCoord(20, 20));
        EntityId rifle = sim.SpawnInfantry(0, ProductionFixture.Inf("test_rifleman"), new CellCoord(21, 20));
        EntityId hq = sim.Buildings.Slots.IdOf(1);
        sim.Step(None);
        var reference = ProductionFixture.Base();
        reference.SpawnVehicle(0, ProductionFixture.Veh("test_tank"), new CellCoord(20, 20));
        reference.SpawnInfantry(0, ProductionFixture.Inf("test_rifleman"), new CellCoord(21, 20));
        reference.Step(None);
        int t = sim.Tick;
        sim.Step(new[]
        {
            new Command(t, 0, 0, CommandKind.Unload, hq, default, default, (int)ActorKind.Building),
            new Command(t, 0, 1, CommandKind.Unload, tank, default, default, (int)ActorKind.Vehicle),
        });
        reference.Step(None);
        Assert.Equal(reference.ComputeStateHash(), sim.ComputeStateHash());
        Assert.True(sim.Infantry.Slots.IsAlive(rifle));
    }

    [Fact]
    public void GrantUpgrade_SetsTheBit_OnlyWithDebug()
    {
        RuleSet rules = ProductionFixture.Loaded;
        Assert.NotEmpty(rules.Sim.Upgrades);
        var setup = ScriptedGame.Setup() with { Rules = ScriptedGame.Setup().Rules with { Upgrades = rules.Sim.Upgrades } };
        var sim = ScriptedGame.NewSim(setup with { AllowDebug = true });
        sim.Step(new[] { new Command(sim.Tick, 1, 0, CommandKind.Debug, default, default, default, (int)DebugOp.GrantUpgrade, 0),
            new Command(sim.Tick, 1, 1, CommandKind.Debug, default, default, default, (int)DebugOp.GrantUpgrade, 63) });   // out of range: ignored
        Assert.True(sim.HasUpgrade(1, 0));
        Assert.Equal(1UL, sim.Players.Upgrades[1]);
        var locked = ScriptedGame.NewSim(setup with { AllowDebug = false });
        locked.Step(new[] { new Command(locked.Tick, 1, 0, CommandKind.Debug, default, default, default, (int)DebugOp.GrantUpgrade, 0) });
        Assert.False(locked.HasUpgrade(1, 0));
    }

    // ---- TOML schema v4 ----------------------------------------------------------------------------

    [Fact]
    public void SchemaV4_Defaults_LeaveEveryM3TypeUnchanged()
    {
        foreach (RuleSet rules in new[] { ProductionFixture.Loaded })   // the fixture keeps the schema defaults; the shipped data is tiered (next test)
        {
            SimRules r = rules.Sim;
            Assert.All(r.Buildings, b =>
            {
                Assert.Equal(1, b.Tier); Assert.Equal(0, b.Garrison); Assert.Equal(256, b.GarrisonRangeBonus); Assert.Equal(750, b.GarrisonRofPermille);
            });
            Assert.All(r.Vehicles, v => Assert.Equal(1, v.Tier));
            Assert.All(r.Infantry, i => { Assert.Equal(1, i.Tier); Assert.Equal(!i.Engineer, i.CanGarrison); });
            Assert.All(r.Warheads, w =>
            {
                Assert.False(w.HitsBurrowed); Assert.Equal(100, w.BonusVsEntrancePercent); Assert.Equal(0, w.FoamTicks); Assert.Equal(0, w.GarrisonDamagePercent);
            });
            Assert.All(r.Upgrades, u => { Assert.Equal(1, u.Tier); Assert.Empty(u.Effects); });
            Assert.Equal(GarrisonRules.Default, r.Garrison);
            Assert.Equal(new GarrisonRules(200, 1), r.Garrison);
            Assert.Equal(new FoamRules(500), r.Foam);
            Assert.All(r.StartKits, k => { Assert.Equal("", k.Tier2Provides); Assert.Equal("", k.Tier3Provides); });
            Assert.False(r.Terrain.Buildable(LandType.Shallow));
            Assert.False(r.Terrain.Soft(LandType.Shallow));
        }
    }

    [Fact]
    public void RealData_CarriesTheDesign08Section4Tiers()
    {
        SimRules r = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!).Sim;
        Assert.All(r.Buildings, b => Assert.InRange(b.Tier, 1, 3));
        Assert.All(r.Vehicles, v => Assert.InRange(v.Tier, 1, 3));
        Assert.All(r.Infantry, i => Assert.InRange(i.Tier, 1, 3));
        Assert.All(r.Upgrades, u => Assert.InRange(u.Tier, 1, 3));

        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["auth_conyard"] = 1, ["auth_seismograph"] = 1, ["auth_gate"] = 1, ["auth_blockhouse"] = 1, ["auth_rifleman"] = 1, ["auth_tank"] = 1,
            ["auth_engineering"] = 2, ["auth_searchlight"] = 2, ["auth_decon_truck"] = 2, ["auth_flak"] = 2, ["auth_airfield"] = 2, ["auth_interceptor"] = 2,
            ["auth_signals_hq"] = 3, ["auth_bomber"] = 3, ["auth_bulwark"] = 3, ["auth_atomic_battery"] = 3,
            ["dir_shaft"] = 1, ["dir_conscript"] = 1, ["dir_bore"] = 1, ["dir_redoubt"] = 1,
            ["dir_isotope_works"] = 2, ["dir_mast"] = 2, ["dir_sapper"] = 2, ["dir_dirigible"] = 2, ["dir_delver"] = 2,
            ["dir_committee_hall"] = 3, ["dir_sunrise"] = 3, ["dir_reactor_crawler"] = 3, ["dir_landtrain"] = 3, ["dir_great_dirigible"] = 3,
            ["auth_up_flak_vests"] = 1, ["auth_grouting"] = 2, ["auth_up_tungsten_shot"] = 3, ["dir_up_tunnel_bracing"] = 1, ["dir_up_fervour"] = 3,
        };
        foreach ((string id, int tier) in expected)
        {
            int actual = r.Buildings.Where(b => b.Id == id).Select(b => b.Tier)
                .Concat(r.Vehicles.Where(v => v.Id == id).Select(v => v.Tier))
                .Concat(r.Infantry.Where(i => i.Id == id).Select(i => i.Tier))
                .Concat(r.Upgrades.Where(u => u.Id == id).Select(u => u.Tier)).Single();
            Assert.True(tier == actual, $"{id}: design 08 §4 says tier {tier}, rules have {actual}");
        }
    }

    [Fact]
    public void Shallow_TerrainRow_IsLoaded()
    {
        TerrainTable t = ProductionFixture.Loaded.Sim.Terrain;
        Assert.Equal(new[] { 40, 0, 0, 100, 100, 0, 100 }, Enum.GetValues<Locomotion>().Select(l => t.SpeedPercent(LandType.Shallow, l)));
    }

    [Fact]
    public void SchemaV4_Keys_Parse()
    {
        using var temp = new TempRules(
            ("buildings.toml", TempRules.Real("buildings.toml").Replace("provides = [\"test_hq\"]",
                "provides = [\"test_hq\"]\ntier = 2\ngarrison = 4\ngarrison_range_bonus = 512\ngarrison_rof_permille = 600")),
            ("infantry.toml", TempRules.Real("infantry.toml") + "\n[test_sapper]\nbase = \"test_engineer\"\nengineer = true\ncan_garrison = true\ntier = 3\n"),
            ("warheads.toml", TempRules.RealWith("warheads.toml", "[test_he]", "[test_he]\nhits_burrowed = true\nbonus_vs_entrance_percent = 200\nfoam_ticks = 400\ngarrison_damage_percent = 30")),
            ("techs.toml", TempRules.Real("techs.toml") + "\n[[effect]]\nid = \"test_hot\"\nop = \"add\"\ntarget = \"warhead:test_he\"\nattr = \"contaminate\"\nvalue = 40\n"
                + "\n[[effect]]\nid = \"test_fast\"\nop = \"mul\"\ntarget = \"weapon:test_rifle\"\nattr = \"rate_of_fire\"\nvalue = 900\n"),
            ("upgrades.toml", TempRules.Real("upgrades.toml") + "tier = 2\neffects = [\"test_hot\", \"test_fast\"]\n"),
            ("game.toml", TempRules.Real("game.toml") + "\n[garrison]\neject_damage_permille = 300\nenter_range_cells = 2\n\n[foam]\nspeed_permille = 250\n"));
        SimRules r = RulesLoader.Load(temp.Dir).Sim;
        BuildingType hq = r.Buildings[SimRules.IndexOf(r.Buildings, "test_hq", b => b.Id)];
        Assert.Equal((2, 4, 512, 600), (hq.Tier, hq.Garrison, hq.GarrisonRangeBonus, hq.GarrisonRofPermille));
        InfantryType sapper = r.Infantry[SimRules.IndexOf(r.Infantry, "test_sapper", i => i.Id)];
        Assert.True(sapper.Engineer && sapper.CanGarrison);
        Assert.Equal(3, sapper.Tier);
        WarheadType he = r.Warheads[SimRules.IndexOf(r.Warheads, "test_he", w => w.Id)];
        Assert.Equal((true, 200, 400, 30), (he.HitsBurrowed, he.BonusVsEntrancePercent, he.FoamTicks, he.GarrisonDamagePercent));
        UpgradeType up = r.Upgrades[0];
        Assert.Equal(2, up.Tier);
        Assert.Equal(new[] { "test_hot", "test_fast" }, up.Effects);
        Assert.Equal(new GarrisonRules(300, 2), r.Garrison);
        Assert.Equal(new FoamRules(250), r.Foam);
    }

    [Fact]
    public void SchemaV4_FieldsEnterTheRulesHash()
    {
        ulong baseHash = ProductionFixture.Loaded.RulesHash;
        using var tier = new TempRules(("vehicles.toml", AppendToFirstEntry("vehicles.toml", "tier = 2")));
        Assert.NotEqual(baseHash, RulesLoader.Load(tier.Dir).RulesHash);
        using var foam = new TempRules(("game.toml", TempRules.Real("game.toml") + "\n[foam]\nspeed_permille = 400\n"));
        Assert.NotEqual(baseHash, RulesLoader.Load(foam.Dir).RulesHash);
    }

    [Fact]
    public void GarrisonOnAnEntrance_IsALoadError()
    {
        using var temp = new TempRules(("buildings.toml", TempRules.Real("buildings.toml").Replace("provides = [\"test_hq\"]",
            "provides = [\"test_hq\"]\ngarrison = 2\nentrance = \"S\"")));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        Assert.Contains(ex.Problems, p => p.Key == "test_hq.garrison" && p.Message.Contains("entrance"));
    }

    [Theory]
    [InlineData("tier = 4", "tier")]
    [InlineData("garrison = 11", "garrison")]
    public void OutOfRangeV4Values_AreLoadErrors(string line, string key)
    {
        using var temp = new TempRules(("buildings.toml", TempRules.Real("buildings.toml").Replace("provides = [\"test_hq\"]", "provides = [\"test_hq\"]\n" + line)));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        Assert.Contains(ex.Problems, p => p.Key == "test_hq." + key);
    }

    [Theory]
    [InlineData("class:vehicle", "power")]          // a building attribute on units
    [InlineData("test_hq", "speed")]                 // a unit attribute on a building
    [InlineData("weapon:test_rifle", "contaminate")] // a warhead attribute on a weapon
    [InlineData("class:infantry", "armour")]         // not an attribute at all
    public void UnknownEffectAttr_IsALoadError(string target, string attr)
    {
        using var temp = new TempRules(("techs.toml",
            TempRules.Real("techs.toml") + $"\n[[effect]]\nid = \"test_bad\"\nop = \"add\"\ntarget = \"{target}\"\nattr = \"{attr}\"\nvalue = 1\n"));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        Assert.Contains(ex.Problems, p => p.Key == "test_bad.attr");
    }

    [Fact]
    public void UnknownWeaponTarget_AndUnknownUpgradeEffect_AreLoadErrors()
    {
        using var temp = new TempRules(
            ("techs.toml", TempRules.Real("techs.toml") + "\n[[effect]]\nid = \"test_bad\"\nop = \"add\"\ntarget = \"weapon:nope\"\nattr = \"damage\"\nvalue = 1\n"),
            ("upgrades.toml", TempRules.Real("upgrades.toml") + "effects = [\"no_such_effect\"]\n"));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        Assert.Contains(ex.Problems, p => p.Key == "test_bad.target" && p.Message.Contains("nope"));
        Assert.Contains(ex.Problems, p => p.Key == "auth_grouting.effects" && p.Message.Contains("no_such_effect"));
    }

    [Fact]
    public void TierSugar_AppendsTheFactionTierProvides()
    {
        // test_hq provides "test_hq" and becomes the tier-2 gate; test_power (tier 2) gets it appended; the gate does not get its own name.
        using var temp = new TempRules(
            ("game.toml", TempRules.RealWith("game.toml", "conyard = \"auth_conyard\"", "conyard = \"auth_conyard\"\ntier2 = \"test_hq\"\ntier3 = \"test_hq\"")),
            ("buildings.toml", TempRules.Real("buildings.toml").Replace("provides = [\"test_hq\"]", "provides = [\"test_hq\"]\ntier = 2")
                + "\n"),
            ("vehicles.toml", AppendToFirstEntry("vehicles.toml", "tier = 3")));
        RuleSet rules = RulesLoader.Load(temp.Dir);
        SimRules r = rules.Sim;
        BuildingType hq = r.Buildings[SimRules.IndexOf(r.Buildings, "test_hq", b => b.Id)];
        Assert.DoesNotContain("test_hq", hq.Prerequisites);
        Assert.Equal("test_hq", r.StartKits[(int)Faction.Authority].Tier2Provides);
        VehicleType first = r.Vehicles.Single(v => v.Tier == 3);
        Assert.Equal("test_hq", first.Prerequisites[^1]);
        Assert.Equal(1, first.Prerequisites.Count(p => p == "test_hq"));   // never appended twice
        Assert.All(r.Vehicles.Where(v => v.Tier == 1), v => Assert.Equal(ProductionFixture.Loaded.Sim.Vehicles.Single(o => o.Id == v.Id).Prerequisites, v.Prerequisites));
    }

    [Fact]
    public void UnknownTierProvides_IsALoadError()
    {
        using var temp = new TempRules(("game.toml", TempRules.RealWith("game.toml", "conyard = \"auth_conyard\"", "conyard = \"auth_conyard\"\ntier2 = \"no_such_gate\"")));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        Assert.Contains(ex.Problems, p => p.Key == "faction.authority.tier2");
    }

    // ---- maps -----------------------------------------------------------------------------------------

    [Fact]
    public void SymmetryImages_AreTheDocumentedTransforms()
    {
        Assert.Equal(new[] { (1, 2) }, MapSymmetries.Images(MapSymmetry.None, 10, 8, 1, 2));
        Assert.Equal(new[] { (1, 2), (8, 5) }, MapSymmetries.Images(MapSymmetry.Rot2, 10, 8, 1, 2));
        Assert.Equal(new[] { (1, 2), (8, 2) }, MapSymmetries.Images(MapSymmetry.MirrorX, 10, 8, 1, 2));
        Assert.Equal(new[] { (1, 2), (1, 5) }, MapSymmetries.Images(MapSymmetry.MirrorY, 10, 8, 1, 2));
        Assert.Equal(new[] { (1, 2), (2, 1) }, MapSymmetries.Images(MapSymmetry.MirrorDiag, 10, 10, 1, 2));
        Assert.Equal(new[] { (1, 2), (7, 1), (8, 7), (2, 8) }, MapSymmetries.Images(MapSymmetry.Rot4, 10, 10, 1, 2));
        Assert.Throws<ArgumentException>(() => MapSymmetries.Images(MapSymmetry.Rot4, 10, 8, 1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapSymmetries.Images(MapSymmetry.Rot2, 10, 8, 10, 2));
    }

    [Theory]
    [InlineData(MapSymmetry.None)]
    [InlineData(MapSymmetry.Rot2)]
    [InlineData(MapSymmetry.Rot4)]
    [InlineData(MapSymmetry.MirrorX)]
    [InlineData(MapSymmetry.MirrorY)]
    [InlineData(MapSymmetry.MirrorDiag)]
    public void SymmetryImages_FormAGroup_OnEveryCell(MapSymmetry s)
    {
        const int n = 7;   // odd, so the axes and the centre are cells
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var images = MapSymmetries.Images(s, n, n, x, y);
                Assert.Equal(MapSymmetries.Order(s), images.Length);
                Assert.Equal((x, y), images[0]);
                foreach (var (ix, iy) in images)
                {
                    Assert.InRange(ix, 0, n - 1); Assert.InRange(iy, 0, n - 1);
                    // closed: the images of an image are the same set
                    Assert.Equal(images.OrderBy(p => p).ToArray(), MapSymmetries.Images(s, n, n, ix, iy).OrderBy(p => p).ToArray());
                }
            }
    }

    [Fact]
    public void GenerateWithParams_DelegatesToLegacy_AndNamesTheMap()
    {
        var p = new MapGenParams(Biome.AshPlains, MapSize.Small, 2, 77);
        Rts.Sim.Map.MapData map = MapGenerator.Generate(p);
        Assert.Equal("ash_plains_s_2p_77", map.Name);
        Assert.Equal(64, map.Width);
        Assert.Equal(Rts.Sim.Map.MapHasher.Compute(MapGenerator.Generate(p)), Rts.Sim.Map.MapHasher.Compute(map)); // M4-02: procedural, no longer the legacy map
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGenerator.Generate(p with { Players = 5 }));
    }

    [Fact]
    public void MapFileV2_RoundTripsMeta_AndKeepsTheHash()
    {
        Rts.Sim.Map.MapData map = MapGenerator.Generate(32, 5);
        var meta = new MapMeta("rust_belt", MapSymmetry.Rot2, 2, new MapGenParams(Biome.RustBelt, MapSize.Medium, 2, ulong.MaxValue),
            new[] { new MapFeature("neu_ruin_depot", new CellCoord(10, 11)), new MapFeature("neu_ruin_tenement", new CellCoord(20, 3)) });
        string path = Temp("v2.map.toml");
        MapFile.Save(map, meta, path);
        Assert.Contains("format_version = 4", File.ReadAllText(path));   // M4-01 wrote 2, M7-02 3 (MapFormat3Tests covers a real version-2 file)
        (Rts.Sim.Map.MapData back, MapMeta backMeta) = MapFile.LoadWithMeta(path);
        Assert.Equal(Rts.Sim.Map.MapHasher.Compute(map), Rts.Sim.Map.MapHasher.Compute(back));
        Assert.Equal(meta.Biome, backMeta.Biome);
        Assert.Equal(meta.Symmetry, backMeta.Symmetry);
        Assert.Equal(meta.MaxPlayers, backMeta.MaxPlayers);
        Assert.Equal(meta.Generator, backMeta.Generator);
        Assert.Equal(meta.Features, backMeta.Features);
    }

    [Fact]
    public void MapFileV1_StillLoads_WithDefaultMeta()
    {
        string path = Path.Combine(TestData.MapsDir, "test_32.map.toml");
        Assert.Contains("format_version = 1", File.ReadAllText(path));
        (Rts.Sim.Map.MapData map, MapMeta meta) = MapFile.LoadWithMeta(path);
        Assert.Equal(MapMeta.DefaultFor(map) with { Features = meta.Features }, meta);
        Assert.Empty(meta.Features);
        Assert.Equal(map.StartPositions.Count, meta.MaxPlayers);
        string v3 = Temp("v5.map.toml");   // a future version
        File.WriteAllText(v3, File.ReadAllText(path).Replace("format_version = 1", "format_version = 5"));
        Assert.Throws<MapFileException>(() => MapFile.Load(v3));
    }

    [Fact]
    public void ShallowLand_IsAValidMapByte()
    {
        var flat = Rts.Sim.Map.MapData.CreateFlat(4, 4);
        var land = new LandType[16];
        land[5] = LandType.Shallow;
        string path = Temp("shallow.map.toml");
        MapFile.Save(new Rts.Sim.Map.MapData("s", "rust_belt", 4, 4, new ushort[16], land, new byte[16], new OverlayKind[16], new byte[16], flat.StartPositions), path);
        Assert.Equal(LandType.Shallow, MapFile.Load(path).Land[5]);
    }

    [Fact]
    public void MapFeaturesPlace_UnknownTypePlacesNothing()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        ulong before = sim.ComputeStateHash();
        var meta = new MapMeta("", MapSymmetry.None, 2, null, new[] { new MapFeature("x", new CellCoord(1, 1)) });
        Assert.True(MapFeatures.Place(sim, meta, 1).Single().IsNone);   // M4-15: unknown type -> None (was: stub returned empty)
        Assert.Equal(before, sim.ComputeStateHash());
    }

    // ---- helpers ----------------------------------------------------------------------------------------

    private static Rts.Sim.Map.MapData MapData() => Rts.Sim.Map.MapData.CreateFlat(16, 16);

    private static string Temp(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts_m4_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name);
    }

    /// <summary>The fixture file with <paramref name="line"/> added as the last key of its first [entry].</summary>
    private static string AppendToFirstEntry(string file, string line)
    {
        string text = TempRules.Real(file);
        int first = text.IndexOf("\n[", StringComparison.Ordinal);
        int second = text.IndexOf("\n[", first + 1, StringComparison.Ordinal);
        return second < 0 ? text + "\n" + line + "\n" : text[..second] + "\n" + line + text[second..];
    }
}
