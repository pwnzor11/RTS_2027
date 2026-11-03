// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.Missions;

public class MissionTests
{
    private static readonly Command[] None = Array.Empty<Command>();

    private static Simulation Make(int speed, out EntityId veh, out EntityId inf)
    {
        var rules = ScriptedGame.RulesWith(new[] { ScriptedGame.Vehicle("t", speed, 10) },
            new[] { ScriptedGame.Infantry("i", 20, 10) });
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(32, 32), 1, rules, 1));
        veh = sim.SpawnVehicle(0, 0, new CellCoord(10, 10));
        inf = sim.SpawnInfantry(0, 0, new CellCoord(5, 5), 0);
        return sim;
    }

    private static Command Move(EntityId id, ActorKind k, int x, int y, int tick = 0) =>
        new(tick, 0, 0, CommandKind.Move, id, default, new CellCoord(x, y), (int)k);

    [Fact]
    public void AssignMission_OnlyQueues_AndCommencesAtNextRunMissions()
    {
        var sim = Make(30, out var id, out _);
        Assert.True(sim.AssignMission(new ActorRef(ActorKind.Vehicle, id), MissionKind.Move, new CellCoord(10, 5)));
        Assert.Equal(MissionKind.None, sim.Units.Mission[id.Index]);
        Assert.Equal(MissionKind.Move, sim.Units.NextMission[id.Index]);
        sim.Step(None);
        Assert.Equal(MissionKind.Move, sim.Units.Mission[id.Index]);
        Assert.Equal(MissionKind.None, sim.Units.NextMission[id.Index]);
    }

    [Fact]
    public void MissionQueuedByHandler_TakesEffectNextTick()
    {
        var sim = Make(300, out var id, out _);
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 10, 9) });   // one cell north; tick 0 only asks for the path
        Assert.Equal(LeptonCoord.CenterOf(new CellCoord(10, 10)), sim.Units.Position[id.Index]);
        sim.Step(None);                                           // tick 1: path arrived, drives in one step
        Assert.Equal(LeptonCoord.CenterOf(new CellCoord(10, 9)), sim.Units.Position[id.Index]);
        Assert.Equal(MissionKind.Move, sim.Units.Mission[id.Index]);
        Assert.Equal(MissionKind.Guard, sim.Units.NextMission[id.Index]);
        sim.Step(None);
        Assert.Equal(MissionKind.Guard, sim.Units.Mission[id.Index]);
    }

    [Fact]
    public void WakeTick_SkipsSleepingActors()
    {
        var sim = Make(30, out var id, out _);
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 10, 5) });
        // Park the actor: the handler must not run until WakeTick <= Tick.
        sim.Units.WakeTick[id.Index] = sim.Tick + 5;
        var pos = sim.Units.Position[id.Index];
        for (int i = 0; i < 5; i++) { sim.Step(None); Assert.Equal(pos, sim.Units.Position[id.Index]); }
        sim.Step(None);
        Assert.NotEqual(pos, sim.Units.Position[id.Index]);
    }

    [Fact]
    public void Guard_SleepsFifteenTicks()
    {
        var sim = Make(300, out var id, out _);
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 10, 9) });   // tick 0: path requested
        sim.Step(None);                                   // tick 1: arrives, Guard queued
        sim.Step(None);                                   // Guard commences and runs at tick 2
        Assert.Equal(MissionKind.Guard, sim.Units.Mission[id.Index]);
        Assert.Equal(2 + 15, sim.Units.WakeTick[id.Index]);
    }

    [Fact]
    public void Vehicle_TurnsThenDrives()
    {
        var sim = Make(30, out var id, out _);            // TurnRate 8, facing north, order east: diff 64
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 15, 10) });
        var start = LeptonCoord.CenterOf(new CellCoord(10, 10));
        Assert.Equal(0, sim.Units.Facing[id.Index]);      // tick 0 only asks for the path
        sim.Step(None);
        int turnTicks = 1;
        Assert.Equal(8, sim.Units.Facing[id.Index]);
        while (sim.Units.Facing[id.Index] != 64) { Assert.Equal(start, sim.Units.Position[id.Index]); sim.Step(None); turnTicks++; }
        Assert.Equal(8, turnTicks);                       // ceil(64 / 8)
        Assert.Equal(start, sim.Units.Position[id.Index]);
        sim.Step(None);
        Assert.Equal(start.X + 30, sim.Units.Position[id.Index].X);
        Assert.Equal(start.Y, sim.Units.Position[id.Index].Y);
    }

    [Fact]
    public void Vehicle_TurnTicksAreCeilOfDifferenceOverRate()
    {
        var sim = Make(30, out var id, out _);            // north -> south = 128 = 16 ticks at rate 8
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 10, 15) });
        sim.Step(None);                                   // tick 0 asks for the path, turning starts at tick 1
        int ticks = 1;
        while (sim.Units.Facing[id.Index] != 128) { sim.Step(None); ticks++; }
        Assert.Equal(16, ticks);
        Assert.Equal(LeptonCoord.CenterOf(new CellCoord(10, 10)), sim.Units.Position[id.Index]);
    }

    [Fact]
    public void Stop_GoesToGuard_AndHaltsMovement()
    {
        var sim = Make(30, out var id, out _);
        sim.Step(new[] { Move(id, ActorKind.Vehicle, 10, 2) });
        sim.Step(None);
        var stop = new Command(2, 0, 0, CommandKind.Stop, id, default, default, (int)ActorKind.Vehicle);
        sim.Step(new[] { stop });
        Assert.Equal(MissionKind.Stop, sim.Units.Mission[id.Index]);
        sim.Step(None);
        Assert.Equal(MissionKind.Guard, sim.Units.Mission[id.Index]);
        var pos = sim.Units.Position[id.Index];
        for (int i = 0; i < 10; i++) sim.Step(None);
        Assert.Equal(pos, sim.Units.Position[id.Index]);
    }

    [Fact]
    public void Infantry_TurnsInstantly_AndMoves()
    {
        var sim = Make(30, out _, out var id);
        sim.Step(new[] { Move(id, ActorKind.Infantry, 10, 5) });
        sim.Step(None);                                   // tick 0 asks for the path
        Assert.Equal(64, sim.Infantry.Facing[id.Index]);
        Assert.Equal(LeptonCoord.CenterOf(new CellCoord(5, 5)).X + 20, sim.Infantry.Position[id.Index].X);
    }

    [Fact]
    public void Infantry_MovesToItsSubSlotInTheTargetCell()
    {
        var sim = Make(30, out _, out _);
        var corner = sim.SpawnInfantry(0, 0, new CellCoord(5, 7), 3);
        sim.Step(new[] { Move(corner, ActorKind.Infantry, 9, 7) });
        for (int i = 0; i < 60; i++) sim.Step(None);
        var c = LeptonCoord.CenterOf(new CellCoord(9, 7));
        Assert.Equal(new LeptonCoord(c.X - 64, c.Y + 64), sim.Infantry.Position[corner.Index]);
        Assert.Equal(MissionKind.Guard, sim.Infantry.Mission[corner.Index]);
    }

    [Fact]
    public void MoveCommand_RoutesByArgKind()
    {
        var sim = Make(30, out var veh, out var inf);
        // same raw index (1) in both stores: Arg picks the store
        Assert.Equal(veh.Index, inf.Index);
        sim.Step(new[] { Move(inf, ActorKind.Infantry, 9, 9) });
        Assert.Equal(MissionKind.Move, sim.Infantry.Mission[inf.Index]);
        Assert.Equal(MissionKind.None, sim.Units.Mission[veh.Index]);
        sim.Step(new[] { Move(veh, ActorKind.Building, 9, 9, 1) });   // unsupported kind: ignored
        Assert.Equal(MissionKind.None, sim.Units.Mission[veh.Index]);
    }

    [Fact]
    public void TryResolve_ChecksStoreAndGeneration()
    {
        var sim = Make(30, out var veh, out var inf);
        Assert.True(sim.TryResolve(new ActorRef(ActorKind.Vehicle, veh)));
        Assert.True(sim.TryResolve(new ActorRef(ActorKind.Infantry, inf)));
        Assert.False(sim.TryResolve(new ActorRef(ActorKind.Infantry, new EntityId(inf.Index, (ushort)(inf.Generation + 1)))));
        Assert.False(sim.TryResolve(new ActorRef(ActorKind.Building, veh)));
        Assert.False(sim.TryResolve(new ActorRef(ActorKind.Vehicle, new EntityId(9, 1))));
    }
}
