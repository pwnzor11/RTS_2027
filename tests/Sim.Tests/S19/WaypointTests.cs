// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Tests.S18;

namespace Rts.Sim.Tests.S19;

/// <summary>S19 D-205 (M13-15): Shift-queued waypoints. The queue is part of the saved and hashed state; a plain order clears it.</summary>
public sealed class WaypointTests
{
    private static readonly CellCoord A = new(24, 20), B = new(24, 26), C = new(30, 26);

    private static Command Queue(S18World w, EntityId id, CellCoord cell, bool attackMove = false) =>
        w.Cmd(0, CommandKind.QueueOrder, id, default, cell, (int)ActorKind.Infantry | (attackMove ? 0x100 : 0));

    private static ActorRef Ref(EntityId id) => new(ActorKind.Infantry, id);

    private static CellCoord Where(S18World w, EntityId id) => w.Sim.Infantry.Position[id.Index].ToCell();

    [Fact]
    public void ShiftOrders_RunInOrder_ThenTheUnitGuardsTheLastOne()
    {
        var w = new S18World();
        EntityId s = w.Inf(0, "auth_rifleman", 20, 20);
        w.Run(1, Queue(w, s, A), Queue(w, s, B), Queue(w, s, C));
        Assert.Equal(2, w.Sim.WaypointsOf(Ref(s)).Count);   // the first one started at once (the unit was idle)
        int atA = -1, atB = -1, atC = -1;
        for (int t = 0; t < 900; t++)
        {
            w.Run(1);
            CellCoord p = Where(w, s);
            if (atA < 0 && p == A) atA = t;
            if (atB < 0 && p == B) atB = t;
            if (atC < 0 && p == C) atC = t;
        }
        Assert.True(atA >= 0 && atB > atA && atC > atB, $"visited A {atA}, B {atB}, C {atC}");
        Assert.Equal(C, Where(w, s));
        Assert.Empty(w.Sim.WaypointsOf(Ref(s)));
        Assert.Equal(MissionKind.Guard, w.Sim.Infantry.Mission[s.Index]);
    }

    [Fact]
    public void AShiftOrder_AfterAPlainMove_WaitsForTheMoveToFinish()
    {
        var w = new S18World();
        EntityId s = w.Inf(0, "auth_rifleman", 20, 20);
        w.Run(1, w.Cmd(0, CommandKind.Move, s, default, A, (int)ActorKind.Infantry));
        w.Run(2, Queue(w, s, C));
        Assert.Single(w.Sim.WaypointsOf(Ref(s)));
        w.Run(900);
        Assert.Equal(C, Where(w, s));
    }

    [Fact]
    public void APlainOrder_ClearsTheQueue()
    {
        var w = new S18World();
        EntityId s = w.Inf(0, "auth_rifleman", 20, 20);
        w.Run(1, Queue(w, s, A), Queue(w, s, B), Queue(w, s, C));
        w.Run(3, w.Cmd(0, CommandKind.Move, s, default, new CellCoord(14, 20), (int)ActorKind.Infantry));
        Assert.Empty(w.Sim.WaypointsOf(Ref(s)));
        w.Run(600);
        Assert.Equal(new CellCoord(14, 20), Where(w, s));
    }

    [Theory]
    [InlineData(CommandKind.Stop)]
    [InlineData(CommandKind.Guard)]
    [InlineData(CommandKind.AttackMove)]
    public void OtherPlainOrders_AlsoClearTheQueue(CommandKind kind)
    {
        var w = new S18World();
        EntityId s = w.Inf(0, "auth_rifleman", 20, 20);
        w.Run(1, Queue(w, s, A), Queue(w, s, B), Queue(w, s, C));
        w.Run(2, w.Cmd(0, kind, s, default, new CellCoord(18, 20), (int)ActorKind.Infantry));
        Assert.Empty(w.Sim.WaypointsOf(Ref(s)));
    }

    [Fact]
    public void TheQueueHoldsEightWaypoints_AndIgnoresTheNinth()
    {
        var w = new S18World();
        EntityId s = w.Inf(0, "auth_rifleman", 20, 20);
        w.Run(1, w.Cmd(0, CommandKind.Move, s, default, A, (int)ActorKind.Infantry));
        w.Run(2);
        for (int i = 0; i < 10; i++) w.Run(1, Queue(w, s, new CellCoord(25 + i, 21)));
        Assert.Equal(8, w.Sim.WaypointsOf(Ref(s)).Count);
        Assert.Equal(new CellCoord(25, 21), w.Sim.WaypointsOf(Ref(s))[0].Cell);
    }

