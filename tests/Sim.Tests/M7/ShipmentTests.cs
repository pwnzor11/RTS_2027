// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Shipments;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Rules;
using Rts.Sim.View;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-06: the home supply line (shipments, tenders, decay, charter) on a hand-made sea: water in the west (x &lt; 10), land east of it.</summary>
public sealed class ShipmentTests
{
    private const int Dock = 0, Refinery = 1;   // building indexes (sorted by id: "dock", "refinery")
    private const int Tender = 0, Harvester = 1;

    private static ShipmentRules Rules(bool enabled = true, string tender = "tender") => new()
    {
        Enabled = enabled, TenderType = tender, IntervalTicks = 100, Value = 1000, DecayPermille = 300, FloorPermille = 350,
        CharterTicks = 0, TendersMax = 5,
    };

    private static TerrainTable Terrain()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        Array.Fill(speed, 100);
        for (int l = 0; l < RuleCounts.Locomotions; l++) speed[(int)LandType.Water * RuleCounts.Locomotions + l] = 0;
        for (int land = 0; land < RuleCounts.LandTypes; land++) speed[land * RuleCounts.Locomotions + (int)Locomotion.Naval] = 0;
        speed[(int)LandType.Water * RuleCounts.Locomotions + (int)Locomotion.Naval] = 100;
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        return new TerrainTable(speed, buildable);
    }

    private static MapData Sea(bool edgeWater = true)
    {
        const int w = 40, h = 20;
        var land = new LandType[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool lake = x >= 5 && x < 10 && y >= 5 && y < 15;
                if (edgeWater ? x < 10 : lake) land[y * w + x] = LandType.Water;
            }
        var overlay = new OverlayKind[w * h];
        var dens = new byte[w * h];
        for (int y = 8; y < 11; y++)
            for (int x = 20; x < 23; x++) { overlay[y * w + x] = OverlayKind.Ashfall; dens[y * w + x] = 12; }
        return new MapData("sea", "rust_belt", w, h, new ushort[w * h], land, new byte[w * h], overlay, dens,
            new[] { new CellCoord(30, 2), new CellCoord(30, 17) });
    }

    private static SimSetup Setup(ShipmentRules ship, bool edgeWater = true, int tenderSpeed = 128)
    {
        var tender = new VehicleType("tender", Locomotion.Naval, tenderSpeed, 8, 100, ArmorClass.Heavy, 5, -1, false, 0, false, 0, Array.Empty<string>());
        VehicleType harv = new VehicleType("harv", Locomotion.Tracked, 40, 8, 200, ArmorClass.Heavy, 5, -1, false, 0, false, 0, Array.Empty<string>()) with { HarvestCapacity = 10 };
        BuildingType[] buildings =
        {
            ScriptedGame.Building("dock", 2, 2, 300) with { ShipmentDock = true },
            ScriptedGame.Building("refinery", 3, 3, 500) with { Refinery = true },
        };
        var rules = new SimRules(new[] { tender, harv }, Array.Empty<InfantryType>(), buildings, Array.Empty<WeaponType>(),
            Array.Empty<WarheadType>(), Terrain(), Array.Empty<EffectDef>()) { Shipments = ship };
        return new SimSetup(1, Sea(edgeWater), 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    private static Simulation NewSim(SimSetup setup, bool dock = true, bool refinery = false)
    {
        var sim = new Simulation(setup);
        if (dock) Assert.False(sim.PlaceBuilding(1, Dock, new CellCoord(10, 10)).IsNone);
        if (refinery)
        {
            Assert.False(sim.PlaceBuilding(1, Refinery, new CellCoord(18, 12)).IsNone);
            sim.SpawnVehicle(1, Harvester, new CellCoord(19, 16));
        }
        return sim;
    }

    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++) sim.Step(Array.Empty<Command>());
    }

    [Fact]
    public void Off_ChangesNothing()
    {
        var plain = NewSim(Setup(Rules(enabled: false)));
        var noTender = NewSim(Setup(Rules(tender: "")));
        var plainFlag = NewSim(Setup(new ShipmentRules()));
        Run(plain, 400); Run(noTender, 400); Run(plainFlag, 400);
        Assert.Empty(plain.Shipments.Tenders);
        Assert.Equal(plain.ComputeStateHash(), noTender.ComputeStateHash());
        Assert.Equal(plain.ComputeStateHash(), plainFlag.ComputeStateHash());
        Assert.All(plain.Shipments.NextShipmentTick, x => Assert.Equal(0, x));
        Assert.Equal(0, plain.StatsOf(1).ShipmentIncome);
    }

    [Fact]
    public void Payload_DecaysPerShipment_AndStopsAtTheFloor()
    {
        var sim = NewSim(Setup(Rules(), tenderSpeed: 1));
        var seen = new List<int>();
        int lastId = -1;
        for (int i = 0; i < 560; i++)
        {
            sim.Step(Array.Empty<Command>());
            foreach (Rts.Sim.Shipments.Tender t in sim.Shipments.Tenders)
                if (t.Id > lastId) { lastId = t.Id; seen.Add(t.Payload); }
        }
        Assert.Equal(new[] { 1000, 700, 400, 350, 350 }, seen);
        Assert.Equal(3, sim.Shipments.DecayStep[1]);   // capped where the floor is reached: ceil(650 / 300) = 3 steps
    }

    [Fact]
    public void Tender_ArrivesAndPays_CountedAsShipmentIncome()
    {
        var sim = NewSim(Setup(Rules() with { TendersMax = 1, IntervalTicks = 500 }));
        int cash = sim.Players.Cash[1];
        Run(sim, 499);
        Assert.Empty(sim.Shipments.Tenders);
        Run(sim, 2);
        Assert.Single(sim.Shipments.Tenders);
        Assert.Equal(cash, sim.Players.Cash[1]);
        Run(sim, 300);
        Assert.Empty(sim.Shipments.Tenders);
        Assert.Equal(cash + 1000, sim.Players.Cash[1]);
        Assert.Equal(1000, sim.StatsOf(1).ShipmentIncome);
        Assert.Equal(0, sim.StatsOf(0).ShipmentIncome);
        Assert.Equal(0, sim.StatsOf(1).UnitsLost);
    }

    [Fact]
    public void Charter_WinsBackADecayStep_OnlyWhileAFieldIsWorked()
    {
        var withField = NewSim(Setup(Rules() with { CharterTicks = 200, IntervalTicks = 100_000 }), refinery: true);
        var without = NewSim(Setup(Rules() with { CharterTicks = 200, IntervalTicks = 100_000 }));
        withField.Shipments.DecayStep[1] = 3;
        without.Shipments.DecayStep[1] = 3;
        Run(withField, 700); Run(without, 700);
        Assert.True(withField.Shipments.DecayStep[1] < 3, $"step {withField.Shipments.DecayStep[1]}");
        Assert.Equal(3, without.Shipments.DecayStep[1]);
    }

    [Fact]
    public void SunkTender_PaysNothing()
    {
        var sim = NewSim(Setup(Rules() with { TendersMax = 1 }));
        Run(sim, 105);
        Rts.Sim.Shipments.Tender t = Assert.Single(sim.Shipments.Tenders);
        int cash = sim.Players.Cash[1];
        sim.Units.Health[(ushort)((uint)t.Vehicle >> 16)] = 0;
        Run(sim, 90);   // before the next shipment sails (tick 200)
        Assert.Equal(cash, sim.Players.Cash[1]);
        Assert.Equal(0, sim.StatsOf(1).ShipmentIncome);
        Assert.Equal(1, sim.StatsOf(1).UnitsLost);
        Assert.DoesNotContain(sim.Shipments.Tenders, x => x.Id == t.Id);
    }

    [Fact]
    public void TendersMax_IsRespected()
    {
        var sim = NewSim(Setup(Rules() with { IntervalTicks = 20, TendersMax = 2 }, tenderSpeed: 1));
        int peak = 0;
        for (int i = 0; i < 400; i++)
        {
            sim.Step(Array.Empty<Command>());
            peak = Math.Max(peak, sim.Shipments.Tenders.Count);
        }
        Assert.Equal(2, peak);
        Assert.Equal(2, sim.Shipments.Tenders.Count);
    }

    [Fact]
    public void NoEdgeWater_NoShipment()
    {
        var sim = NewSim(Setup(Rules(), edgeWater: false));
        Run(sim, 500);
        Assert.Empty(sim.Shipments.Tenders);
        Assert.Equal(0, sim.Shipments.DecayStep[1]);
    }

    [Fact]
    public void Spawn_RotatesAlongTheEdge()
    {
        var sim = NewSim(Setup(Rules(), tenderSpeed: 1));
        Run(sim, 320);
        var cells = sim.Shipments.Tenders.Select(t => (t.CellX, t.CellY)).ToArray();
        Assert.Equal(3, cells.Length);
        Assert.Equal(3, cells.Distinct().Count());
    }

    [Fact]
    public void SaveLoad_MidVoyage_ReproducesTheHash()
    {
        SimSetup setup = Setup(Rules() with { CharterTicks = 200 }, tenderSpeed: 32);
        var sim = NewSim(setup, refinery: true);
        Run(sim, 130);
        Assert.NotEmpty(sim.Shipments.Tenders);
        var ms = new MemoryStream();
        sim.Save(ms);
        Simulation loaded = Simulation.Load(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        for (int i = 0; i < 400; i++)
        {
            sim.Step(Array.Empty<Command>());
            loaded.Step(Array.Empty<Command>());
            Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.True(sim.StatsOf(1).ShipmentIncome > 0);
        Assert.Equal(sim.StatsOf(1).ShipmentIncome, loaded.StatsOf(1).ShipmentIncome);
    }

    [Fact]
    public void PlayerView_ShowsOwnEtaDecayAndTenders()
    {
        var sim = NewSim(Setup(Rules(), tenderSpeed: 1));
        Run(sim, 1);
        PlayerView v = sim.ViewFor(1);
        Assert.Equal(99, v.ShipmentEtaTicks);
        Assert.Equal(1000, v.NextShipmentPayload);
        Run(sim, 110);
        v = sim.ViewFor(1);
        Assert.Equal(1, v.ShipmentDecayStep);
        Assert.Equal(700, v.NextShipmentPayload);
        var list = new List<TenderView>();
        v.CopyOwnTenders(list);
        Assert.Equal(1000, Assert.Single(list).Payload);
        sim.ViewFor(0).CopyOwnTenders(list);
        Assert.Empty(list);
        Assert.Equal(-1, sim.ViewFor(0).ShipmentEtaTicks);
    }

    // ---- rules --------------------------------------------------------------------------------------------

    private static RuleSet Load(params (string File, string? Content)[] overrides)
    {
        using var temp = new TempRules(overrides);
        return RulesLoader.Load(temp.Dir);
    }

    private static (string, string?) Game(string body) => ("game.toml", TempRules.Real("game.toml") + "\n[shipments]\n" + body + "\n");

    private static (string, string?) Veh(string body) =>
        ("vehicles.toml", TempRules.Real("vehicles.toml") + "\n[test_ship]\nbase = \"test_tank\"\n" + body + "\n");

    private static (string, string?) Bld(string body) =>
        ("buildings.toml", TempRules.Real("buildings.toml") + "\n[test_quay]\nbase = \"test_barracks\"\n" + body + "\n");

    [Fact]
    public void TenderType_MustBeAnUnarmedNavalVehicle()
    {
        const string Ship = "locomotion = \"naval\"\nprimary_weapon = \"\"";
        RuleSet ok = Load(Veh(Ship), Game("tender_type = \"test_ship\""));
        Assert.Equal("test_ship", ok.Sim.Shipments.TenderType);
        Assert.Equal("", Load().Sim.Shipments.TenderType);
        Assert.Throws<RulesException>(() => Load(Veh(Ship), Game("tender_type = \"nope\"")));
        Assert.Throws<RulesException>(() => Load(Veh(Ship), Game("tender_type = \"test_tank\"")));            // tracked
        Assert.Throws<RulesException>(() => Load(Veh("locomotion = \"naval\""), Game("tender_type = \"test_ship\"")));   // armed (base tank)
        Assert.NotEqual(ok.RulesHash, Load(Veh(Ship), Game("tender_type = \"\"")).RulesHash);
    }

    [Fact]
    public void ShipmentDock_ParsesHashesAndNeedsAWaterBuilding()
    {
        Assert.DoesNotContain(Load().Sim.Buildings, b => b.ShipmentDock);
        RuleSet on = Load(Bld("water_building = true\nshipment_dock = true"));
        RuleSet off = Load(Bld("water_building = true"));
        Assert.True(on.Sim.Buildings.Single(b => b.Id == "test_quay").ShipmentDock);
        Assert.NotEqual(on.RulesHash, off.RulesHash);
        Assert.Throws<RulesException>(() => Load(Bld("shipment_dock = true")));
        Assert.Throws<RulesException>(() => Load(Bld("water_building = true\nshipment_dock = 3")));
    }
}
