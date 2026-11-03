using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Movement;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using static Rts.Sim.Tests.Movement.MoveWorld;

namespace Rts.Sim.Tests.Movement;

public class BlockageTests
{
    private static Command Scatter(EntityId id, ActorKind kind, int tick, byte player = 0) =>
        new(tick, player, 0, CommandKind.Scatter, id, default, default, (int)kind);

    [Fact]
    public void TwoTanksSwappingPlacesThroughAOneWideCorridor_ResolveWithoutEverOverlapping()
    {
        // a 7-cell corridor (y=4, x=8..14) joins two open rooms; each tank starts in one room and must reach the other's start
        var map = Grid(24, 9, (land, w) =>
        {
            for (int x = 8; x <= 14; x++) { land[3 * w + x] = LandType.Rock; land[5 * w + x] = LandType.Rock; }
        });
        var sim = NewSim(map);
        var a = sim.SpawnVehicle(0, Tank, new CellCoord(3, 4));
        var b = sim.SpawnVehicle(0, Tank, new CellCoord(20, 4));
        sim.Step(new[] { Move(a, ActorKind.Vehicle, 20, 4), Move(b, ActorKind.Vehicle, 3, 4, 0, 0, 1) });
        AssertConsistent(sim);
        RunUntil(sim, () => sim.Units.Mission[a.Index] == MissionKind.Guard && sim.Units.Mission[b.Index] == MissionKind.Guard, 3000);
        Assert.Equal(LeptonCoord.CenterOf(new CellCoord(20, 4)), sim.Units.Position[a.Index]);
        Assert.Equal(LeptonCoord.CenterOf(new CellCoord(3, 4)), sim.Units.Position[b.Index]);
    }

    [Fact]
    public void TwoTanksCrossingHeadOn_InTheOpen_PassWithoutOverlap()
    {
        var sim = NewSim(Grid(24, 5));
        var a = sim.SpawnVehicle(0, Tank, new CellCoord(2, 2));
        var b = sim.SpawnVehicle(0, Tank, new CellCoord(20, 2));
        sim.Step(new[] { Move(a, ActorKind.Vehicle, 20, 2), Move(b, ActorKind.Vehicle, 2, 2, 0, 0, 1) });
        RunUntil(sim, () => sim.Units.Mission[a.Index] == MissionKind.Guard && sim.Units.Mission[b.Index] == MissionKind.Guard, 3000);
        Assert.Equal(new CellCoord(20, 2), CellOf(sim, a));
        Assert.Equal(new CellCoord(2, 2), CellOf(sim, b));
    }

    [Fact]
    public void GroupMove_OfNineTanks_EndsOnNineDistinctCells()
    {
        var sim = NewSim(Grid(28, 28));
        var ids = new List<EntityId>();
        for (int k = 0; k < 9; k++) ids.Add(sim.SpawnVehicle(0, Tank, new CellCoord(2 + k % 3, 2 + k / 3)));
        var cmds = ids.Select((id, k) => Move(id, ActorKind.Vehicle, 18, 18, 0, 0, (ushort)k)).ToArray();
        sim.Step(cmds);
        AssertConsistent(sim);
        RunUntil(sim, () => ids.All(id => sim.Units.Mission[id.Index] == MissionKind.Guard), 3000);
        var cells = ids.Select(id => CellOf(sim, id)).ToList();
        Assert.Equal(9, cells.Distinct().Count());
        // the spiral gave them the 3x3 block around the target; units shoved aside by late arrivals may sit one ring further out
        Assert.All(cells, c => Assert.True(Math.Abs(c.X - 18) <= 2 && Math.Abs(c.Y - 18) <= 2, $"{c} not near the target"));
    }

    [Fact]
    public void GroupMove_MixedKinds_GetSeparateSpreadDestinations()
    {
        var sim = NewSim(Grid(20, 20));
        var tanks = new[] { sim.SpawnVehicle(0, Tank, new CellCoord(2, 2)), sim.SpawnVehicle(0, Tank, new CellCoord(2, 4)) };
        var foot = new List<EntityId>();
        for (int k = 0; k < 7; k++) foot.Add(sim.SpawnInfantry(0, Rifle, new CellCoord(4 + k % 3, 8 + k / 3), k % 5));
        var cmds = new List<Command>();
        ushort seq = 0;
        foreach (var t in tanks) cmds.Add(Move(t, ActorKind.Vehicle, 14, 14, 0, 0, seq++));
        foreach (var f in foot) cmds.Add(Move(f, ActorKind.Infantry, 14, 14, 0, 0, seq++));
        sim.Step(cmds);
        RunUntil(sim, () => tanks.All(t => sim.Units.Mission[t.Index] == MissionKind.Guard)
            && foot.All(f => sim.Infantry.Mission[f.Index] == MissionKind.Guard), 4000);
        Assert.Equal(2, tanks.Select(t => CellOf(sim, t)).Distinct().Count());
        // no infantry shares a cell with a tank, and no cell holds more than 5 soldiers
        var tankCells = tanks.Select(t => CellOf(sim, t)).ToHashSet();
        Assert.All(foot, f => Assert.DoesNotContain(CellOfInf(sim, f), tankCells));
        Assert.True(foot.GroupBy(f => CellOfInf(sim, f)).All(g => g.Count() <= 5));
    }