    [Fact]
    public void AnOffMapCell_AndAnotherPlayersUnit_AreRefused()
    {
        var w = new S18World();
        EntityId mine = w.Inf(0, "auth_rifleman", 20, 20);
        EntityId theirs = w.Inf(1, "auth_rifleman", 40, 40);
        w.Run(1, Queue(w, mine, new CellCoord(-4, 9000)), Queue(w, theirs, A));
        w.Run(3);
        Assert.Empty(w.Sim.WaypointsOf(Ref(mine)));
        Assert.Empty(w.Sim.WaypointsOf(Ref(theirs)));
        Assert.Equal(new CellCoord(40, 40), Where(w, theirs));
    }

    [Fact]
    public void AnAttackMoveWaypoint_FiresOnTheWay()
    {
        var w = new S18World();
        EntityId s = w.Inf(0, "auth_rifleman", 20, 20);
        EntityId truck = w.Veh(1, "auth_decon_truck", 26, 21);   // sits beside the route
        w.Run(1, Queue(w, s, A), Queue(w, s, new CellCoord(30, 21), attackMove: true));
        w.Run(700);
        Assert.True(w.VehHp(truck) < w.Rules.Vehicles[w.V("auth_decon_truck")].MaxHealth);
    }

    [Fact]
    public void TheQueueIsSavedHashedAndRestored()
    {
        var w = new S18World();
        EntityId s = w.Inf(0, "auth_rifleman", 20, 20);
        ulong plain = w.Sim.ComputeStateHash();
        w.Run(1, Queue(w, s, A), Queue(w, s, B), Queue(w, s, C, attackMove: true));
        w.Run(10);
        Assert.NotEqual(plain, w.Sim.ComputeStateHash());
        using var ms = new MemoryStream();
        w.Sim.Save(ms);
        ms.Position = 0;
        Simulation copy = Simulation.Load(ms, w.Sim.Setup);
        Assert.Equal(w.Sim.WaypointsOf(Ref(s)), copy.WaypointsOf(Ref(s)));
        Assert.Equal(w.Sim.ComputeStateHash(), copy.ComputeStateHash());
        for (int i = 0; i < 700; i++) { w.Sim.Step(Array.Empty<Command>()); copy.Step(Array.Empty<Command>()); }
        Assert.Equal(w.Sim.ComputeStateHash(), copy.ComputeStateHash());
        Assert.Equal(C, copy.Infantry.Position[s.Index].ToCell());
    }

    [Fact]
    public void ADeadUnitsQueue_DoesNotPassToTheNextUnitInTheSlot()
    {
        var w = new S18World();
        EntityId s = w.Inf(0, "auth_rifleman", 20, 20);
        w.Run(1, w.Cmd(0, CommandKind.Move, s, default, A, (int)ActorKind.Infantry));
        w.Run(2, Queue(w, s, B), Queue(w, s, C));
        w.Run(1, w.Cmd(0, CommandKind.Debug, s, default, default, (int)DebugOp.Kill, (int)ActorKind.Infantry));
        w.Run(3);
        w.Run(1, w.Cmd(0, CommandKind.Debug, default, default, new CellCoord(21, 20), (int)DebugOp.SpawnInfantry, w.I("auth_rifleman")));
        w.Run(2);
        var fresh = new ActorRef(ActorKind.Infantry, w.Sim.Infantry.Slots.IdOf(s.Index));
        Assert.Empty(w.Sim.WaypointsOf(fresh));
    }

    [Fact]
    public void AVehicleQueuesToo()
    {
        var w = new S18World();
        EntityId v = w.Veh(0, "auth_decon_truck", 20, 20);
        w.Run(1, w.Cmd(0, CommandKind.QueueOrder, v, default, A, (int)ActorKind.Vehicle),
            w.Cmd(0, CommandKind.QueueOrder, v, default, B, (int)ActorKind.Vehicle));
        w.Run(900);
        Assert.Equal(B, w.Sim.Units.Position[v.Index].ToCell());
    }
}
