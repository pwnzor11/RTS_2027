// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Production.ProductionFixture;

namespace Rts.Sim.Tests.Production;

public class ProductionTests
{
    private static readonly ProductionCategory Infantry = ProductionCategory.Infantry;
    private static readonly ProductionCategory Vehicle = ProductionCategory.Vehicle;
    private static readonly ProductionCategory Structure = ProductionCategory.Structure;
    private static readonly ProductionCategory Defence = ProductionCategory.Defence;

    [Fact]
    public void Prerequisites_GateEveryCategory()
    {
        var sim = NewSim(new[] { ("test_hq", 4, 4) });
        sim.Players.Cash[0] = 10000;
        // HQ only: no barracks (infantry, turret), no factory (tank). A structure that needs only the HQ is fine.
        Step(sim, Build(sim, Infantry, Inf("test_rifleman")), Build(sim, Vehicle, Veh("test_tank")),
            Build(sim, Defence, Bld("test_turret")), Build(sim, Structure, Bld("test_barracks")),
            Build(sim, Structure, Bld("test_refinery")));
        Assert.Equal(0, sim.Production.QueueLength(0, Infantry));
        Assert.Equal(0, sim.Production.QueueLength(0, Vehicle));
        Assert.Equal(0, sim.Production.QueueLength(0, Defence));
        Assert.Equal(1, sim.Production.QueueLength(0, Structure));   // barracks only; the refinery needs a power plant
        Assert.Equal(Bld("test_barracks"), sim.Production.QueuedType(0, Structure, 0));
        // Wrong category for the type, bad index, aircraft and a neutral-style bad player are all rejected.
        Step(sim, Build(sim, Defence, Bld("test_power")), Build(sim, Structure, 99), Build(sim, ProductionCategory.Aircraft, 0));
        Assert.Equal(1, sim.Production.QueueLength(0, Structure));
        Assert.Equal(0, sim.Production.QueueLength(0, Defence));
    }

    [Fact]
    public void Prerequisites_UnlockWhenTheBuildingExists_AndRelock_WhenItIsGone()
    {
        var sim = NewSim(new[] { ("test_hq", 4, 4), ("test_barracks", 8, 4) });
        sim.Players.Cash[0] = 5000;
        Assert.True(sim.CanBuild(0, Infantry, Inf("test_rifleman")));
        Assert.True(sim.CanBuild(0, Defence, Bld("test_turret")));
        Assert.False(sim.CanBuild(0, Vehicle, Veh("test_tank")));
        Step(sim, Sell(sim, BuildingOf(sim, 8, 4)));
        Assert.False(sim.CanBuild(0, Infantry, Inf("test_rifleman")));
        Assert.False(sim.CanBuild(0, Defence, Bld("test_turret")));
    }

    [Fact]
    public void QueueLimit_IsEconomyMaxQueue()
    {
        var sim = Base();
        Assert.Equal(5, Loaded.Sim.Economy.MaxQueue);
        var cmds = Enumerable.Range(0, 7).Select(_ => Build(sim, Infantry, Inf("test_rifleman"))).ToArray();
        Step(sim, cmds);
        Assert.Equal(5, sim.Production.QueueLength(0, Infantry));
        Step(sim, Build(sim, Infantry, Inf("test_rifleman")));
        Assert.Equal(5, sim.Production.QueueLength(0, Infantry));
    }