    [Fact]
    public void IdleAlly_InTheWay_IsShoved_AndTheMoverGetsThrough()
    {
        // corridor so the mover cannot simply drive around
        var map = Grid(20, 7, (land, w) =>
        {
            for (int x = 4; x <= 12; x++) { land[2 * w + x] = LandType.Rock; land[4 * w + x] = LandType.Rock; }
        });
        var sim = NewSim(map);
        var mover = sim.SpawnVehicle(0, Tank, new CellCoord(2, 3));
        var idle = sim.SpawnVehicle(0, Tank, new CellCoord(7, 3));
        sim.Step(new[] { Move(mover, ActorKind.Vehicle, 16, 3) });
        RunUntil(sim, () => sim.Units.Mission[mover.Index] == MissionKind.Guard, 1500);
        Assert.Equal(new CellCoord(16, 3), CellOf(sim, mover));
        Assert.True(sim.Units.Slots.IsAlive(idle));
        Assert.NotEqual(new CellCoord(7, 3), CellOf(sim, idle));
    }

    [Fact]
    public void ScatterCommand_MovesToTheFirstFreeNeighbour_InFixedOrder()
    {
        var sim = NewSim(Grid(12, 12));
        var id = sim.SpawnVehicle(0, Tank, new CellCoord(5, 5));
        // block every neighbour except SW (4,6) and W (4,5): the pick depends only on the RNG start index
        foreach (var c in new[] { (5, 4), (6, 4), (6, 5), (6, 6), (5, 6), (4, 4) })
            sim.SpawnVehicle(1, Light, new CellCoord(c.Item1, c.Item2));
        sim.Step(new[] { Scatter(id, ActorKind.Vehicle, 0) });
        RunUntil(sim, () => sim.Units.Mission[id.Index] == MissionKind.Guard, 100);
        CellCoord end = CellOf(sim, id);
        Assert.True(end == new CellCoord(4, 6) || end == new CellCoord(4, 5), $"ended at {end}");
        AssertConsistent(sim);
    }

    [Fact]
    public void ScatterCommand_IsDeterministic()
    {
        CellCoord Once()
        {
            var sim = NewSim(Grid(12, 12), seed: 77);
            var id = sim.SpawnVehicle(0, Tank, new CellCoord(5, 5));
            sim.Step(new[] { Scatter(id, ActorKind.Vehicle, 0) });
            Run(sim, 60);
            return CellOf(sim, id);
        }
        Assert.Equal(Once(), Once());
    }

    [Fact]
    public void Crusher_KillsEnemyInfantry_InItsPath()
    {
        var sim = NewSim(Grid(14, 7));
        var tank = sim.SpawnVehicle(0, Tank, new CellCoord(2, 3));
        var foe = sim.SpawnInfantry(1, Rifle, new CellCoord(6, 3), 0);
        var foe2 = sim.SpawnInfantry(1, Rifle, new CellCoord(6, 3), 3);
        sim.Step(new[] { Move(tank, ActorKind.Vehicle, 10, 3) });
        RunUntil(sim, () => sim.Units.Mission[tank.Index] == MissionKind.Guard, 600);
        Assert.False(sim.Infantry.Slots.IsAlive(foe));
        Assert.False(sim.Infantry.Slots.IsAlive(foe2));
        Assert.Equal(new CellCoord(10, 3), CellOf(sim, tank));
        Assert.True(sim.Map.InfantryAt(new CellCoord(6, 3), 0).IsNone);   // slots released by the cleanup
    }

    [Fact]
    public void NonCrusher_RoutesAroundEnemyInfantry_WhichSurvives()
    {
        var sim = NewSim(Grid(14, 7));
        var tank = sim.SpawnVehicle(0, Light, new CellCoord(2, 3));
        var foe = sim.SpawnInfantry(1, Rifle, new CellCoord(6, 3), 0);
        sim.Step(new[] { Move(tank, ActorKind.Vehicle, 10, 3) });
        RunUntil(sim, () => sim.Units.Mission[tank.Index] == MissionKind.Guard, 800);
        Assert.True(sim.Infantry.Slots.IsAlive(foe));
        Assert.Equal(30, sim.Infantry.Health[foe.Index]);
        Assert.Equal(new CellCoord(10, 3), CellOf(sim, tank));
    }

    [Fact]
    public void Crusher_DoesNotCrushAlliedInfantry_ItAsksThemToMove()
    {
        var sim = NewSim(Grid(14, 7));
        var tank = sim.SpawnVehicle(0, Tank, new CellCoord(2, 3));
        var friend = sim.SpawnInfantry(0, Rifle, new CellCoord(6, 3), 0);
        sim.Step(new[] { Move(tank, ActorKind.Vehicle, 10, 3) });
        RunUntil(sim, () => sim.Units.Mission[tank.Index] == MissionKind.Guard, 1000);
        Assert.True(sim.Infantry.Slots.IsAlive(friend));
        Assert.Equal(30, sim.Infantry.Health[friend.Index]);
        Assert.Equal(new CellCoord(10, 3), CellOf(sim, tank));
    }

