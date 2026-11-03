using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Movement.MoveWorld;

namespace Rts.Sim.Tests.Movement;

public class PathFollowingTests
{
    [Fact]
    public void Vehicle_RoutesAroundARockWall()
    {
        // wall at x=10 from y=0..9; the only way through is the gap at y=10..11
        var map = Grid(20, 12, (land, w) => { for (int y = 0; y <= 9; y++) land[y * w + 10] = LandType.Rock; });
        var sim = NewSim(map);
        var id = sim.SpawnVehicle(0, Tank, new CellCoord(3, 5));
        var goal = new CellCoord(17, 5);
        bool wentThroughGap = false;
        sim.Step(new[] { Move(id, ActorKind.Vehicle, goal.X, goal.Y) });
        RunUntil(sim, () =>
        {
            CellCoord c = CellOf(sim, id);
            Assert.NotEqual(LandType.Rock, sim.Map.LandAt(c));
            if (c.X == 10 && c.Y >= 10) wentThroughGap = true;
            return sim.Units.Mission[id.Index] == MissionKind.Guard;
        }, 1500);
        Assert.True(wentThroughGap);
        Assert.Equal(LeptonCoord.CenterOf(goal), sim.Units.Position[id.Index]);
    }

    [Fact]
    public void WheeledVehicle_IsFasterOnARoad()
    {
        // two identical wheeled tanks, one on a road row (125%) and one on a clear row (100%)
        // a rock row keeps the clear-row tank from detouring onto the road
        var map = Grid(40, 10, (land, w) =>
        {
            for (int x = 0; x < 40; x++) { land[2 * w + x] = LandType.Road; land[4 * w + x] = LandType.Rock; }
        });
        var sim = NewSim(map);
        var onRoad = sim.SpawnVehicle(0, Wheeled, new CellCoord(2, 2));
        var onClear = sim.SpawnVehicle(0, Wheeled, new CellCoord(2, 7));
        sim.Step(new[] { Move(onRoad, ActorKind.Vehicle, 34, 2), Move(onClear, ActorKind.Vehicle, 34, 7) with { Sequence = 1 } });
        int roadTicks = 0, clearTicks = 0;
        for (int t = 1; t < 600 && (roadTicks == 0 || clearTicks == 0); t++)
        {
            sim.Step(None);
            if (roadTicks == 0 && CellOf(sim, onRoad).X == 34 && sim.Units.Position[onRoad.Index] == LeptonCoord.CenterOf(new CellCoord(34, 2))) roadTicks = t;
            if (clearTicks == 0 && CellOf(sim, onClear).X == 34 && sim.Units.Position[onClear.Index] == LeptonCoord.CenterOf(new CellCoord(34, 7))) clearTicks = t;
        }
        Assert.True(roadTicks > 0 && clearTicks > 0);
        Assert.True(roadTicks * 10 < clearTicks * 9, $"road {roadTicks} ticks vs clear {clearTicks}");
    }

    [Fact]
    public void RoughGround_SlowsAVehicle_ByItsSpeedPercent()
    {
        var map = Grid(30, 8, (land, w) =>
        {
            for (int x = 0; x < 30; x++) { land[5 * w + x] = LandType.Rough; land[3 * w + x] = LandType.Rock; land[4 * w + x] = LandType.Rock; land[6 * w + x] = LandType.Rock; }
        });
        var sim = NewSim(map);
        var rough = sim.SpawnVehicle(0, Tank, new CellCoord(2, 5));
        var clear = sim.SpawnVehicle(0, Tank, new CellCoord(2, 1));
        sim.Step(new[] { Move(rough, ActorKind.Vehicle, 12, 5), Move(clear, ActorKind.Vehicle, 12, 1) with { Sequence = 1 } });
        int a = 0, b = 0;
        for (int t = 1; t < 400 && (a == 0 || b == 0); t++)
        {
            sim.Step(None);
            if (a == 0 && sim.Units.Mission[rough.Index] == MissionKind.Move && sim.Units.NextMission[rough.Index] == MissionKind.Guard) a = t;
            if (b == 0 && sim.Units.Mission[clear.Index] == MissionKind.Move && sim.Units.NextMission[clear.Index] == MissionKind.Guard) b = t;
        }
        Assert.True(a > 0 && b > 0);
        Assert.True(a > b + b / 5, $"rough {a} vs clear {b}");   // 75% speed: about a third longer
    }

    [Fact]
    public void Infantry_WalksThePath_ClaimingOneSlotPerCell()
    {
        var map = Grid(20, 12, (land, w) => { for (int y = 0; y <= 9; y++) land[y * w + 10] = LandType.Rock; });
        var sim = NewSim(map);
        var id = sim.SpawnInfantry(0, Rifle, new CellCoord(3, 5), 2);
        sim.Step(new[] { Move(id, ActorKind.Infantry, 17, 5) });
        RunUntil(sim, () => sim.Infantry.Mission[id.Index] == MissionKind.Guard, 2000);
        Assert.Equal(new CellCoord(17, 5), CellOfInf(sim, id));
        Assert.Equal(sim.Infantry.Position[id.Index], Rts.Sim.World.InfantryStore.SlotPosition(new CellCoord(17, 5), 2));
    }

