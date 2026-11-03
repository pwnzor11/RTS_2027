using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.World;
using static Rts.Sim.Tests.Movement.MoveWorld;

namespace Rts.Sim.Tests.Movement;

public class OccupancyTests
{
    [Fact]
    public void Spawn_ClaimsTheCell_AndASecondSpawnOnTheSameCellDoesNotStealIt()
    {
        var sim = NewSim(Grid(10, 10));
        var a = sim.SpawnVehicle(0, Tank, new CellCoord(3, 3));
        var b = sim.SpawnVehicle(0, Tank, new CellCoord(3, 3));
        Assert.Equal(a, sim.Map.VehicleAt(new CellCoord(3, 3)));
        sim.Units.Health[b.Index] = 0;           // the duplicate dies: the first one's claim must survive
        sim.Step(None);
        Assert.Equal(a, sim.Map.VehicleAt(new CellCoord(3, 3)));
    }

    [Fact]
    public void InfantrySpawn_OnATakenSlot_MovesToAFreeOne()
    {
        var sim = NewSim(Grid(10, 10));
        var a = sim.SpawnInfantry(0, Rifle, new CellCoord(3, 3), 2);
        var b = sim.SpawnInfantry(0, Rifle, new CellCoord(3, 3), 2);
        Assert.Equal(2, sim.Infantry.SubSlot[a.Index]);
        Assert.NotEqual(2, sim.Infantry.SubSlot[b.Index]);
        Assert.Equal(InfantryStore.SlotPosition(new CellCoord(3, 3), sim.Infantry.SubSlot[b.Index]), sim.Infantry.Position[b.Index]);
        AssertConsistent(sim);
    }