    [Fact]
    public void Drain_StallsWhenBroke_AndResumes()
    {
        // rifleman: cost 100, 20 build ticks => 5 credits due per tick.
        var sim = Base(cash: 3);
        Step(sim, Build(sim, Infantry, Inf("test_rifleman")));   // build tick 0: pays 3 of 5 and stalls
        Assert.Equal(3, sim.Production.CreditsSpent(0, Infantry));
        Assert.True(sim.Production.IsStalled(0, Infantry));
        Assert.Equal(0, sim.Players.Cash[0]);
        Assert.Equal(0, sim.HudFor(0).Queues.Single(q => q.Category == Infantry).Items[0].ProgressPermille);
        Run(sim, 3);   // still broke: nothing moves
        Assert.Equal(3, sim.Production.CreditsSpent(0, Infantry));
        Assert.True(sim.HudFor(0).Queues.Single(q => q.Category == Infantry).Items[0].Stalled);
        sim.Players.Cash[0] = 1000;
        sim.Step(None);   // pays the missing 2 and progresses one tick
        Assert.False(sim.Production.IsStalled(0, Infantry));
        Assert.Equal(5, sim.Production.CreditsSpent(0, Infantry));
        Assert.Equal(50, sim.HudFor(0).Queues.Single(q => q.Category == Infantry).Items[0].ProgressPermille);
        Run(sim, 18);
        Assert.Equal(0, sim.Infantry.Slots.Count);   // 19 productive ticks done
        Run(sim, 1);
        Assert.Equal(1, sim.Infantry.Slots.Count);   // the 20th completes the item
        Assert.Equal(903, sim.Players.Cash[0]);   // 3 were paid before the top-up: 1000 - (100 - 3)
        Assert.Equal(0, sim.Production.QueueLength(0, Infantry));
    }

    [Fact]
    public void Cancel_RefundsExactlyTheSpentCredits_AndOnlyForTheFront()
    {
        var sim = Base(cash: 1000);
        Step(sim, Build(sim, Infantry, Inf("test_rifleman")), Build(sim, Infantry, Inf("test_engineer")));
        Run(sim, 6);   // 7 ticks of progress so far: 100 * 7 / 20 = 35 credits
        Assert.Equal(35, sim.Production.CreditsSpent(0, Infantry));
        Assert.Equal(965, sim.Players.Cash[0]);
        // Cancelling the second item refunds nothing.
        Step(sim, Cancel(sim, Infantry, 1));
        Assert.Equal(1, sim.Production.QueueLength(0, Infantry));
        Assert.Equal(965 - 5, sim.Players.Cash[0]);   // the front item paid its 8th tick (35 -> 40) in the same step
        int before = sim.Players.Cash[0];
        int spent = sim.Production.CreditsSpent(0, Infantry);
        Step(sim, Cancel(sim, Infantry, 0));
        Assert.Equal(before + spent, sim.Players.Cash[0]);
        Assert.Equal(0, sim.Production.QueueLength(0, Infantry));
        Assert.Equal(0, sim.Production.CreditsSpent(0, Infantry));
        Assert.Equal(1000, sim.Players.Cash[0]);   // everything came back
        Step(sim, Cancel(sim, Infantry, 0), Cancel(sim, Infantry, 3));   // empty / bad position: ignored
        Assert.Equal(1000, sim.Players.Cash[0]);
    }

    [Theory]
    [InlineData(0, 0, 20)]   // one barracks: 20 ticks
    [InlineData(1, 0, 13)]   // two: 20 * 2 / 3
    [InlineData(2, 0, 10)]   // three: 20 * 2 / 4
    public void ExtraFactories_SpeedUpUnits(int extraBarracks, int extraFactories, int expectedTicks)
    {
        var sim = Base(extraBarracks: extraBarracks, extraFactories: extraFactories);
        Step(sim, Build(sim, Infantry, Inf("test_rifleman")));
        int steps = 1 + RunUntil(sim, () => sim.Infantry.Slots.Count == 1);
        Assert.Equal(expectedTicks, steps);
        Assert.Equal(10000 - 100, sim.Players.Cash[0]);
    }

    [Fact]
    public void ExtraFactories_OnlyCountTheirOwnCategory()
    {
        var sim = Base(extraBarracks: 2);   // 3 barracks, 1 factory
        Step(sim, Build(sim, Vehicle, Veh("test_jeep")));   // jeep: cost 400 -> 80 ticks at one factory
        int steps = 1 + RunUntil(sim, () => sim.Units.Slots.Count == 1);
        Assert.Equal(80, steps);
    }