    [Fact]
    public void Move_ToCurrentCell_JustSettlesOnTheCentre()
    {
        var sim = NewSim(Grid(10, 10));
        var id = sim.SpawnVehicle(0, Tank, new CellCoord(4, 4));
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 4, 4) });
        RunUntil(sim, () => sim.Units.Mission[id.Index] == MissionKind.Guard, 20);
        Assert.Equal(LeptonCoord.CenterOf(new CellCoord(4, 4)), sim.Units.Position[id.Index]);
    }

    [Fact]
    public void Move_ToAnUnreachableCell_StopsAtTheClosestReachableOne()
    {
        // goal enclosed by rock: the vehicle stops in the reachable cell nearest to it and the mission ends
        var map = Grid(16, 16, (land, w) =>
        {
            for (int x = 8; x <= 10; x++) for (int y = 8; y <= 10; y++) land[y * w + x] = LandType.Rock;
            land[9 * w + 9] = LandType.Clear;
        });
        var sim = NewSim(map);
        var id = sim.SpawnVehicle(0, Tank, new CellCoord(2, 9));
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 9, 9) });
        RunUntil(sim, () => sim.Units.Mission[id.Index] == MissionKind.Guard, 1000);
        CellCoord c = CellOf(sim, id);
        Assert.Equal(2, Math.Max(Math.Abs(c.X - 9), Math.Abs(c.Y - 9)));   // just outside the ring of rock
    }

    [Fact]
    public void Stop_MidStep_ReleasesTheReservedCell()
    {
        var sim = NewSim(Grid(20, 8));
        var id = sim.SpawnVehicle(0, Tank, new CellCoord(2, 4));
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 15, 4) });
        RunUntil(sim, () => sim.Units.NextCellReserved[id.Index] && CellOf(sim, id).X == 2
            && sim.Units.Position[id.Index].X > LeptonCoord.CenterOf(new CellCoord(2, 4)).X, 100);
        Assert.True(sim.Map.VehicleAt(new CellCoord(3, 4)) == id);
        var stop = new Command(sim.Tick, 0, 0, CommandKind.Stop, id, default, default, (int)ActorKind.Vehicle);
        Run(sim, 1, new[] { stop });
        Assert.True(sim.Map.VehicleAt(new CellCoord(3, 4)).IsNone);
        Assert.False(sim.Units.NextCellReserved[id.Index]);
        Run(sim, 5);
        // a later Move starts from the cell's centre again
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 6, 4, sim.Tick) });
        RunUntil(sim, () => sim.Units.Mission[id.Index] == MissionKind.Guard, 400);
        Assert.Equal(LeptonCoord.CenterOf(new CellCoord(6, 4)), sim.Units.Position[id.Index]);
    }

    [Fact]
    public void NewMove_WhileDriving_FinishesTheStepThenTurns()
    {
        var sim = NewSim(Grid(20, 12));
        var id = sim.SpawnVehicle(0, Tank, new CellCoord(2, 4));
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 15, 4) });
        Run(sim, 12);
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 6, 9, sim.Tick) });
        AssertConsistent(sim);
        RunUntil(sim, () => sim.Units.Mission[id.Index] == MissionKind.Guard, 600);
        Assert.Equal(LeptonCoord.CenterOf(new CellCoord(6, 9)), sim.Units.Position[id.Index]);
    }

    [Fact]
    public void SpeedIsAtLeastOne_WhenThePercentRoundsToZero()
    {
        var table = new TerrainTable(Enumerable.Repeat(1, RuleCounts.LandTypes * RuleCounts.Locomotions).ToArray(), new bool[RuleCounts.LandTypes]);
        var rules = new SimRules(new[] { Determinism.ScriptedGame.Vehicle("slow", 5, 10) }, Array.Empty<InfantryType>(),
            Determinism.ScriptedGame.Buildings(), Array.Empty<WeaponType>(), Array.Empty<WarheadType>(), table, Array.Empty<EffectDef>());
        var sim = new Simulation(new SimSetup(1, Grid(8, 8), 1, rules, 1));
        var id = sim.SpawnVehicle(0, 0, new CellCoord(2, 2));
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 3, 2) });
        for (int t = 0; t < 20; t++) sim.Step(None);   // 8 ticks turning east (D-042), then 1 lepton per tick
        int moved = sim.Units.Position[id.Index].X - LeptonCoord.CenterOf(new CellCoord(2, 2)).X;
        Assert.InRange(moved, 5, 20);
    }
}
