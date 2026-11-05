// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Rules;

namespace Rts.Sim.Tests.Supply;

/// <summary>SUP-1 (docs/design/13_supply_cap.md, D-125): supply per unit, the cap from buildings, enqueue rejection, the queue pause, rigs,
/// free units, save/load and the rules (loader, hash). Fixture rules plus a test-only [supply] table; the fixture itself has none.</summary>
public sealed class SupplyTests
{
    private const ProductionCategory Inf = ProductionCategory.Infantry, Veh = ProductionCategory.Vehicle, Str = ProductionCategory.Structure;
    private static readonly Command[] None = Array.Empty<Command>();
    private static ushort _seq;

    /// <summary>Fixture rules with supply: HQ gives <paramref name="hq"/>, test_depot 10, test_still (rig, packs into test_crawler) 10.</summary>
    private static RuleSet Rules(string? supply = "enabled = true", int hq = 20)
    {
        string game = TempRules.Real("game.toml") + (supply is null ? "" : "\n[supply]\n" + supply + "\n");
        string buildings = TempRules.RealWith("buildings.toml", "provides = [\"test_hq\"]", "provides = [\"test_hq\"]\nsupply_provided = " + hq)
            + """

            [test_depot]
            footprint_w = 2
            footprint_h = 2
            max_health = 300
            armor = "structure"
            sight = 3
            primary_weapon = ""
            cost = 100
            power = 0
            prerequisites = ["test_hq"]
            provides = []
            supply_provided = 10

            [test_still]
            footprint_w = 2
            footprint_h = 2
            max_health = 300
            armor = "structure"
            sight = 3
            primary_weapon = ""
            cost = 100
            power = 0
            prerequisites = ["test_hq"]
            provides = []
            supply_provided = 10
            packs_into = "test_crawler"
            pack_ticks = 10
            """;
        string vehicles = TempRules.RealWith("vehicles.toml", "cost = 0\nprerequisites = []", "cost = 0\nprerequisites = []\nsupply = 0")
            .Replace("harvest_capacity = 20", "harvest_capacity = 20\nsupply = 1");
        using var temp = new TempRules(("game.toml", game), ("buildings.toml", buildings), ("vehicles.toml", vehicles));
        return RulesLoader.Load(temp.Dir);
    }

    private static int B(RuleSet r, string id) => SimRules.IndexOf(r.Sim.Buildings, id, x => x.Id);
    private static int V(RuleSet r, string id) => SimRules.IndexOf(r.Sim.Vehicles, id, x => x.Id);
    private static int I(RuleSet r, string id) => SimRules.IndexOf(r.Sim.Infantry, id, x => x.Id);

    private static SimSetup SetupOf(RuleSet r, bool debug = false) =>
        new(1, MapData.CreateFlat(40, 40), 2, r.Sim, r.RulesHash) { AllowDebug = debug };

    /// <summary>HQ (4,4), power (12,4), barracks (8,4), factory (4,8) for player 0, plus the given extra buildings.</summary>
    private static Simulation NewSim(RuleSet r, bool debug = false, params (string Id, int X, int Y)[] extra)
    {
        var sim = new Simulation(SetupOf(r, debug));
        foreach (var (id, x, y) in new[] { ("test_hq", 4, 4), ("test_power", 12, 4), ("test_barracks", 8, 4), ("test_factory", 4, 8) }.Concat(extra))
            Assert.False(sim.PlaceBuilding(0, B(r, id), new CellCoord(x, y)).IsNone, id);
        sim.Players.PowerOut[0] = 1000;
        sim.Players.Cash[0] = 100000;
        return sim;
    }

    private static Command Build(Simulation s, ProductionCategory c, int type) =>
        new(s.Tick, 0, _seq++, CommandKind.Build, default, default, default, (int)c, type);

    private static Command Place(Simulation s, int type, int x, int y) =>
        new(s.Tick, 0, _seq++, CommandKind.Place, default, default, new CellCoord(x, y), type);

    private static Command Sell(Simulation s, EntityId b) =>
        new(s.Tick, 0, _seq++, CommandKind.Sell, b, default, default, (int)ActorKind.Building);