    [Theory]
    [InlineData(1, 30)]   // out 120, use 130 -> 92 %: x1.5
    [InlineData(2, 50)]   // 20 vs 40 -> 50 %: x2.5
    [InlineData(3, 80)]   // 20 vs 80 -> 25 %: x4
    public void LowPower_SlowsProductionByTheBandMultiplier(int band, int expectedTicks)
    {
        var list = new List<(string, int, int)> { ("test_hq", 4, 4), ("test_barracks", 8, 4) };
        if (band == 1)
        {
            list.Add(("test_power", 12, 4));
            for (int i = 0; i < 12; i++) list.Add(("test_turret", 2 + i, 20));
        }
        else
        {
            list.Add(("test_factory", 4, 8));
            if (band == 3) for (int i = 0; i < 4; i++) list.Add(("test_turret", 2 + i, 20));
        }
        var sim = NewSim(list);
        sim.Players.Cash[0] = 5000;
        Assert.Equal(band, sim.PowerBand(0));
        Step(sim, Build(sim, Infantry, Inf("test_rifleman")));
        int steps = 1 + RunUntil(sim, () => sim.Infantry.Slots.Count == 1);
        Assert.Equal(expectedTicks, steps);
        Assert.Equal(4900, sim.Players.Cash[0]);   // the price does not change, only the time
    }

    [Fact]
    public void UnitExits_AtFactoryExitCell_AndWalksToRally()
    {
        var sim = Base();
        EntityId barracks = BuildingOf(sim, 8, 4);
        Step(sim, Rally(sim, barracks, 20, 20), Build(sim, Infantry, Inf("test_rifleman")));
        Assert.Equal(new CellCoord(20, 20), sim.Production.RallyCell(0, Infantry));
        Assert.Equal(barracks, sim.Production.PrimaryFactory(0, Infantry));
        RunUntil(sim, () => sim.Infantry.Slots.Count == 1);
        int i = Enumerable.Range(1, sim.Infantry.Capacity - 1).First(sim.Infantry.Slots.IsIndexAlive);
        Assert.Equal(new CellCoord(9, 6), sim.Infantry.Position[i].ToCell());   // top-left (8,4) + exit (1,2)
        Assert.Equal(MissionKind.Move, sim.Infantry.NextMission[i]);
        Assert.Equal(new CellCoord(20, 20), sim.Infantry.NextCell[i]);
        Run(sim, 30);
        Assert.Equal(MissionKind.Move, sim.Infantry.Mission[i]);
        Assert.NotEqual(new CellCoord(9, 6), sim.Infantry.Position[i].ToCell());
    }

    [Fact]
    public void BlockedExit_UsesTheNearestFreeCell_InRingOrder()
    {
        var sim = Base();
        sim.SpawnVehicle(0, Veh("test_crawler"), new CellCoord(9, 6));   // sits on the barracks exit
        Step(sim, Build(sim, Infantry, Inf("test_rifleman")));
        RunUntil(sim, () => sim.Infantry.Slots.Count == 1);
        int i = Enumerable.Range(1, sim.Infantry.Capacity - 1).First(sim.Infantry.Slots.IsIndexAlive);
        // Ring 1 around (9,6); (8,5) and (9,5) are barracks cells. M4-16: within the ring the free cell nearest the map centre wins
        // (it used to be the top row first, which sent the bottom-right base's units out behind their factory): (10,7).
        Assert.Equal(new CellCoord(10, 7), sim.Infantry.Position[i].ToCell());
    }

    [Fact]
    public void Harvesters_AreNotSentToTheRally()
    {
        var sim = Base();
        EntityId factory = BuildingOf(sim, 4, 8);
        Step(sim, Rally(sim, factory, 25, 25), Build(sim, Vehicle, Veh("test_harvester")));
        RunUntil(sim, () => sim.Units.Slots.Count == 1);
        int i = Enumerable.Range(1, sim.Units.Capacity - 1).First(sim.Units.Slots.IsIndexAlive);
        Assert.Equal(new CellCoord(5, 11), sim.Units.Position[i].ToCell());
        Assert.NotEqual(MissionKind.Move, sim.Units.NextMission[i]);
        // A tank does get the rally order.
        Step(sim, Build(sim, Vehicle, Veh("test_tank")));
        RunUntil(sim, () => sim.Units.Slots.Count == 2);
        int j = Enumerable.Range(1, sim.Units.Capacity - 1).First(k => sim.Units.Slots.IsIndexAlive(k) && k != i);
        Assert.True(sim.Units.NextMission[j] == MissionKind.Move || sim.Units.Mission[j] == MissionKind.Move);
    }

