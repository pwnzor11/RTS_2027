// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.Boundary;

/// <summary>M1.5-02: setup-only mutators, total command order, command validation, Debug gating, store-full handling.</summary>
public class BoundaryTests
{
    private static readonly Command[] None = Array.Empty<Command>();

    private static Simulation NewSim(bool debug = true, SimLimits? limits = null)
    {
        var s = ScriptedGame.Setup() with { AllowDebug = debug };
        if (limits != null) s = s with { Limits = limits };
        return new Simulation(s);
    }

    private static Command Dbg(DebugOp op, byte player = 0, EntityId subject = default, CellCoord cell = default, int arg2 = 0, ushort seq = 0) =>
        new(0, player, seq, CommandKind.Debug, subject, default, cell, (int)op, arg2);

    [Fact]
    public void SetupCallsAfterStep_Throw()
    {
        var sim = NewSim();
        sim.SpawnVehicle(0, 0, new CellCoord(2, 2)); // fine before the first Step
        sim.Step(None);
        Assert.Throws<InvalidOperationException>(() => sim.SpawnVehicle(0, 0, new CellCoord(3, 3)));
        Assert.Throws<InvalidOperationException>(() => sim.SpawnInfantry(0, 0, new CellCoord(3, 3)));
        Assert.Throws<InvalidOperationException>(() => sim.PlaceBuilding(0, 2, new CellCoord(10, 10)));
        Assert.Throws<InvalidOperationException>(() =>
            sim.AssignMission(new ActorRef(ActorKind.Vehicle, sim.Units.Slots.IdOf(1)), Rts.Sim.Missions.MissionKind.Stop));
    }

    [Fact]
    public void CommandOrder_IsTotal_RegardlessOfInputOrder()
    {
        // Same player and sequence, differing only in Kind / Arg: SetCash (Arg 4) vs BurnRng (Arg 1) vs Kill of nothing.
        var c1 = Dbg(DebugOp.SetCash, arg2: 777);
        var c2 = Dbg(DebugOp.SetCash, arg2: 5);
        var c3 = Dbg(DebugOp.BurnRng);
        var c4 = new Command(0, 0, 0, CommandKind.Stop, default, default, default, 0);
        var all = new[] { c1, c2, c3, c4 };
        ulong? expected = null;
        foreach (var perm in Permutations(all))
        {
            var sim = NewSim();
            sim.Step(perm);
            ulong h = sim.ComputeStateHash();
            expected ??= h;
            Assert.Equal(expected, h);
        }
    }

    private static IEnumerable<Command[]> Permutations(Command[] items)
    {
        if (items.Length <= 1) { yield return items; yield break; }
        for (int i = 0; i < items.Length; i++)
        {
            var rest = items.Where((_, k) => k != i).ToArray();
            foreach (var p in Permutations(rest)) yield return p.Prepend(items[i]).ToArray();
        }
    }

    [Fact]
    public void LateCommand_IsCounted_FutureIgnored()
    {
        var sim = NewSim();
        sim.Step(None);
        sim.Step(None);
        sim.Step(new[]
        {
            new Command(0, 0, 0, CommandKind.Stop, default, default, default, 0),  // late
            new Command(9, 0, 1, CommandKind.Stop, default, default, default, 0),  // future
            new Command(2, 0, 2, CommandKind.Stop, default, default, default, 0),  // on time
        });
        Assert.Equal(1, sim.LateCommandCount);
    }

    [Fact]
    public void WrongPlayer_IsDropped()
    {
        var bad = NewSim();
        bad.Step(new[] { Dbg(DebugOp.SetCash, player: 5, arg2: 100) });
        var none = NewSim();
        none.Step(None);
        Assert.Equal(none.ComputeStateHash(), bad.ComputeStateHash());
    }

    [Fact]
    public void Place_ByNeutralPlayer_IsDropped()
    {
        var setup = ScriptedGame.Setup() with { Teams = new byte[] { 0, Teams.Neutral } };
        var sim = new Simulation(setup);
        sim.Step(new[] { new Command(0, 1, 0, CommandKind.Place, default, default, new CellCoord(10, 10), 2) });
        Assert.Equal(0, sim.Buildings.Slots.Count);
        var sim2 = new Simulation(setup);
        sim2.Step(new[] { new Command(0, 0, 0, CommandKind.Place, default, default, new CellCoord(10, 10), 2) });
        Assert.Equal(1, sim2.Buildings.Slots.Count);
    }

