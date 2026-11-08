// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Replay;
using Rts.Sim.Rules;
using Rts.Sim.Serialization;
using Rts.Sim.Shipments;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Rules;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-02 contracts (docs/design/11_m7_plan.md §2): enum values, the rules schema v7 fields with "off" defaults, the "shipments",
/// "beachhead" and "naval" parts and the format versions. No gameplay behaviour is tested here: none exists yet.</summary>
public sealed class M7ContractTests
{
    [Fact]
    public void EnumValues_ArePinned()
    {
        Assert.Equal(4, (int)Faction.Expedition);
        Assert.Equal(5, RuleCounts.Factions);
        Assert.Equal(RuleCounts.Factions, Enum.GetValues<Faction>().Length);
        Assert.Equal(4, RuleCounts.PlayableFactions);   // M7-14: the Expedition is offered in setup
        Assert.Contains(Faction.Expedition, RuleCounts.PlayableOrder);
        Assert.True(RuleCounts.IsPlayable(Faction.Expedition));
        Assert.Equal(6, (int)Locomotion.Naval);
        Assert.Equal(7, RuleCounts.Locomotions);
        Assert.Equal(RuleCounts.Locomotions, Enum.GetValues<Locomotion>().Length);
        Assert.Equal(8, (int)LandType.Coast);
        Assert.Equal(9, RuleCounts.LandTypes);
        Assert.Equal(RuleCounts.LandTypes, Enum.GetValues<LandType>().Length);
        Assert.Equal((5, 6, 7), ((int)ArmorClass.ShipLight, (int)ArmorClass.ShipHeavy, (int)ArmorClass.Submerged));
        Assert.Equal(8, RuleCounts.ArmorClasses);
        Assert.Equal(RuleCounts.ArmorClasses, Enum.GetValues<ArmorClass>().Length);
        Assert.Equal((3, 4, 5), ((int)WeaponTargets.Naval, (int)WeaponTargets.Submerged, (int)WeaponTargets.NavalAndSubmerged));
        Assert.Equal((19, 20, 21, 22), ((int)MissionKind.Unload, (int)MissionKind.Submerge, (int)MissionKind.Sortie, (int)MissionKind.ConvoySail));
        Assert.Equal(26, Enum.GetValues<Rts.Sim.Core.CommandKind>().Length);   // M7 adds no CommandKind; M11-10i added Hold = 25
    }