    [Fact]
    public void Rally_RejectsForeignAndNonFactoryBuildings()
    {
        var sim = Base();
        Step(sim, Rally(sim, BuildingOf(sim, 4, 4), 20, 20),          // HQ produces nothing
            Rally(sim, BuildingOf(sim, 8, 4), 20, 20, player: 1));    // not the owner
        Assert.Null(sim.Production.RallyCell(0, Infantry));
        Assert.Null(sim.Production.RallyCell(1, Infantry));
    }

    [Fact]
    public void Structure_BecomesReady_ThenPlaceChecksTheBuildRadius()
    {
        var sim = Base();
        int power = Bld("test_power");
        Step(sim, Build(sim, Structure, power));
        Step(sim, Build(sim, Structure, Bld("test_barracks")));   // fine, queued behind it
        Assert.Equal(2, sim.Production.QueueLength(0, Structure));
        RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == power);
        Assert.Equal(1, sim.Production.QueueLength(0, Structure));
        Assert.Equal(power, sim.HudFor(0).Queues.Single(q => q.Category == Structure).ReadyTypeIndex);
        // While something is ready, Build in that category is refused and the queue waits.
        Step(sim, Build(sim, Structure, Bld("test_barracks")));
        Assert.Equal(1, sim.Production.QueueLength(0, Structure));
        int cash = sim.Players.Cash[0];
        Run(sim, 80);
        Assert.Equal(cash, sim.Players.Cash[0]);   // nothing drains while waiting
        // Outside the radius: rejected, still ready. HQ covers x 4..6, y 4..6; radius 2 => x up to 8 is in.
        int before = sim.Buildings.Slots.Count;
        Step(sim, Place(sim, power, 20, 20));
        Assert.Equal(before, sim.Buildings.Slots.Count);
        Assert.Equal(power, sim.Production.ReadyType(0, Structure));
        // Wrong type is rejected.
        Step(sim, Place(sim, Bld("test_silo"), 4, 12));
        Assert.Equal(before, sim.Buildings.Slots.Count);
        // Inside the radius: accepted and the ready slot clears.
        Step(sim, Place(sim, power, 1, 1));   // two cells from the HQ's top-left corner
        Assert.Equal(before + 1, sim.Buildings.Slots.Count);
        Assert.Equal(-1, sim.Production.ReadyType(0, Structure));
    }

    [Fact]
    public void Place_WithoutAReadyItem_IsRejected_WhenDebugIsOff()
    {
        var sim = Base();
        int before = sim.Buildings.Slots.Count;
        Step(sim, Place(sim, Bld("test_power"), 8, 8));
        Assert.Equal(before, sim.Buildings.Slots.Count);
    }

    [Fact]
    public void Walls_DoNotExtendTheBuildRadius()
    {
        var sim = NewSim(new[] { ("test_hq", 4, 4), ("test_barracks", 25, 25) });
        sim.Players.Cash[0] = 10000;
        Step(sim, Build(sim, Defence, Bld("test_wall")));
        RunUntil(sim, () => sim.Production.ReadyType(0, Defence) == Bld("test_wall"));
        Step(sim, Place(sim, Bld("test_wall"), 8, 8));   // HQ x/y 4..6: distance 2 both ways => inside
        Assert.False(BuildingOf(sim, 8, 8).IsNone);
        Step(sim, Build(sim, Defence, Bld("test_turret")));
        RunUntil(sim, () => sim.Production.ReadyType(0, Defence) == Bld("test_turret"));
        Step(sim, Place(sim, Bld("test_turret"), 10, 10));   // 2 from the wall but 4 from the HQ: walls do not count
        Assert.True(BuildingOf(sim, 10, 10).IsNone);
        Assert.Equal(Bld("test_turret"), sim.Production.ReadyType(0, Defence));
        Step(sim, Place(sim, Bld("test_turret"), 8, 7));   // 1 from the HQ's x edge... distance 2 => inside
        Assert.False(BuildingOf(sim, 8, 7).IsNone);
    }

    [Fact]
    public void RefineryPlacement_DeliversAFreeHarvesterAtTheExit()
    {
        var sim = NewSim(new[] { ("test_hq", 4, 4), ("test_power", 8, 4) });
        sim.Players.Cash[0] = 10000;
        int refinery = Bld("test_refinery");
        Step(sim, Build(sim, Structure, refinery));
        RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == refinery);
        Assert.Equal(0, sim.Units.Slots.Count);
        Step(sim, Place(sim, refinery, 4, 8));
        Assert.Equal(1, sim.Units.Slots.Count);
        int i = Enumerable.Range(1, sim.Units.Capacity - 1).First(sim.Units.Slots.IsIndexAlive);
        Assert.Equal(Veh("test_harvester"), sim.Units.TypeIndex[i]);
        Assert.Equal(0, sim.Units.Player[i]);
        Assert.Equal(new CellCoord(5, 11), sim.Units.Position[i].ToCell());   // (4,8) + exit (1,3)
        // Another structure placed the same way gives no unit.
        Step(sim, Build(sim, Structure, Bld("test_silo")));
        RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == Bld("test_silo"));
        Step(sim, Place(sim, Bld("test_silo"), 8, 7));
        Assert.Equal(1, sim.Units.Slots.Count);
    }

    [Fact]
    public void Sell_RefundsThePermilleOfCost_AndChecksOwner()
    {
        var sim = Base(cash: 100);
        EntityId factory = BuildingOf(sim, 4, 8);
        Step(sim, Sell(sim, factory, player: 1));
        Assert.True(sim.TryResolve(new ActorRef(ActorKind.Building, factory)));
        Assert.Equal(100, sim.Players.Cash[0]);
        Step(sim, Sell(sim, factory));
        Assert.False(sim.TryResolve(new ActorRef(ActorKind.Building, factory)));
        Assert.Equal(100 + 500, sim.Players.Cash[0]);   // 1000 * 500 / 1000
        Step(sim, Sell(sim, factory));   // already gone
        Assert.Equal(600, sim.Players.Cash[0]);
    }

    [Fact]
    public void Repair_CostsCreditsPerHp_AndStopsWhenFull()
    {
        var sim = Base(cash: 1000);
        EntityId barracks = BuildingOf(sim, 8, 4);   // cost 300, 600 hp: 0.25 credit/hp, 2 hp per tick
        sim.Buildings.Health[barracks.Index] = 500;
        Step(sim, Repair(sim, barracks));
        Run(sim, 3);   // 4 repair ticks: +8 hp, 2 credits
        Assert.Equal(508, sim.Buildings.Health[barracks.Index]);
        Assert.Equal(998, sim.Players.Cash[0]);
        Run(sim, 60);
        Assert.Equal(600, sim.Buildings.Health[barracks.Index]);
        Assert.Equal(1000 - 25, sim.Players.Cash[0]);   // 100 hp * 0.25
        Run(sim, 5);
        Assert.Equal(1000 - 25, sim.Players.Cash[0]);   // switched itself off
    }

    [Fact]
    public void Repair_Toggles_AndStopsWhenBroke()
    {
        var sim = Base(cash: 3);
        EntityId hq = BuildingOf(sim, 4, 4);
        var turret = sim.PlaceBuilding(0, Bld("test_turret"), new CellCoord(1, 1));   // cost 400, 400 hp: 1 credit per tick
        Assert.False(turret.IsNone);
        sim.Buildings.Health[turret.Index] = 300;
        Step(sim, Repair(sim, turret));
        Run(sim, 2);
        Assert.Equal(306, sim.Buildings.Health[turret.Index]);
        Assert.Equal(0, sim.Players.Cash[0]);
        Run(sim, 5);   // broke: no more healing
        Assert.Equal(306, sim.Buildings.Health[turret.Index]);
        sim.Players.Cash[0] = 100;
        Run(sim, 3);
        Assert.Equal(306, sim.Buildings.Health[turret.Index]);   // it stopped for good; needs a new Repair
        Step(sim, Repair(sim, turret));    // on
        Step(sim, Repair(sim, turret));    // off again
        Run(sim, 3);
        Assert.Equal(308, sim.Buildings.Health[turret.Index]);   // only the one tick the switch was on
        Assert.Equal(99, sim.Players.Cash[0]);
        Assert.False(hq.IsNone);
    }

    [Fact]
    public void UnpoweredTurret_HoldsFire_PoweredOneFires()
    {
        int Fight(bool powered)
        {
            var list = new List<(string, int, int)> { ("test_hq", 4, 4), ("test_barracks", 8, 4), ("test_turret", 10, 10) };
            if (powered) list.Add(("test_power", 12, 4)); else list.Add(("test_factory", 4, 8));
            var sim = NewSim(list);
            Assert.Equal(powered ? 0 : 3, sim.PowerBand(0));   // 20 out vs 50 in => 40 %
            var target = sim.SpawnVehicle(1, Veh("test_crawler"), new CellCoord(12, 10));
            Run(sim, 60);
            return sim.Units.Slots.IsAlive(target) ? sim.Units.Health[target.Index] : 0;
        }
        Assert.Equal(300, Fight(powered: false));
        Assert.True(Fight(powered: true) < 300);
    }

    [Fact]
    public void Defeat_GameOver_AndWinningTeam_WithTeams()
    {
        var sim = NewSim(new[] { ("test_hq", 4, 4) }, teams: new byte[] { 0, 0, 1, 1 }, players: 4,
            others: new[] { ((byte)1, "test_hq", 10, 4), ((byte)2, "test_hq", 4, 10), ((byte)3, "test_hq", 10, 10), ((byte)3, "test_wall", 20, 20) });
        Assert.False(sim.GameOver);
        Assert.Equal(-1, sim.WinningTeam);
        Run(sim, 3);
        Assert.False(sim.GameOver);
        sim.Buildings.Health[BuildingOf(sim, 4, 10).Index] = 0;   // player 2 loses its HQ
        sim.Step(None);
        Assert.True(sim.Players.Defeated[2]);
        Assert.True(sim.HudFor(2).Defeated);
        Assert.False(sim.GameOver);                                // player 3 is still alive on team 1
        sim.Buildings.Health[BuildingOf(sim, 10, 10).Index] = 0;  // player 3 keeps only a wall: still defeated
        sim.Step(None);
        Assert.True(sim.Players.Defeated[3]);
        Assert.True(sim.GameOver);
        Assert.Equal(0, sim.WinningTeam);
        Assert.False(sim.Players.Defeated[0]);
        Run(sim, 5);
        Assert.True(sim.GameOver);                                 // sticky
    }

    [Fact]
    public void Defeat_FreeForAll_AndNeverBasedPlayersAreNotDefeated()
    {
        var sim = NewSim(new[] { ("test_hq", 4, 4) }, others: new[] { ((byte)1, "test_hq", 10, 10) });
        sim.Buildings.Health[BuildingOf(sim, 10, 10).Index] = 0;
        sim.Step(None);
        Assert.True(sim.GameOver);
        Assert.Equal(0, sim.WinningTeam);
        // A sandbox where nobody has ever had a building: nobody is defeated, the match never ends.
        var sandbox = NewSim(Array.Empty<(string, int, int)>());
        Run(sandbox, 5);
        Assert.False(sandbox.Players.Defeated[0]);
        Assert.False(sandbox.GameOver);
    }

    [Fact]
    public void DefeatedPlayer_CommandsAreIgnored_UnitsRemain()
    {
        var sim = NewSim(new[] { ("test_hq", 4, 4), ("test_barracks", 8, 4) }, others: new[] { ((byte)1, "test_hq", 10, 10) });
        sim.Players.Cash[0] = 1000;
        var unit = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(2, 2));
        sim.Buildings.Health[BuildingOf(sim, 4, 4).Index] = 0;
        sim.Buildings.Health[BuildingOf(sim, 8, 4).Index] = 0;
        sim.Step(None);
        Assert.True(sim.Players.Defeated[0]);
        Assert.True(sim.Infantry.Slots.IsAlive(unit));   // classic: the army stays
        Step(sim, Build(sim, ProductionCategory.Structure, Bld("test_power")));
        Assert.Equal(0, sim.Production.QueueLength(0, ProductionCategory.Structure));
        Assert.Empty(sim.HudFor(0).Buildable);
    }

    [Fact]
    public void Hud_ReportsQueuesProgressReadyAndBuildable()
    {
        var sim = Base(cash: 777);
        int rifle = Inf("test_rifleman"), power = Bld("test_power");
        Step(sim, Build(sim, Infantry, rifle), Build(sim, Infantry, Inf("test_engineer")), Build(sim, Structure, power));
        Run(sim, 9);   // 10 ticks of progress on the rifleman (20 total) and on the power plant (60 total)
        PlayerHud hud = sim.HudFor(0);
        Assert.Equal(5, hud.Queues.Count);
        var inf = hud.Queues.Single(q => q.Category == Infantry);
        Assert.Equal(new[] { new QueueItemView(rifle, 500, false), new QueueItemView(Inf("test_engineer"), 0, false) }, inf.Items.ToArray());
        Assert.Equal(-1, inf.ReadyTypeIndex);
        var st = hud.Queues.Single(q => q.Category == Structure);
        Assert.Equal(new[] { new QueueItemView(power, 166, false) }, st.Items.ToArray());   // 10 / 60 ticks
        Assert.Empty(hud.Queues.Single(q => q.Category == Vehicle).Items);
        Assert.Equal(777 - 50 - 50, hud.Cash);   // 5 credits per tick on each of two queues for 10 ticks
        Assert.Equal(sim.Players.PowerOut[0], hud.PowerOut);
        Assert.Equal(sim.Players.PowerUse[0], hud.PowerUse);
        Assert.False(hud.Defeated);
        Assert.Contains((Infantry, rifle), hud.Buildable);   // queue has room
        Assert.Contains((Vehicle, Veh("test_tank")), hud.Buildable);
        Assert.Contains((Defence, Bld("test_turret")), hud.Buildable);
        Assert.Contains((Structure, power), hud.Buildable);
        Assert.DoesNotContain((Structure, Bld("test_silo")), hud.Buildable);   // needs a refinery
        Assert.DoesNotContain((Structure, Bld("test_turret")), hud.Buildable); // defences are their own category
        RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == power);
        hud = sim.HudFor(0);
        Assert.Equal(power, hud.Queues.Single(q => q.Category == Structure).ReadyTypeIndex);
        Assert.DoesNotContain(hud.Buildable, b => b.Category == Structure);   // a ready item blocks the structure queue
    }

    [Fact]
    public void SaveLoad_MidProduction_ContinuesWithIdenticalHashes()
    {
        var sim = Base(cash: 2000);
        EntityId barracks = BuildingOf(sim, 8, 4), turretless = BuildingOf(sim, 4, 8);
        sim.Buildings.Health[turretless.Index] = 900;
        Step(sim, Rally(sim, barracks, 18, 18), Build(sim, Infantry, Inf("test_rifleman")), Build(sim, Infantry, Inf("test_engineer")),
            Build(sim, Vehicle, Veh("test_jeep")), Build(sim, Structure, Bld("test_power")), Repair(sim, turretless));
        Run(sim, 23);
        var ms = new MemoryStream(); sim.Save(ms); ms.Position = 0;
        var loaded = Simulation.Load(ms, sim.Setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(sim.ComputeSubHashes().Single(h => h.Part == "production"), loaded.ComputeSubHashes().Single(h => h.Part == "production"));
        for (int t = 0; t < 120; t++)
        {
            Command[] cmds = t == 40 ? new[] { Cancel(sim, Vehicle, 0) } : None;
            sim.Step(cmds); loaded.Step(cmds);
            Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.Equal(sim.Infantry.Slots.Count, loaded.Infantry.Slots.Count);
        Assert.True(sim.Infantry.Slots.Count >= 1);
    }
}