    [Fact]
    public void OutOfMapCell_MoveAttackMoveGuard_AreDropped()
    {
        var sim = NewSim();
        EntityId id = sim.SpawnVehicle(0, 0, new CellCoord(5, 5));
        var far = new CellCoord(500, 500);
        foreach (var kind in new[] { CommandKind.Move, CommandKind.AttackMove, CommandKind.Guard })
        {
            sim.Step(new[] { new Command(sim.Tick, 0, 0, kind, id, default, far, (int)ActorKind.Vehicle) });
            Assert.Equal(Rts.Sim.Missions.MissionKind.None, sim.Units.NextMission[id.Index]);
        }
    }

    [Fact]
    public void Debug_Ops_Work_WhenAllowed()
    {
        var sim = NewSim();
        EntityId v = sim.SpawnVehicle(0, 0, new CellCoord(5, 5));
        EntityId inf = sim.SpawnInfantry(0, 0, new CellCoord(8, 8));
        sim.Step(new[]
        {
            Dbg(DebugOp.SpawnVehicle, cell: new CellCoord(20, 20), arg2: 1, seq: 1),
            Dbg(DebugOp.SpawnInfantry, cell: new CellCoord(22, 22), arg2: 0, seq: 2),
            Dbg(DebugOp.SetCash, arg2: 1234, seq: 3),
            Dbg(DebugOp.Kill, subject: v, arg2: (int)ActorKind.Vehicle, seq: 4),
        });
        Assert.Equal(1, sim.Units.Slots.Count);          // spawned one, killed one
        Assert.False(sim.Units.Slots.IsAlive(v));
        Assert.Equal(2, sim.Infantry.Slots.Count);
        Assert.True(sim.Infantry.Slots.IsAlive(inf));
        Assert.Equal(1234, sim.Players.Cash[0]);
        Assert.DoesNotContain(sim.SetupLog, e => e.Cell == new CellCoord(20, 20));
    }

    [Fact]
    public void Debug_BurnRng_ChangesHash_OnlyWhenAllowed()
    {
        var a = NewSim(debug: true); var b = NewSim(debug: true);
        a.Step(new[] { Dbg(DebugOp.BurnRng) }); b.Step(None);
        Assert.NotEqual(a.ComputeStateHash(), b.ComputeStateHash());
    }

    [Fact]
    public void Debug_DoesNothing_WhenNotAllowed()
    {
        var a = NewSim(debug: false); var b = NewSim(debug: false);
        EntityId va = a.SpawnVehicle(0, 0, new CellCoord(5, 5)); b.SpawnVehicle(0, 0, new CellCoord(5, 5));
        a.Step(new[]
        {
            Dbg(DebugOp.BurnRng, seq: 0), Dbg(DebugOp.SpawnVehicle, cell: new CellCoord(9, 9), seq: 1),
            Dbg(DebugOp.SetCash, arg2: 99, seq: 2), Dbg(DebugOp.Kill, subject: va, arg2: (int)ActorKind.Vehicle, seq: 3),
        });
        b.Step(None);
        Assert.Equal(b.ComputeStateHash(), a.ComputeStateHash());
        Assert.True(a.Units.Slots.IsAlive(va));
    }

    [Fact]
    public void StoreFull_ReturnsNone_WithoutThrowing()
    {
        var sim = NewSim(limits: new SimLimits(3, 3, 3, 8)); // 2 usable slots each
        Assert.False(sim.SpawnVehicle(0, 0, new CellCoord(2, 2)).IsNone);
        Assert.False(sim.SpawnVehicle(0, 0, new CellCoord(4, 2)).IsNone);
        int logged = sim.SetupLog.Count;
        Assert.True(sim.SpawnVehicle(0, 0, new CellCoord(6, 2)).IsNone);
        Assert.Equal(logged, sim.SetupLog.Count);
        Assert.False(sim.SpawnInfantry(0, 0, new CellCoord(2, 8)).IsNone);
        Assert.False(sim.SpawnInfantry(0, 0, new CellCoord(4, 8)).IsNone);
        Assert.True(sim.SpawnInfantry(0, 0, new CellCoord(6, 8)).IsNone);
        Assert.False(sim.PlaceBuilding(0, 2, new CellCoord(20, 20)).IsNone);
        Assert.False(sim.PlaceBuilding(0, 2, new CellCoord(25, 20)).IsNone);
        Assert.True(sim.PlaceBuilding(0, 2, new CellCoord(30, 20)).IsNone);
        sim.Step(None); // still runs
    }
}