    private static Command Pack(Simulation s, EntityId b) =>
        new(s.Tick, 0, _seq++, CommandKind.Pack, b, default, default, 0);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(None); }

    private static void Riflemen(Simulation s, RuleSet r, int n)
    {
        for (int k = 0; k < n; k++) s.SpawnInfantry(0, I(r, "test_rifleman"), new CellCoord(20 + k % 10, 20 + k / 10));
    }

    [Fact]
    public void BlockReason_SupplyCap_IsSix() => Assert.Equal(6, (int)QueueBlockReason.SupplyCap);

    [Fact]
    public void Bands_AndPerTypeOverride()
    {
        SupplyRules s = Rules().Sim.Supply;
        Assert.Equal(1, s.SupplyOf(0, -1));
        Assert.Equal(1, s.SupplyOf(400, -1));
        Assert.Equal(2, s.SupplyOf(401, -1));
        Assert.Equal(2, s.SupplyOf(900, -1));
        Assert.Equal(3, s.SupplyOf(901, -1));
        Assert.Equal(0, s.SupplyOf(5000, 0));
        Assert.Equal(1, s.SupplyOf(5000, 1));
        RuleSet r = Rules();
        Simulation sim = NewSim(r);
        Assert.Equal(1, sim.SupplyOfType(Inf, I(r, "test_rifleman")));       // 100
        Assert.Equal(2, sim.SupplyOfType(Inf, I(r, "test_engineer")));       // 500
        Assert.Equal(2, sim.SupplyOfType(Veh, V(r, "test_tank")));           // 800
        Assert.Equal(1, sim.SupplyOfType(Veh, V(r, "test_jeep")));           // 400
        Assert.Equal(2, sim.SupplyOfType(Veh, V(r, "test_artillery_truck")));   // 900
        Assert.Equal(1, sim.SupplyOfType(Veh, V(r, "test_harvester")));      // 600 but override 1
        Assert.Equal(0, sim.SupplyOfType(Veh, V(r, "test_crawler")));        // override 0
        Assert.Equal(0, sim.SupplyOfType(Str, B(r, "test_depot")));
    }

    [Fact]
    public void Cap_IsTheSumOfOwnBuildings_LimitedByTheCeiling()
    {
        RuleSet r = Rules();
        Simulation sim = NewSim(r, false, ("test_depot", 16, 16), ("test_depot", 20, 16));
        Assert.Equal(40, sim.SupplyCap(0));
        Assert.Equal(0, sim.SupplyCap(1));   // player 1 owns nothing
        RuleSet capped = Rules("enabled = true\nceiling = 25");
        Simulation sim2 = NewSim(capped, false, ("test_depot", 16, 16), ("test_depot", 20, 16));
        Assert.Equal(25, sim2.SupplyCap(0));
        Assert.Equal(new SupplyView(0, 25, 25), sim2.HudFor(0).Supply);
    }

    [Fact]
    public void Used_CountsEveryLiveInfantryAndVehicle()
    {
        RuleSet r = Rules();
        Simulation sim = NewSim(r);
        Riflemen(sim, r, 3);
        sim.SpawnVehicle(0, V(r, "test_tank"), new CellCoord(30, 30));
        sim.SpawnVehicle(0, V(r, "test_harvester"), new CellCoord(32, 30));
        sim.SpawnVehicle(0, V(r, "test_crawler"), new CellCoord(34, 30));
        Assert.Equal(3 + 2 + 1 + 0, sim.SupplyUsed(0));
        Assert.Equal(0, sim.SupplyUsed(1));
        Assert.Equal(new SupplyView(6, 20, 200), sim.HudFor(0).Supply);
    }

    [Fact]
    public void Cache_FollowsSpawnsAndRemovals_InsideOneTick()
    {
        RuleSet r = Rules();
        Simulation sim = NewSim(r);
        Assert.Equal(0, sim.SupplyUsed(0));
        EntityId a = sim.SpawnInfantry(0, I(r, "test_rifleman"), new CellCoord(20, 20));
        Assert.Equal(1, sim.SupplyUsed(0));
        sim.SpawnVehicle(0, V(r, "test_tank"), new CellCoord(30, 30));
        Assert.Equal(3, sim.SupplyUsed(0));
        sim.Infantry.Health[a.Index] = 0;
        Run(sim, 1);
        Assert.Equal(2, sim.SupplyUsed(0));
    }

    [Fact]
    public void PackingRig_StopsCountingAtOnce_AndAPackedRigContributesZero()
    {
        RuleSet r = Rules();
        Simulation sim = NewSim(r, false, ("test_still", 16, 16));
        EntityId still = sim.Map.BuildingAt(new CellCoord(16, 16));
        Assert.Equal(30, sim.SupplyCap(0));
        sim.Step(new[] { Pack(sim, still) });
        Assert.True(sim.Rigs.PackTicksLeft[still.Index] > 0);
        Assert.Equal(20, sim.SupplyCap(0));      // packing: the still no longer counts
        Run(sim, 12);
        Assert.False(sim.Buildings.Slots.IsAlive(still));
        Assert.Equal(20, sim.SupplyCap(0));      // packed: a vehicle, no cap
        Assert.Equal(0, sim.SupplyUsed(0));      // the packed rig (test_crawler) has supply 0
    }

    [Fact]
    public void Enqueue_IsRejectedAtTheCap_ButNotStructuresOrFreeTypes()
    {
        RuleSet r = Rules(hq: 3);
        Simulation sim = NewSim(r);
        int rifle = I(r, "test_rifleman"), tank = V(r, "test_tank");
        Assert.Equal(QueueBlockReason.None, sim.QueueBlockReason(0, Inf, rifle));
        sim.Step(new[] { Build(sim, Inf, rifle), Build(sim, Veh, tank) });      // 1 + 2 = 3
        Assert.Equal(1, sim.Production.QueueLength(0, Inf));
        Assert.Equal(1, sim.Production.QueueLength(0, Veh));
        Assert.Equal(QueueBlockReason.SupplyCap, sim.QueueBlockReason(0, Inf, rifle));
        Assert.False(sim.CanBuild(0, Inf, rifle));
        sim.Step(new[] { Build(sim, Inf, rifle) });
        Assert.Equal(1, sim.Production.QueueLength(0, Inf));
        Assert.Contains(sim.HudFor(0).Options, o => o.Category == Inf && o.TypeIndex == rifle && o.Blocked == QueueBlockReason.SupplyCap);
        // structures are never blocked; a supply-0 unit (the crawler is not buildable here) needs no room
        Assert.Equal(QueueBlockReason.None, sim.QueueBlockReason(0, Str, B(r, "test_depot")));
    }

    [Fact]
    public void QueueHead_Pauses_WhenTheCapDrops_AndResumesWhenASupplyBuildingCompletes()
    {
        RuleSet r = Rules(hq: 2);
        Simulation sim = NewSim(r, debug: true, ("test_depot", 16, 16));   // cap 12
        Riflemen(sim, r, 3);
        int rifle = I(r, "test_rifleman");
        sim.Step(new[] { Build(sim, Inf, rifle) });
        Run(sim, 5);
        int spent = sim.Production.CreditsSpent(0, Inf), progress = sim.HudFor(0).Queues.Single(q => q.Category == Inf).Items[0].ProgressPermille;
        Assert.True(progress > 0);
        // lose the depot and two riflemen's worth of room: 12 -> 2 with 3 used
        sim.Step(new[] { Sell(sim, sim.Map.BuildingAt(new CellCoord(16, 16))) });
        Assert.Equal(2, sim.SupplyCap(0));
        int cash = sim.Players.Cash[0];
        spent = sim.Production.CreditsSpent(0, Inf);
        progress = sim.HudFor(0).Queues.Single(q => q.Category == Inf).Items[0].ProgressPermille;
        Run(sim, 200);
        Assert.Equal(1, sim.Production.QueueLength(0, Inf));
        Assert.Equal(spent, sim.Production.CreditsSpent(0, Inf));    // no spend
        Assert.Equal(progress, sim.HudFor(0).Queues.Single(q => q.Category == Inf).Items[0].ProgressPermille);   // no progress
        Assert.Equal(cash, sim.Players.Cash[0]);                     // no refund
        Assert.Equal(3, sim.SupplyUsed(0));
        // a new supply building completes: the head resumes and the unit appears
        sim.Step(new[] { Place(sim, B(r, "test_depot"), 16, 16) });
        Assert.Equal(12, sim.SupplyCap(0));
        Run(sim, 400);
        Assert.Equal(0, sim.Production.QueueLength(0, Inf));
        Assert.Equal(4, sim.SupplyUsed(0));
    }

    [Fact]
    public void FinishedUnit_WaitsInTheLane_UntilItFits()
    {
        RuleSet r = Rules(hq: 2);
        Simulation sim = NewSim(r, debug: true, ("test_depot", 16, 16));
        Riflemen(sim, r, 3);
        sim.Step(new[] { Build(sim, Inf, I(r, "test_rifleman")) });
        // run until the build is almost complete, then lose the depot before the exit: fully paid items still wait
        Run(sim, 10);
        sim.Step(new[] { Sell(sim, sim.Map.BuildingAt(new CellCoord(16, 16))) });
        int infantry = sim.Infantry.Slots.Count;
        Run(sim, 300);
        Assert.Equal(infantry, sim.Infantry.Slots.Count);
        Assert.Equal(1, sim.Production.QueueLength(0, Inf));
        sim.Step(new[] { Place(sim, B(r, "test_depot"), 16, 16) });
        Run(sim, 400);
        Assert.Equal(infantry + 1, sim.Infantry.Slots.Count);
    }

    [Fact]
    public void DestroyingASupplyBuilding_KillsNoUnit()
    {
        RuleSet r = Rules(hq: 2);
        Simulation sim = NewSim(r, false, ("test_depot", 16, 16));
        Riflemen(sim, r, 6);
        Assert.Equal(6, sim.SupplyUsed(0));
        EntityId depot = sim.Map.BuildingAt(new CellCoord(16, 16));
        sim.Buildings.Health[depot.Index] = 0;
        Run(sim, 2);
        Assert.False(sim.Buildings.Slots.IsAlive(depot));
        Assert.Equal(2, sim.SupplyCap(0));
        Assert.Equal(6, sim.SupplyUsed(0));      // over the cap, nobody died
        Assert.Equal(6, sim.Infantry.Slots.Count);
    }

    [Fact]
    public void FreeUnits_AreNeverBlocked_AndCountOverTheCap()
    {
        RuleSet r = Rules(hq: 1);
        Simulation sim = NewSim(r, debug: true);
        Riflemen(sim, r, 2);                                             // setup spawns are not blocked
        Assert.Equal(2, sim.SupplyUsed(0));
        sim.Step(new[] { Place(sim, B(r, "test_refinery"), 16, 16) });   // free_unit = test_harvester (supply 1)
        Assert.Equal(1, sim.Units.Slots.Count);
        Assert.Equal(3, sim.SupplyUsed(0));
        Assert.Equal(1, sim.SupplyCap(0));
        Assert.Equal(QueueBlockReason.SupplyCap, sim.QueueBlockReason(0, Inf, I(r, "test_rifleman")));
    }

    [Fact]
    public void Disabled_ChangesNothing()
    {
        foreach (string? table in new string?[] { null, "enabled = false" })
        {
            RuleSet r = Rules(table, hq: 1);
            Simulation sim = NewSim(r);
            Riflemen(sim, r, 5);
            Assert.False(sim.SupplyEnabled);
            Assert.Equal(0, sim.SupplyUsed(0));
            Assert.Equal(0, sim.SupplyCap(0));
            Assert.Equal(default, sim.HudFor(0).Supply);
            Assert.Equal(QueueBlockReason.None, sim.QueueBlockReason(0, Inf, I(r, "test_rifleman")));
            sim.Step(Enumerable.Range(0, 5).Select(_ => Build(sim, Inf, I(r, "test_rifleman"))).ToArray());
            Assert.Equal(5, sim.Production.QueueLength(0, Inf));
            Run(sim, 400);
            Assert.True(sim.Production.QueueLength(0, Inf) < 5);   // units come out
        }
    }

    [Fact]
    public void Disabled_StateHashEqualsAnAbsentTable()
    {
        ulong Run400(string? table)
        {
            RuleSet r = Rules(table, hq: 1);
            Simulation sim = NewSim(r);
            Riflemen(sim, r, 4);
            sim.Step(new[] { Build(sim, Inf, I(r, "test_rifleman")), Build(sim, Veh, V(r, "test_tank")) });
            Run(sim, 400);
            return sim.ComputeStateHash();
        }
        Assert.Equal(Run400(null), Run400("enabled = false"));
    }

    [Fact]
    public void SaveLoad_MidPause_ReplaysToTheSameHash()
    {
        RuleSet r = Rules(hq: 2);
        SimSetup setup = SetupOf(r, debug: true);
        var sim = new Simulation(setup);
        foreach (var (id, x, y) in new[] { ("test_hq", 4, 4), ("test_power", 12, 4), ("test_barracks", 8, 4), ("test_depot", 16, 16) })
            sim.PlaceBuilding(0, B(r, id), new CellCoord(x, y));
        sim.Players.PowerOut[0] = 1000; sim.Players.Cash[0] = 100000;
        Riflemen(sim, r, 3);
        sim.Step(new[] { Build(sim, Inf, I(r, "test_rifleman")) });
        Run(sim, 5);
        sim.Step(new[] { Sell(sim, sim.Map.BuildingAt(new CellCoord(16, 16))) });
        Run(sim, 20);   // paused now
        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        Simulation loaded = Simulation.Load(ms, setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(sim.SupplyCap(0), loaded.SupplyCap(0));
        Assert.Equal(sim.SupplyUsed(0), loaded.SupplyUsed(0));
        for (int t = 0; t < 300; t++)
        {
            Command[] cmds = t == 40 ? new[] { Place(sim, B(r, "test_depot"), 16, 16) } : None;
            Command[] cmds2 = t == 40 ? new[] { Place(loaded, B(r, "test_depot"), 16, 16) } : None;
            sim.Step(cmds); loaded.Step(cmds2);
            Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.Equal(0, sim.Production.QueueLength(0, Inf));   // it did resume
    }

    // ---- rules ---------------------------------------------------------------------------------------------

    [Fact]
    public void Defaults_AreOff()
    {
        using var temp = new TempRules();
        SupplyRules s = RulesLoader.Load(temp.Dir).Sim.Supply;
        Assert.False(s.Enabled);
        Assert.Equal((200, 400, 900, 3), (s.Ceiling, s.Band1Max, s.Band2Max, s.StockIntervals));
        Assert.Equal(new[] { 1, 2, 3 }, s.BandValues);
    }

    [Fact]
    public void SupplyTable_Parses_AndABadTableIsRejected()
    {
        SupplyRules s = Rules("enabled = true\nceiling = 120\nband1_max = 300\nband2_max = 700\nband_values = [1, 3, 5]\nstock_intervals = 4").Sim.Supply;
        Assert.True(s.Enabled);
        Assert.Equal((120, 300, 700, 4), (s.Ceiling, s.Band1Max, s.Band2Max, s.StockIntervals));
        Assert.Equal(new[] { 1, 3, 5 }, s.BandValues);
        Assert.Throws<RulesException>(() => Rules("band1_max = 900\nband2_max = 400"));
        Assert.Throws<RulesException>(() => Rules("band_values = [1, 2]"));
        Assert.Throws<RulesException>(() => Rules("bogus = 1"));
    }

    [Fact]
    public void TypeKeys_Parse()
    {
        RuleSet r = Rules();
        Assert.Equal(1, r.Sim.Vehicles[V(r, "test_harvester")].Supply);
        Assert.Equal(0, r.Sim.Vehicles[V(r, "test_crawler")].Supply);
        Assert.Equal(-1, r.Sim.Vehicles[V(r, "test_tank")].Supply);
        Assert.Equal(-1, r.Sim.Infantry[I(r, "test_rifleman")].Supply);
        Assert.Equal(20, r.Sim.Buildings[B(r, "test_hq")].SupplyProvided);
        Assert.Equal(10, r.Sim.Buildings[B(r, "test_depot")].SupplyProvided);
        Assert.Equal(-1, r.Sim.Buildings[B(r, "test_depot")].SupplyUnstocked);
        Assert.Equal(0, r.Sim.Buildings[B(r, "test_power")].SupplyProvided);
    }

    [Fact]
    public void RulesHash_CoversTheSupplyKeys()
    {
        Assert.Equal(11, RulesHash.FormatVersion);
        ulong baseline = Rules().RulesHash;
        Assert.NotEqual(baseline, Rules("enabled = true\nceiling = 199").RulesHash);
        Assert.NotEqual(baseline, Rules("enabled = true\nband_values = [1, 2, 4]").RulesHash);
        Assert.NotEqual(baseline, Rules("enabled = false").RulesHash);
        Assert.NotEqual(baseline, Rules(hq: 21).RulesHash);
        Assert.Equal(baseline, Rules().RulesHash);
    }
}