    [Fact]
    public void TakenFinalCell_MoverStopsShort()
    {
        // an enemy tank (never moves) sits on the goal
        var sim = NewSim(Grid(14, 7));
        var mover = sim.SpawnVehicle(0, Tank, new CellCoord(2, 3));
        sim.SpawnVehicle(1, Tank, new CellCoord(9, 3));
        sim.Step(new[] { Move(mover, ActorKind.Vehicle, 9, 3) });
        RunUntil(sim, () => sim.Units.Mission[mover.Index] == MissionKind.Guard, 1000);
        Assert.Equal(new CellCoord(8, 3), CellOf(sim, mover));
    }

    [Fact]
    public void EnemyTankBlockingACorridor_MoverWaitsThenGivesUp_NeverOverlapping()
    {
        var map = Grid(16, 5, (land, w) => { for (int x = 4; x <= 10; x++) { land[1 * w + x] = LandType.Rock; land[3 * w + x] = LandType.Rock; } });
        var sim = NewSim(map);
        var mover = sim.SpawnVehicle(0, Tank, new CellCoord(1, 2));
        var foe = sim.SpawnVehicle(1, Tank, new CellCoord(7, 2));
        sim.Step(new[] { Move(mover, ActorKind.Vehicle, 14, 2) });
        Run(sim, 400);
        Assert.Equal(new CellCoord(7, 2), CellOf(sim, foe));
        Assert.True(CellOf(sim, mover).X < 7);
    }

    [Fact]
    public void BuildingPlacedOnTheRoute_CausesARepath()
    {
        var sim = NewSim(Grid(24, 12));
        var id = sim.SpawnVehicle(0, Tank, new CellCoord(2, 5));
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 20, 5) });
        Run(sim, 40);
        // turret (1x1) on the route, a few cells ahead
        var cell = new CellCoord(CellOf(sim, id).X + 4, 5);
        Assert.NotEqual(EntityId.None, Boundary.LateSpawn.Building(sim, 0, 2, cell));
        RunUntil(sim, () => sim.Units.Mission[id.Index] == MissionKind.Guard, 1500);
        Assert.Equal(new CellCoord(20, 5), CellOf(sim, id));
    }

    [Fact]
    public void Infantry_NeverMoreThanFivePerCell()
    {
        var sim = NewSim(Grid(16, 8));
        var cmds = new List<Command>();
        var ids = new List<EntityId>();
        for (int k = 0; k < 8; k++)
        {
            ids.Add(sim.SpawnInfantry(0, Rifle, new CellCoord(1 + k, 1), 0));
            cmds.Add(Move(ids[k], ActorKind.Infantry, 12, 5, 4 * k, 0, (ushort)k));   // one per 4 ticks: not one batch
        }
        for (int t = 0; t < 1500; t++)
        {
            sim.Step(cmds);
            AssertConsistent(sim);
            foreach (var g in ids.GroupBy(i => sim.Infantry.SlotCell[i.Index]))
                Assert.True(g.Count() <= 5, $"{g.Count()} infantry in {g.Key}");
        }
        Assert.All(ids, i => Assert.Equal(MissionKind.Guard, sim.Infantry.Mission[i.Index]));
        int inTarget = ids.Count(i => sim.Infantry.SlotCell[i.Index] == new CellCoord(12, 5));
        Assert.Equal(5, inTarget);           // the cell fills up; the others stop next to it
    }

    [Fact]
    public void NonCrusher_BlockedByAnEnemyInfantryOnTheFinalCell_StopsShort()
    {
        var sim = NewSim(Grid(14, 7));
        var tank = sim.SpawnVehicle(0, Light, new CellCoord(2, 3));
        sim.SpawnInfantry(1, Rifle, new CellCoord(8, 3), 0);
        sim.Step(new[] { Move(tank, ActorKind.Vehicle, 8, 3) });
        RunUntil(sim, () => sim.Units.Mission[tank.Index] == MissionKind.Guard, 800);
        Assert.NotEqual(new CellCoord(8, 3), CellOf(sim, tank));
        Assert.True(Math.Abs(CellOf(sim, tank).X - 8) <= 2);
    }

    [Fact]
    public void Spiral_EnumeratesEveryCellOfEachRingOnce()
    {
        var c = new CellCoord(10, 10);
        var seen = new HashSet<CellCoord>();
        for (int n = 0; n < 81; n++)
        {
            Assert.True(Spiral.Cell(c, n, out CellCoord cell));
            Assert.True(seen.Add(cell), $"{cell} repeated at {n}");
            int ring = Math.Max(Math.Abs(cell.X - 10), Math.Abs(cell.Y - 10));
            int expected = 0;
            while ((2 * expected + 1) * (2 * expected + 1) <= n) expected++;
            Assert.Equal(expected, ring);
        }
        Assert.Equal(81, seen.Count);
        Assert.False(Spiral.Cell(c, 10_000, out _));
    }
}