    [Fact]
    public void DeadVehicle_ReleasesItsCell_AndItsReservation()
    {
        var sim = NewSim(Grid(14, 6));
        var id = sim.SpawnVehicle(0, Tank, new CellCoord(2, 3));
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 10, 3) });
        RunUntil(sim, () => sim.Units.NextCellReserved[id.Index] && CellOf(sim, id) == new CellCoord(2, 3)
            && sim.Units.Position[id.Index].X > LeptonCoord.CenterOf(new CellCoord(2, 3)).X, 100);
        Assert.Equal(id, sim.Map.VehicleAt(new CellCoord(2, 3)));
        Assert.Equal(id, sim.Map.VehicleAt(new CellCoord(3, 3)));
        sim.Units.Health[id.Index] = 0;
        sim.Step(None);
        Assert.False(sim.Units.Slots.IsAlive(id));
        Assert.True(sim.Map.VehicleAt(new CellCoord(2, 3)).IsNone);
        Assert.True(sim.Map.VehicleAt(new CellCoord(3, 3)).IsNone);
        AssertConsistent(sim);
    }

    [Fact]
    public void DeadInfantry_ReleasesItsSlotAndReservation_AndAPendingPathRequest()
    {
        var sim = NewSim(Grid(14, 6));
        var id = sim.SpawnInfantry(0, Rifle, new CellCoord(2, 3), 1);
        sim.Step(new[] { Move(id, ActorKind.Infantry, 10, 3) });
        RunUntil(sim, () => sim.Infantry.NextCellReserved[id.Index], 50);
        sim.Infantry.Health[id.Index] = 0;
        sim.Step(None);
        Assert.False(sim.Infantry.Slots.IsAlive(id));
        for (int x = 0; x < 14; x++)
            for (int s = 0; s < 5; s++) Assert.True(sim.Map.InfantryAt(new CellCoord(x, 3), s).IsNone);

        // dying while the path request is still queued
        var other = sim.SpawnInfantry(0, Rifle, new CellCoord(5, 5), 0);
        sim.Step(new[] { Move(other, ActorKind.Infantry, 12, 1, sim.Tick) });
        sim.Infantry.Health[other.Index] = 0;
        Run(sim, 5);
        Assert.False(sim.Infantry.Slots.IsAlive(other));
        AssertConsistent(sim);
    }

    [Fact]
    public void OccupancyStaysConsistent_WhenUnitsDieAtRandomMoments()
    {
        var sim = NewSim(Grid(30, 30));
        var ids = new List<EntityId>();
        var cmds = new List<Command>();
        for (int k = 0; k < 12; k++)
        {
            ids.Add(sim.SpawnVehicle(0, k % 3 == 0 ? Light : Tank, new CellCoord(2 + k, 3)));
            cmds.Add(Move(ids[k], ActorKind.Vehicle, 25 - k, 24, 0, 0, (ushort)k));
        }
        for (int t = 0; t < 400; t++)
        {
            sim.Step(cmds);
            if (t % 37 == 20) { int victim = ids[(t / 37) % ids.Count].Index; if (sim.Units.Slots.IsIndexAlive(victim)) sim.Units.Health[victim] = 0; }
            AssertConsistent(sim);
        }
    }

    [Fact]
    public void SaveAndLoad_MidPath_GiveIdenticalHashes()
    {
        // two groups heading through a corridor in opposite directions: waits, repaths, sidesteps and reservations are all in flight
        var map = Grid(30, 11, (land, w) =>
        {
            for (int x = 10; x <= 18; x++) { land[4 * w + x] = LandType.Rock; land[6 * w + x] = LandType.Rock; }
        });
        var setupSim = NewSim(map);
        var cmds = new List<Command>();
        ushort seq = 0;
        for (int k = 0; k < 4; k++)
        {
            var left = setupSim.SpawnVehicle(0, Tank, new CellCoord(2, 3 + k));
            var right = setupSim.SpawnVehicle(1, k == 1 ? Light : Tank, new CellCoord(27, 3 + k));
            cmds.Add(Move(left, ActorKind.Vehicle, 27, 5, 0, 0, seq++));
            cmds.Add(Move(right, ActorKind.Vehicle, 2, 5, 0, 1, seq++));
        }
        for (int k = 0; k < 6; k++)
        {
            var inf = setupSim.SpawnInfantry(0, Rifle, new CellCoord(3 + k, 8), k % 5);
            cmds.Add(Move(inf, ActorKind.Infantry, 26, 3, 3, 0, seq++));
        }
        var setup = setupSim.Setup;
        var straight = new Simulation(setup);
        // replay the spawns into a second simulation with identical order
        for (int k = 0; k < 4; k++) { straight.SpawnVehicle(0, Tank, new CellCoord(2, 3 + k)); straight.SpawnVehicle(1, k == 1 ? Light : Tank, new CellCoord(27, 3 + k)); }
        for (int k = 0; k < 6; k++) straight.SpawnInfantry(0, Rifle, new CellCoord(3 + k, 8), k % 5);

        for (int t = 0; t < 75; t++) straight.Step(cmds);
        Assert.Contains(Enumerable.Range(1, 8), i => straight.Units.NextCellReserved[i]);   // really in the middle of moving
        var ms = new MemoryStream();
        straight.Save(ms);
        ms.Position = 0;
        var loaded = Simulation.Load(ms, setup);
        Assert.Equal(straight.ComputeStateHash(), loaded.ComputeStateHash());
        for (int t = 0; t < 400; t++)
        {
            straight.Step(cmds); loaded.Step(cmds);
            Assert.Equal(straight.ComputeStateHash(), loaded.ComputeStateHash());
            AssertConsistent(loaded);
        }
    }

    [Fact]
    public void ScriptedGame_NeverOverlapsVehicles_AndKeepsOccupancyConsistent()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        var script = ScriptedGame.Script(500);
        for (int t = 0; t < 500; t++)
        {
            sim.Step(script);
            AssertConsistent(sim);
        }
    }

    [Fact]
    public void ScriptedGame_TwoRuns_GiveTheSameHashes()
    {
        var script = ScriptedGame.Script(300);
        ulong Final()
        {
            var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
            for (int t = 0; t < 300; t++) sim.Step(script);
            return sim.ComputeStateHash();
        }
        Assert.Equal(Final(), Final());
    }

    [Fact]
    public void InfantryAndVehicles_NeverShareACell_OnAHandfulOfCrossingOrders()
    {
        var sim = NewSim(Grid(16, 16));
        var cmds = new List<Command>();
        ushort seq = 0;
        for (int k = 0; k < 4; k++)
        {
            var v = sim.SpawnVehicle(0, k % 2 == 0 ? Light : Tank, new CellCoord(1, 2 + 3 * k));
            cmds.Add(Move(v, ActorKind.Vehicle, 14, 2 + 3 * (3 - k), 0, 0, seq++));
        }
        for (int k = 0; k < 8; k++)
        {
            var inf = sim.SpawnInfantry(0, Rifle, new CellCoord(14, 1 + 2 * k), k % 5);
            cmds.Add(Move(inf, ActorKind.Infantry, 1, 14 - 2 * k, 0, 0, seq++));
        }
        for (int t = 0; t < 800; t++)
        {
            sim.Step(cmds);
            AssertConsistent(sim);
            for (int i = 1; i < sim.Infantry.Capacity; i++)
                if (sim.Infantry.Slots.IsIndexAlive(i))
                    Assert.True(sim.Map.VehicleAt(sim.Infantry.SlotCell[i]).IsNone, $"infantry {i} shares a cell with a vehicle at tick {t}");
        }
    }

    [Fact]
    public void ScatterCommand_MovesInfantryToAFreeNeighbourSlot()
    {
        var sim = NewSim(Grid(10, 10));
        var id = sim.SpawnInfantry(0, Rifle, new CellCoord(5, 5), 0);
        sim.Step(new[] { new Command(0, 0, 0, CommandKind.Scatter, id, default, default, (int)ActorKind.Infantry) });
        RunUntil(sim, () => sim.Infantry.Mission[id.Index] == MissionKind.Guard, 100);
        CellCoord c = sim.Infantry.SlotCell[id.Index];
        Assert.NotEqual(new CellCoord(5, 5), c);
        Assert.Equal(1, Math.Max(Math.Abs(c.X - 5), Math.Abs(c.Y - 5)));
    }
}
