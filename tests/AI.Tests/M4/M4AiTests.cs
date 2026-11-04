// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests.M4;

/// <summary>M4-12: N players, tier climbing, tier rosters, garrisons and the no-income rescue of the Recruit framework.</summary>
public class M4AiTests(ITestOutputHelper log)
{
    private static DifficultyProfile Tier(int tier) => TestWorld.Tiers[tier - 1];

    // ---- scouting and target choice for 2-4 players ----------------------------------------------------

    private static AiContext Ctx(CellCoord start, IReadOnlyList<CellCoord> starts, int size = 64) => new()
    {
        Player = 0, PlayerCount = starts.Count, GameSeed = 1, Profile = Tier(3), Rng = new AiRandom(1, 0),
        MapWidth = size, MapHeight = size, StartCell = start, StartPositions = starts,
    };

    [Fact]
    public void TheRouteVisitsEveryOtherStartFirst_NearestFirst()
    {
        var starts = new List<CellCoord> { new(4, 4), new(59, 59), new(59, 4), new(4, 59) };
        List<CellCoord> route = StartRoute.For(Ctx(new CellCoord(5, 5), starts));
        Assert.True(route.Count >= 3);
        // the three other starts come first, the far corner (59,59) after the two equally near ones
        Assert.Equal(new[] { new CellCoord(59, 59), new CellCoord(59, 4), new CellCoord(4, 59) }.OrderBy(c => c.X * 100 + c.Y),
            route.Take(3).OrderBy(c => c.X * 100 + c.Y));
        Assert.Equal(new CellCoord(59, 59), route[2]);   // the diagonal corner is the farthest
        Assert.DoesNotContain(route, c => Math.Max(Math.Abs(c.X - 4), Math.Abs(c.Y - 4)) < 8);   // never our own start
    }

    [Fact]
    public void WithoutStartPositionsTheRouteIsTheMirrorFirst()
    {
        List<CellCoord> route = StartRoute.For(Ctx(new CellCoord(5, 5), Array.Empty<CellCoord>()));
        Assert.Equal(new CellCoord(58, 58), route[0]);
    }

    [Fact]
    public void FourPlayerFreeForAllIsDeterministicAndEveryAiFindsAnEnemy()
    {
        MapData map = QuadMap(48, 2);
        var sides = Enumerable.Range(0, 4).Select(i => new AiSide(AiRegistry.TieredId, Tier(3), i % 2 == 0 ? Faction.Authority : Faction.Directorate)).ToList();
        AiMultiResult a = AiMatchRunner.PlayN(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 5, sides, 1, 3000);
        AiMultiResult b = AiMatchRunner.PlayN(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 5, sides, 1, 3000);
        Assert.Equal(a.FinalHash, b.FinalHash);
        Assert.Equal(new[] { 1, 2, 3, 0 }, a.Start);
        for (int i = 0; i < 4; i++) Assert.True(a.Stats[i].Structures >= 4, $"side {i} built a base");
    }

    [Fact]
    public void EachAiPicksAnEnemyPlayerAsItsTarget()
    {
        MapData map = QuadMap(40, 3);
        var sim = new Simulation(new SimSetup(3, map, 4, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash));
        SkirmishStart.Apply(sim);
        var driver = new AiDriver(3);
        var ais = new RecruitAi[4];
        for (byte p = 0; p < 4; p++) { ais[p] = new RecruitAi(Tier(3)); driver.Add(p, ais[p], Tier(3)); }
        var pending = new List<Command>();
        for (int t = 0; t < 9000 && Enumerable.Range(0, 4).Any(p => ais[p].TargetPlayer < 0); t++)
        {
            driver.Collect(sim, pending);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
        }
        int chose = 0;
        for (int p = 0; p < 4; p++)
        {
            log.WriteLine($"P{p} targets P{ais[p].TargetPlayer}{(sim.Players.Defeated[p] ? " (defeated)" : "")} at tick {sim.Tick}");
            Assert.NotEqual(p, ais[p].TargetPlayer);
            if (ais[p].TargetPlayer >= 0) chose++;
        }
        // fog-honest: a side that is knocked out before it sees an enemy building never gets a target; most do
        Assert.True(chose >= 3, $"{chose} of 4 AIs chose an enemy player");
    }