    [Fact]
    public void Terrain_HasCoastWithLandingAndNavalColumn()
    {
        foreach (RuleSet rules in new[] { RulesLoader.Load(TestData.RulesDir), RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!) })
        {
            TerrainTable t = rules.Sim.Terrain;
            Assert.True(t.Landing(LandType.Coast));
            Assert.False(t.Landing(LandType.Clear));
            Assert.False(t.Landing(LandType.Shallow));
            Assert.Equal(100, t.SpeedPercent(LandType.Water, Locomotion.Naval));
            Assert.Equal(100, t.SpeedPercent(LandType.Shallow, Locomotion.Naval));
            Assert.Equal(0, t.SpeedPercent(LandType.Coast, Locomotion.Naval));
            foreach (LandType l in new[] { LandType.Clear, LandType.Road, LandType.Rough, LandType.Ash, LandType.Rock, LandType.Cliff })
                Assert.Equal(0, t.SpeedPercent(l, Locomotion.Naval));
            foreach (Locomotion loco in new[] { Locomotion.Foot, Locomotion.Wheeled, Locomotion.Tracked })
                Assert.Equal(t.SpeedPercent(LandType.Clear, loco), t.SpeedPercent(LandType.Coast, loco));
        }
    }

    // ---- rules fields (schema v8) --------------------------------------------------------------------------

    private static RuleSet Load(params (string File, string? Content)[] overrides)
    {
        using var temp = new TempRules(overrides);
        return RulesLoader.Load(temp.Dir);
    }

    private static (string, string?) Game(string shipments) => ("game.toml", TempRules.Real("game.toml") + "\n[shipments]\n" + shipments + "\n");

    private static (string, string?) Veh(string body) =>
        ("vehicles.toml", TempRules.Real("vehicles.toml") + "\n[test_ship]\nbase = \"test_tank\"\n" + body + "\n");

    private static (string, string?) Bld(string body) =>
        ("buildings.toml", TempRules.Real("buildings.toml") + "\n[test_quay]\nbase = \"test_barracks\"\n" + body + "\n");

    private static VehicleType Ship(RuleSet r) => r.Sim.Vehicles.Single(v => v.Id == "test_ship");
    private static BuildingType Quay(RuleSet r) => r.Sim.Buildings.Single(b => b.Id == "test_quay");

    [Fact]
    public void Shipments_DefaultToOff_AndTheDesignTable()
    {
        ShipmentRules s = Load().Sim.Shipments;
        Assert.False(s.Enabled);
        Assert.Equal((1800, 1500, 40, 350), (s.IntervalTicks, s.Value, s.DecayPermille, s.FloorPermille));
        Assert.Equal((1200, 3, 0), (s.CharterTicks, s.TendersMax, s.LocalAshBonusPermille));
    }

    [Fact]
    public void ShippedGameToml_HasTheShipmentsBlock_Enabled()
    {
        ShipmentRules s = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!).Sim.Shipments;
        Assert.True(s.Enabled);   // M7-10: switched on (only games with an exp_anchorage dock are affected)
        Assert.Equal(ShipmentRules.Default with { Enabled = true, TenderType = "exp_supply_tender", IntervalTicks = 600, Value = 2000, DecayPermille = 60 }, s);   // S11 (m7_income.md): interval 600, value 2000, decay 60   // M7-08 names the convoy ship
    }

    [Fact]
    public void ShipmentsTable_Parses()
    {
        ShipmentRules s = Load(Game("""
            enabled = true
            interval_ticks = 900
            value = 2000
            decay_permille = 50
            floor_permille = 300
            charter_ticks = 600
            tenders_max = 2
            local_ash_bonus_permille = 150
            """)).Sim.Shipments;
        Assert.True(s.Enabled);
        Assert.Equal((900, 2000, 50, 300), (s.IntervalTicks, s.Value, s.DecayPermille, s.FloorPermille));
        Assert.Equal((600, 2, 150), (s.CharterTicks, s.TendersMax, s.LocalAshBonusPermille));
    }

    [Theory]
    [InlineData("enabld = true")]
    [InlineData("enabled = 1")]
    [InlineData("interval_ticks = 0")]
    [InlineData("value = -1")]
    [InlineData("decay_permille = 1001")]
    [InlineData("floor_permille = 1001")]
    [InlineData("charter_ticks = -5")]
    [InlineData("tenders_max = 33")]
    [InlineData("local_ash_bonus_permille = -1")]
    public void BadShipmentsTables_AreRejected(string line) => Assert.Throws<RulesException>(() => Load(Game(line)));

    [Fact]
    public void VehicleFields_DefaultToOff()
    {
        VehicleType t = Load().Sim.Vehicles.Single(v => v.Id == "test_tank");
        Assert.Equal(1000, t.DrawScalePermille);
        Assert.False(t.DeepOnly);
        Assert.False(t.Submerges);
        Assert.Equal((0, 0, 0, 0), (t.SonarRadius, t.AircraftSlots, t.SortieTicks, t.UnloadTicks));
    }

    [Fact]
    public void VehicleFields_Parse()
    {
        VehicleType v = Ship(Load(Veh("""
            locomotion = "naval"
            draw_scale_permille = 2500
            deep_only = true
            submerges = true
            sonar_radius = 9
            aircraft_slots = 4
            sortie_ticks = 60
            unload_ticks = 8
            """)));
        Assert.Equal(Locomotion.Naval, v.Locomotion);
        Assert.Equal(2500, v.DrawScalePermille);
        Assert.True(v.DeepOnly);
        Assert.True(v.Submerges);
        Assert.Equal((9, 4, 60, 8), (v.SonarRadius, v.AircraftSlots, v.SortieTicks, v.UnloadTicks));
    }

    [Theory]
    [InlineData("draw_scale_permille = 99")]
    [InlineData("draw_scale_permille = 10001")]
    [InlineData("draw_scale_permille = \"big\"")]
    [InlineData("deep_only = 1")]
    [InlineData("deep_only = true")]                                   // not naval
    [InlineData("submerges = true")]                                   // not naval
    [InlineData("sonar_radius = -1")]
    [InlineData("aircraft_slots = 33")]
    [InlineData("aircraft_slots = 2")]                                 // a carrier needs sortie_ticks
    [InlineData("sortie_ticks = -1")]
    [InlineData("unload_ticks = 1000001")]
    [InlineData("locomotion = \"sail\"")]
    public void BadVehicleFields_AreRejected(string line) => Assert.Throws<RulesException>(() => Load(Veh(line)));

    [Fact]
    public void WeaponTargets_AcceptTheNavalValues()
    {
        string w = TempRules.Real("weapons.toml");
        Assert.Equal(WeaponTargets.Ground, Load().Sim.Weapons.Single(x => x.Id == "test_mg").Targets);
        foreach ((string key, WeaponTargets want) in new[] { ("naval", WeaponTargets.Naval), ("submerged", WeaponTargets.Submerged),
                     ("naval_submerged", WeaponTargets.NavalAndSubmerged), ("ground", WeaponTargets.Ground), ("both", WeaponTargets.Both) })
        {
            RuleSet r = Load(("weapons.toml", w + "\n[test_torp]\nbase = \"test_mg\"\ntargets = \"" + key + "\"\n"));
            Assert.Equal(want, r.Sim.Weapons.Single(x => x.Id == "test_torp").Targets);
        }
        Assert.Throws<RulesException>(() => Load(("weapons.toml", w + "\n[test_torp]\nbase = \"test_mg\"\ntargets = \"underwater\"\n")));
    }

    [Fact]
    public void ShortWarheadVersusLists_PadTheShipClassesWith100()
    {
        WarheadType w = Load().Sim.Warheads.Single(x => x.Id == "test_ap");
        Assert.Equal(RuleCounts.ArmorClasses, w.VersusPercent.Count);
        Assert.Equal(new[] { 25, 75, 100, 50, 40, 100, 100, 100 }, w.VersusPercent);
        string wh = TempRules.Real("warheads.toml");
        RuleSet r = Load(("warheads.toml", wh.Replace("versus = [25, 75, 100, 50, 40]", "versus = [25, 75, 100, 50, 40, 60, 70, 80]")));
        Assert.Equal(new[] { 25, 75, 100, 50, 40, 60, 70, 80 }, r.Sim.Warheads.Single(x => x.Id == "test_ap").VersusPercent);
        RuleSet seven = Load(("warheads.toml", wh.Replace("versus = [25, 75, 100, 50, 40]", "versus = [25, 75, 100, 50, 40, 60, 70]")));
        Assert.Equal(new[] { 25, 75, 100, 50, 40, 60, 70, 100 }, seven.Sim.Warheads.Single(x => x.Id == "test_ap").VersusPercent);
        Assert.Throws<RulesException>(() => Load(("warheads.toml", wh.Replace("versus = [25, 75, 100, 50, 40]", "versus = [25, 75, 100, 50, 40, 60, 70, 80, 90]"))));
        Assert.Throws<RulesException>(() => Load(("warheads.toml", wh.Replace("versus = [25, 75, 100, 50, 40]", "versus = [25, 75, 100, 50]"))));
    }

    [Fact]
    public void ShippedWarheads_HaveFullVersusLists()
    {
        RuleSet r = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
        Assert.All(r.Sim.Warheads, w => Assert.Equal(RuleCounts.ArmorClasses, w.VersusPercent.Count));
        Assert.All(r.Sim.Warheads, w => Assert.True(w.VersusPercent[(int)ArmorClass.Submerged] is 0 or 100));
    }

    [Fact]
    public void BuildingFields_DefaultToOff()
    {
        BuildingType b = Load().Sim.Buildings.Single(x => x.Id == "test_barracks");
        Assert.False(b.WaterBuilding);
        Assert.Empty(b.WaterMask);
        Assert.False(b.BeachheadSource);
        Assert.Equal(0, b.BeachheadRadius);
    }

    [Fact]
    public void BuildingFields_Parse()
    {
        BuildingType b = Quay(Load(Bld("water_building = true\nwater = [0, 1, 0, 1]\nbeachhead_source = true\nbeachhead_radius = 8")));
        Assert.True(b.WaterBuilding);
        Assert.Equal(new[] { false, true, false, true }, b.WaterMask);
        Assert.True(b.BeachheadSource);
        Assert.Equal(8, b.BeachheadRadius);
        BuildingType noMask = Quay(Load(Bld("water_building = true")));
        Assert.True(noMask.WaterBuilding);
        Assert.Empty(noMask.WaterMask);
    }

    [Theory]
    [InlineData("water_building = 1")]
    [InlineData("water = [0, 1, 0, 1]")]                                          // a mask needs water_building
    [InlineData("water_building = true\nwater = [0, 1, 0]")]                      // wrong length (footprint is 2x2)
    [InlineData("water_building = true\nwater = [0, 2, 0, 0]")]                   // not 0/1
    [InlineData("water_building = true\nwater = [0, 0, 0, 0]")]                   // no water cell
    [InlineData("beachhead_source = true")]                                       // needs a radius
    [InlineData("beachhead_radius = 6")]                                          // needs the source flag
    [InlineData("beachhead_source = true\nbeachhead_radius = -1")]
    [InlineData("beachhead_source = true\nbeachhead_radius = 100000")]
    public void BadBuildingFields_AreRejected(string body) => Assert.Throws<RulesException>(() => Load(Bld(body)));

    [Fact]
    public void ExpeditionKit_IsTheShippedDataKit()
    {
        // The fixture set has no [faction.expedition] table: an Expedition start is refused clearly.
        RuleSet none = Load();
        Assert.Throws<InvalidOperationException>(() => none.Sim.ResolveStartKit(Faction.Expedition));
        // M7-14: the shipped [faction.expedition] table resolves to the data kit (a Command Ship that deploys, six units), not the placeholder.
        RuleSet shipped = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
        (int conyard, var units) = shipped.Sim.ResolveStartKit(Faction.Expedition);
        Assert.Equal(-1, conyard);
        Assert.Equal(6, units.Count);
        Assert.Equal(RuleCounts.Factions, shipped.Sim.StartKits.Count);
        Assert.Equal(Faction.Expedition, SimRules.FactionFromId("exp_marine"));
        Assert.Equal(Faction.Authority, SimRules.FactionFromId("auth_marine"));
    }

    [Fact]
    public void EveryNewRulesField_ChangesTheRulesHash()
    {
        Assert.Equal(11, RulesHash.FormatVersion);
        ulong baseline = Load().RulesHash;
        var seen = new HashSet<ulong> { baseline };
        void Add(RuleSet r, string what) => Assert.True(seen.Add(r.RulesHash), what + " does not change the rules hash");
        foreach (string line in new[]
        {
            "enabled = true", "interval_ticks = 1801", "value = 1501", "decay_permille = 41", "floor_permille = 351",
            "charter_ticks = 1201", "tenders_max = 4", "local_ash_bonus_permille = 1",
        })
            Add(Load(Game(line)), line);
        foreach (string line in new[]
        {
            "draw_scale_permille = 1001", "sonar_radius = 1", "sortie_ticks = 1", "unload_ticks = 1",
            "locomotion = \"naval\"", "locomotion = \"naval\"\ndeep_only = true", "locomotion = \"naval\"\nsubmerges = true",
            "aircraft_slots = 1\nsortie_ticks = 1",
        })
            Add(Load(Veh(line)), line);
        foreach (string body in new[]
        {
            "water_building = true", "water_building = true\nwater = [1, 0, 0, 0]", "water_building = true\nwater = [0, 1, 0, 0]",
            "beachhead_source = true\nbeachhead_radius = 6", "beachhead_source = true\nbeachhead_radius = 7",
        })
            Add(Load(Bld(body)), body);
        string wh = TempRules.Real("warheads.toml");
        Add(Load(("warheads.toml", wh.Replace("versus = [25, 75, 100, 50, 40]", "versus = [25, 75, 100, 50, 40, 100, 100, 99]"))), "submerged versus");
        string terrain = TempRules.Real("terrain.toml");
        Add(Load(("terrain.toml", terrain.Replace("[landing]\ncoast = true", "[landing]\ncoast = false"))), "landing column");
        Add(Load(("terrain.toml", terrain.Replace("hover = 80\nair = 100\nburrow = 100\nnaval = 0", "hover = 81\nair = 100\nburrow = 100\nnaval = 0"))), "coast speed");
    }

    [Fact]
    public void TerrainRequiresTheCoastLand_AndAcceptsTheLandingTable()
    {
        string terrain = TempRules.Real("terrain.toml");
        Assert.Throws<RulesException>(() => Load(("terrain.toml", terrain.Replace("[speed.coast]", "[speed.coastline]"))));
        Assert.Throws<RulesException>(() => Load(("terrain.toml", terrain.Replace("[landing]\ncoast = true", "[landing]\nbeach = true"))));
        Assert.Throws<RulesException>(() => Load(("terrain.toml", terrain.Replace("[landing]\ncoast = true", "[landing]\ncoast = 3"))));
        RuleSet noLanding = Load(("terrain.toml", terrain.Replace("[landing]\ncoast = true", "")));
        Assert.False(noLanding.Sim.Terrain.Landing(LandType.Coast));
        Assert.Throws<RulesException>(() => Load(("terrain.toml", terrain.Replace("coast = true\n\n# M3", "\n# M3"))));   // [buildable] needs coast
    }

    // ---- state parts and formats -----------------------------------------------------------------------------

    private static byte[] Bytes(ISimStatePart p)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) p.Write(w);
        return ms.ToArray();
    }

    [Fact]
    public void Formats_AndPartOrder_ArePinned()
    {
        Assert.Equal(13, SimWriter.SaveVersion);
        Assert.Equal(13, ReplayWriter.Version);
        Assert.Equal(7, ReplayWriter.OldestReadableVersion);
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        string[] names = sim.StateParts.Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "salvage", "veterancy", "shipments", "beachhead", "naval" }, names[^5..]);
        Assert.Same(sim.Shipments, sim.StateParts[^3]);
        Assert.Same(sim.Beachhead, sim.StateParts[^2]);
        Assert.Same(sim.Naval, sim.StateParts[^1]);
    }

    [Fact]
    public void NewParts_AreEmptyByDefault_AndRoundTripWhenFilled()
    {
        var setup = ScriptedGame.Setup();
        var sim = ScriptedGame.NewSim(setup);
        Assert.Empty(sim.Shipments.Tenders);
        Assert.All(sim.Shipments.NextShipmentTick, x => Assert.Equal(0, x));
        Assert.All(sim.Beachhead.Zone, row => Assert.All(row, x => Assert.Equal(0UL, x)));
        Assert.All(sim.Naval.NavalRegion, x => Assert.Equal(0, x));

        ulong empty = sim.ComputeStateHash();
        sim.Shipments.NextShipmentTick[1] = 1800; sim.Shipments.DecayStep[1] = 2; sim.Shipments.CharterProgress[1] = 77;
        sim.Shipments.Tenders.Add(new Tender(sim.Shipments.NextTenderId++, 1, 3, 4, 1440));
        sim.Beachhead.Zone[1][2] = 0xF0F0UL;
        sim.Naval.NavalRegion[5] = 3;
        sim.Naval.UnloadTicksLeft[1] = 6; sim.Naval.UnloadX[1] = 9; sim.Naval.UnloadY[1] = 8; sim.Naval.Submerged[2] = true;
        Assert.NotEqual(empty, sim.ComputeStateHash());

        var ms = new MemoryStream();
        sim.Save(ms);
        Simulation loaded = Simulation.Load(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(new Tender(0, 1, 3, 4, 1440), Assert.Single(loaded.Shipments.Tenders));
        Assert.Equal(1, loaded.Shipments.NextTenderId);
        Assert.Equal((1800, 2, 77), (loaded.Shipments.NextShipmentTick[1], loaded.Shipments.DecayStep[1], loaded.Shipments.CharterProgress[1]));
        Assert.True(loaded.Beachhead.Contains(1, 2 * 64 + 4));
        Assert.False(loaded.Beachhead.Contains(0, 2 * 64 + 4));
        Assert.Equal(3, loaded.Naval.NavalRegion[5]);
        Assert.Equal((6, 9, 8, true), (loaded.Naval.UnloadTicksLeft[1], loaded.Naval.UnloadX[1], loaded.Naval.UnloadY[1], loaded.Naval.Submerged[2]));
        foreach (ISimStatePart p in new ISimStatePart[] { loaded.Shipments, loaded.Beachhead, loaded.Naval })
            Assert.Equal(Bytes(p), Bytes(sim.StateParts.Single(q => q.Name == p.Name)));
    }

    [Fact]
    public void NavalVehicleSlots_AreClearedOnSpawn()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        for (int i = 1; i < sim.Naval.Submerged.Length; i++) { sim.Naval.Submerged[i] = true; sim.Naval.UnloadTicksLeft[i] = 4; }
        EntityId id = sim.SpawnVehicle(0, 0, new CellCoord(10, 10));
        Assert.False(id.IsNone);
        int slot = id.Index;
        Assert.False(sim.Naval.Submerged[slot]);
        Assert.Equal(0, sim.Naval.UnloadTicksLeft[slot]);
    }

    [Fact]
    public void Format11Save_IsRejectedAndFormat11ReplayStillReads()
    {
        var setup = ScriptedGame.Setup();
        var ms = new MemoryStream(); ScriptedGame.NewSim(setup).Save(ms);
        byte[] bytes = ms.ToArray();
        bytes[4] = 11;
        var ex = Assert.Throws<InvalidDataException>(() => Simulation.Load(new MemoryStream(bytes), setup));
        Assert.Contains("version 11", ex.Message);

        var sim = ScriptedGame.NewSim(setup);
        var rep = new MemoryStream();
        new ReplayWriter(rep, sim, "b", Array.Empty<(string, string)>()).Finish();
        byte[] r = rep.ToArray();
        Assert.Equal(13, ReplayReader.Read(new MemoryStream(r)).Version);
        r[ReplayWriter.Magic.Length] = 11;   // the version byte follows the magic
        Assert.Equal(11, ReplayReader.Read(new MemoryStream(r)).Version);
    }

    [Fact]
    public void ExpeditionPlayer_SetsUpAndSurvivesSaveAndReplayHeader()
    {
        var setup = ScriptedGame.Setup() with { Factions = new[] { Faction.Expedition, Faction.Authority } };
        var sim = ScriptedGame.NewSim(setup);
        var ms = new MemoryStream();
        sim.Save(ms);
        Assert.Equal(Faction.Expedition, Simulation.Load(new MemoryStream(ms.ToArray()), setup).Players.Faction[0]);
        var rep = new MemoryStream();
        new ReplayWriter(rep, sim, "b", Array.Empty<(string, string)>()).Finish();
        Assert.Equal(new[] { Faction.Expedition, Faction.Authority }, ReplayReader.Read(new MemoryStream(rep.ToArray())).Header.Factions);
    }
}