    /// <summary>A 4-start test map: the 2-player generator's top-left quadrant mirrored into every corner.</summary>
    internal static MapData QuadMap(int size, ulong seed)
    {
        MapData m = MapGenerator.Generate(size, seed);
        int w = m.Width, h = m.Height, n = w * h;
        var land = new LandType[n]; var ov = new OverlayKind[n]; var den = new byte[n];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sx = x < (w + 1) / 2 ? x : w - 1 - x, sy = y < (h + 1) / 2 ? y : h - 1 - y;
                land[y * w + x] = m.Land[sy * w + sx]; ov[y * w + x] = m.Overlay[sy * w + sx]; den[y * w + x] = m.Density[sy * w + sx];
            }
        CellCoord s = m.StartPositions[0];
        return new MapData("quad", m.Theater, w, h, new ushort[n], land, new byte[n], ov, den,
            [s, new CellCoord(w - 1 - s.X, h - 1 - s.Y), new CellCoord(w - 1 - s.X, s.Y), new CellCoord(s.X, h - 1 - s.Y)]);
    }

    // ---- tier climbing ------------------------------------------------------------------------------

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void EliteClimbsToTierThreeAndBuildsTierTypes(Faction f)
    {
        // a rich economy so the schedule, not the money, decides: the AI must build both gates and field tier-2/3 units
        var rig = new TierRig(f, Tier(4) with { ActiveScouting = false, Harass = false, AttackThreshold = 500 }, r =>
        {
            CellCoord yard = r.Yard(0);
            r.SpawnHarvester(0, f, yard);
            r.Sim.Players.Cash[0] = 40000;
        });
        rig.Run(TierPlanner.Tier3GateTick + 6000, () => rig.Ai.Tier3Tick >= 0 && TierUnits(rig, f) >= 2);
        log.WriteLine($"{f}: tier2 at {rig.Ai.Tier2Tick}, tier3 at {rig.Ai.Tier3Tick}, tier units {TierUnits(rig, f)}, upgrades {rig.Ai.UpgradesOrdered}, tick {rig.Sim.Tick}");
        PlayerHud hud = rig.Sim.HudFor(0);
        foreach (QueueView q in hud.Queues) log.WriteLine($"  {q.Category}: {string.Join(",", q.Items.Select(i => (q.Category is ProductionCategory.Structure or ProductionCategory.Defence ? rig.World.Sim.Buildings[i.TypeIndex].Id : i.TypeIndex.ToString()) + "@" + i.ProgressPermille + (i.Stalled ? "(stalled)" : "")))} ready {q.ReadyTypeIndex}");
        log.WriteLine($"  income-stalled {rig.Ai.StructuresSold} harvesters {rig.Harvesters(0).Count}");
        log.WriteLine($"  power {hud.PowerOut}/{hud.PowerUse} cash {hud.Cash}; structures: " + string.Join(",", Enumerable.Range(1, rig.Sim.Buildings.Capacity - 1)
            .Where(i => rig.Sim.Buildings.Slots.IsIndexAlive(i) && rig.Sim.Buildings.Player[i] == 0).Select(i => rig.World.Sim.Buildings[rig.Sim.Buildings.TypeIndex[i]].Id)));
        Assert.True(rig.Ai.Tier2Tick >= 0, "the tier-2 gate was built");
        Assert.True(rig.Ai.Tier3Tick >= 0 && rig.Ai.Tier3Tick <= 15 * 60 * 20, "tier 3 within 15 minutes");
        Assert.True(TierUnits(rig, f) >= 2, "tier-2/3 units were built");
    }

    [Fact]
    public void VeteranStaysAtTierOneWithoutTheUpgradeFlag()
    {
        var rig = new TierRig(Faction.Authority, Tier(3) with { AttackThreshold = 500 }, r =>
        {
            r.SpawnHarvester(0, Faction.Authority, r.Yard(0));
            r.Sim.Players.Cash[0] = 40000;
        });
        rig.Run(TierPlanner.Tier3GateTick + 600);
        Assert.Equal(0, rig.Count(0, "auth_signals_hq"));
        Assert.Equal(-1, rig.Ai.Tier3Tick);
    }

    private static int TierUnits(TierRig rig, Faction f)
    {
        SimRules rules = rig.World.Sim;
        int n = 0;
        for (int i = 1; i < rig.Sim.Units.Capacity; i++)
            if (rig.Sim.Units.Slots.IsIndexAlive(i) && rig.Sim.Units.Player[i] == 0 && rules.Vehicles[rig.Sim.Units.TypeIndex[i]].Tier >= 2) n++;
        for (int i = 1; i < rig.Sim.Infantry.Capacity; i++)
            if (rig.Sim.Infantry.Slots.IsIndexAlive(i) && rig.Sim.Infantry.Player[i] == 0 && rules.Infantry[rig.Sim.Infantry.TypeIndex[i]].Tier >= 2) n++;
        return n;
    }

    [Fact]
    public void EveryRosterIdResolvesAndHasTheRightTier()
    {
        SimRules rules = TestWorld.Rules.Sim;
        foreach (Faction f in new[] { Faction.Authority, Faction.Directorate })
        {
            RecruitFaction k = RecruitFaction.For(f);
            foreach (string id in k.InfantryTier2) Assert.Equal(2, rules.Infantry[SimRules.IndexOf(rules.Infantry, id, x => x.Id)].Tier);
            foreach (string id in k.VehicleTier2) Assert.InRange(rules.Vehicles[SimRules.IndexOf(rules.Vehicles, id, x => x.Id)].Tier, 1, 2);
            foreach (string id in k.VehicleTier3) Assert.Equal(3, rules.Vehicles[SimRules.IndexOf(rules.Vehicles, id, x => x.Id)].Tier);
            foreach (string? id in new[] { k.Tier2GateId, k.Tier3GateId, k.GarrisonId, k.AirProducerId, k.SuperweaponBuildingId })
                Assert.True(id is not null && SimRules.IndexOf(rules.Buildings, id, x => x.Id) >= 0, $"{f}: {id}");
            Assert.True(rules.Buildings[SimRules.IndexOf(rules.Buildings, k.GarrisonId!, x => x.Id)].Garrison > 0);
        }
        // design 08 §4: the Seismic Mortar, the Foam truck and both T3 heavies are in the Authority bags
        RecruitFaction a = RecruitFaction.For(Faction.Authority);
        Assert.Contains("auth_seismic_mortar", a.VehicleTier2);
        Assert.Contains("auth_foam_truck", a.VehicleTier2);
        Assert.Contains("auth_bulwark", a.VehicleTier3);
        Assert.Contains("auth_howitzer", a.VehicleTier3);
    }

    [Fact]
    public void TheAircraftPickIsAGroundAttackerOfTheHighestTier()
    {
        var rig = new TierRig(Faction.Authority, Tier(4) with { ActiveScouting = false, Harass = false, AttackThreshold = 500 }, r =>
        {
            CellCoord yard = r.Yard(0);
            r.PlaceNear(0, "auth_power", yard); r.PlaceNear(0, "auth_power", yard); r.PlaceNear(0, "auth_power", yard);
            r.PlaceNear(0, "auth_refinery", yard);
            r.PlaceNear(0, "auth_factory", yard);
            r.PlaceNear(0, "auth_engineering", yard);
            r.PlaceNear(0, "auth_signals_hq", yard);
            r.PlaceNear(0, "auth_airfield", yard);
            r.SpawnFoot(0, Faction.Authority, 10, new CellCoord(yard.X + 2, yard.Y + 6));
            r.Sim.Players.Cash[0] = 20000;
        });
        rig.Run(2500, () => rig.Ai.AircraftOrdered >= 1);
        Assert.True(rig.Ai.AircraftOrdered >= 1);
        int bomber = rig.VehicleType("auth_bomber"), fighter = rig.VehicleType("auth_interceptor");
        rig.Run(2000);
        int bombers = 0, fighters = 0;
        for (int i = 1; i < rig.Sim.Units.Capacity; i++)
        {
            if (!rig.Sim.Units.Slots.IsIndexAlive(i) || rig.Sim.Units.Player[i] != 0) continue;
            if (rig.Sim.Units.TypeIndex[i] == bomber) bombers++;
            if (rig.Sim.Units.TypeIndex[i] == fighter) fighters++;
        }
        log.WriteLine($"bombers {bombers}, interceptors {fighters}");
        Assert.True(bombers >= 1, "the T3 bomber, not the anti-air fighter, while no enemy aircraft was seen");
        Assert.Equal(0, fighters);
    }

    // ---- garrisons ----------------------------------------------------------------------------------

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void RegularFillsItsOwnGarrisonWithIdleInfantry(Faction f)
    {
        string id = RecruitFaction.For(f).GarrisonId!;
        EntityId garrison = default;
        var rig = new TierRig(f, Tier(2) with { AttackThreshold = 500 }, r =>
        {
            CellCoord yard = r.Yard(0);
            r.SpawnHarvester(0, f, yard);
            garrison = r.PlaceNear(0, id, new CellCoord(yard.X + 5, yard.Y + 5));
            r.SpawnFoot(0, f, 10, new CellCoord(yard.X + 2, yard.Y + 8));
        });
        rig.Run(1500, () => rig.Sim.OccupantsIn(garrison) >= 3);
        log.WriteLine($"{f}: occupants {rig.Sim.OccupantsIn(garrison)}, orders {rig.Ai.GarrisonOrders}");
        Assert.True(rig.Sim.OccupantsIn(garrison) >= 3, "idle infantry went into the garrison");
        Assert.True(rig.Ai.PeakGarrisoned >= 1);
        // the recruit tier does not garrison
        var recruit = new TierRig(f, Tier(1) with { AttackThreshold = 500 }, r =>
        {
            CellCoord yard = r.Yard(0);
            r.SpawnHarvester(0, f, yard);
            garrison = r.PlaceNear(0, id, new CellCoord(yard.X + 5, yard.Y + 5));
            r.SpawnFoot(0, f, 10, new CellCoord(yard.X + 2, yard.Y + 8));
        });
        recruit.Run(1500);
        Assert.Equal(0, recruit.Ai.GarrisonOrders);
    }

    [Fact]
    public void RegularOccupiesAnEmptyNeutralRuinNearItsBase()
    {
        // three slots: 0 = the AI, 1 = an idle enemy, 2 = the neutral owner of a ruin beside our base (design 08 §3, Q-M4-b)
        MapData map = MapGenerator.Generate(64, 1);
        var sim = new Simulation(new SimSetup(1, map, 3, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash)
        { Teams = [0, 1, Teams.Neutral], AllowDebug = true });
        SkirmishStart.Apply(sim);
        CellCoord yard = default;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == 0) { yard = sim.Buildings.Cell[i]; break; }
        int ruinType = SimRules.IndexOf(TestWorld.Rules.Sim.Buildings, "neu_ruin_depot", b => b.Id);
        EntityId ruin = EntityId.None;
        for (int d = 5; d < 12 && ruin.IsNone; d++) ruin = sim.PlaceBuilding(2, ruinType, new CellCoord(yard.X + d, yard.Y + d));
        Assert.False(ruin.IsNone);
        int rifle = SimRules.IndexOf(TestWorld.Rules.Sim.Infantry, "auth_rifleman", x => x.Id);
        int scraper = SimRules.IndexOf(TestWorld.Rules.Sim.Vehicles, "auth_scraper", x => x.Id);
        sim.SpawnVehicle(0, scraper, new CellCoord(yard.X, yard.Y + 5));
        for (int k = 0; k < 10; k++) sim.SpawnInfantry(0, rifle, new CellCoord(yard.X + 1 + k % 5, yard.Y + 4 + k / 5));
        var ai = new RecruitAi(Tier(2) with { AttackThreshold = 500 });
        var driver = new AiDriver(1);
        driver.Add(0, ai, Tier(2) with { AttackThreshold = 500 });
        var pending = new List<Command>();
        for (int t = 0; t < 1500 && sim.OccupantsIn(ruin) == 0; t++)
        {
            driver.Collect(sim, pending);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
        }
        log.WriteLine($"ruin occupants {sim.OccupantsIn(ruin)}, orders {ai.GarrisonOrders}");
        Assert.True(ai.GarrisonOrders >= 1, "the AI ordered soldiers into the neutral ruin");
        Assert.True(sim.OccupantsIn(ruin) >= 1, "a soldier is inside");
    }

    // ---- finishing games ----------------------------------------------------------------------------

    [Fact]
    public void ASideThatLostItsHarvesterAndIsBrokeSellsForANewOne()
    {
        var rig = new TierRig(Faction.Authority, Tier(2) with { AttackThreshold = 500 }, r =>
        {
            CellCoord yard = r.Yard(0);
            r.PlaceNear(0, "auth_power", yard); r.PlaceNear(0, "auth_power", yard);
            r.PlaceNear(0, "auth_refinery", yard);
            r.PlaceNear(0, "auth_factory", yard);
            r.PlaceNear(0, "auth_barracks", yard);
            r.PlaceNear(0, "auth_silo", yard);
            r.SpawnHarvester(0, Faction.Authority, yard);
        });
        rig.Run(200);
        foreach (EntityId h in rig.Harvesters(0)) rig.Kill(h, ActorKind.Vehicle);
        rig.Run(2);
        rig.Sim.Players.Cash[0] = 0; rig.Sim.Players.Stored[0] = 0;
        rig.Run(2500, () => rig.Harvesters(0).Count > 0);
        log.WriteLine($"sold {rig.Ai.StructuresSold}, harvesters {rig.Harvesters(0).Count}");
        Assert.True(rig.Ai.StructuresSold >= 1, "spare structures were sold");
        Assert.Equal(1, rig.Count(0, "auth_factory"));
        Assert.Equal(1, rig.Count(0, "auth_conyard"));
        Assert.True(rig.Harvesters(0).Count >= 1, "a new harvester was bought with the refunds");
    }

    [Fact]
    public void ADirectorateMirrorAtVeteranResolvesBeforeTheLimit()
    {
        // seed 1 at size 56 was a stalemate in M3 (both sides broke, two trucks unable to out-damage the yard's repair)
        MapData map = MapGenerator.Generate(56, 1);
        var side = new AiSide(AiRegistry.TieredId, Tier(3), Faction.Directorate);
        AiGameResult r = AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 1, side, side, true, 25000);
        log.WriteLine($"winner {r.Winner} at {r.EndTick}");
        Assert.True(r.Winner >= 0 && r.EndTick < 25000);
    }
}
