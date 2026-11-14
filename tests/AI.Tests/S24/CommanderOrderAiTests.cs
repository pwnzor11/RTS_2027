// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Sim.Core;
using Xunit.Abstractions;

namespace AI.Tests.S24;

/// <summary>S24 (M5-12, D-241): the tiered AI carries out commander orders. Scripted three-player games on a fixed seed; assertions are margins.</summary>
public class CommanderOrderAiTests(ITestOutputHelper log)
{
    private static bool Same(List<Command> a, List<Command> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (!a[i].Equals(b[i])) return false;
        return true;
    }

    [Fact]
    public void WithNoOrdersTheCommandsAreIdentical()
    {
        var plain = new OrderRig();
        plain.Run(3500);
        var empty = new OrderRig();
        empty.Driver.SetOrderSource(0, new NoOrders());
        empty.Run(3500);
        Assert.True(plain.Collected.Count > 50);
        Assert.True(Same(plain.Collected, empty.Collected), "an order source with nothing to say changes no command");
        Assert.Equal(plain.Sim.ComputeStateHash(), empty.Sim.ComputeStateHash());
    }

    private sealed class NoOrders : IOrderSource
    {
        public void TakeOrderCommands(int execTick, byte player, ref ushort sequence, List<Command> outCommands) { }
    }

    private sealed class OneOrder(CellCoord cell) : IOrderSource
    {
        private bool _sent;
        public void TakeOrderCommands(int execTick, byte player, ref ushort sequence, List<Command> outCommands)
        {
            if (_sent || execTick < 10) return;
            _sent = true;
            outCommands.Add(CommanderOrderCodec.ToCommand(execTick, player, sequence, CommanderOrderKind.HoldCell, 2, cell, 0, 0));
            unchecked { sequence++; }
        }
    }

    [Fact]
    public void AnOrderSourceFeedsTheOrderIntoTheCommandStream()
    {
        var rig = new OrderRig();
        CellCoord cell = rig.NearYard();
        rig.Driver.SetOrderSource(0, new OneOrder(cell));
        rig.Run(60);
        CommanderOrder o = rig.Sim.CommanderOrdersOf(0)[2];
        Assert.Equal(CommanderOrderKind.HoldCell, o.Kind);
        Assert.Equal(cell, o.Cell);
        Assert.Contains(rig.Collected, c => c.Kind == CommandKind.CommanderOrder && c.Player == 0);
    }

    [Fact]
    public void AttackAtKeepsTheArmyHomeUntilDueThenLaunchesOneWaveAtTheCell()
    {
        var plain = new OrderRig();
        int plainFirst = -1;
        plain.Run(9000, r => { if (plainFirst < 0 && r.Ai.Waves > 0) plainFirst = r.Sim.Tick; });
        Assert.True(plainFirst > 0, "the plain AI launches a wave in the window");

        int due = plainFirst + 1500;
        var rig = new OrderRig();
        CellCoord cell = rig.NearYard(10);
        rig.Run(5);
        rig.Order(0, CommanderOrderKind.AttackAt, cell, amount: 1, dueTick: due);
        int first = -1, wavesAtDue = 0, wavesSoonAfter = 0;
        rig.Run(9000 - 5, r =>
        {
            if (r.Sim.Tick == due - 1) wavesAtDue = r.Ai.Waves;
            if (r.Sim.Tick == due + 250) wavesSoonAfter = r.Ai.Waves;
            if (first < 0 && r.Ai.Waves > 0) first = r.Sim.Tick;
        });
        log.WriteLine($"plain first wave {plainFirst}, due {due}, ordered first wave {first}, waves at due {wavesAtDue}, total {rig.Ai.Waves}");
        Assert.Equal(0, wavesAtDue);
        Assert.True(first >= due - 2 && first <= due + 200, $"the wave left at {first}, due {due}");
        Assert.Equal(1, wavesSoonAfter);   // one wave for the order, not one per decision
    }

    [Fact]
    public void AttackAtWaitsForTheArmyValue()
    {
        var rig = new OrderRig();
        CellCoord cell = rig.NearYard(10);
        rig.Run(5);
        rig.Order(0, CommanderOrderKind.AttackAt, cell, amount: 1_000_000, dueTick: 0);
        rig.Run(7000);
        Assert.Equal(0, rig.Ai.Waves);
    }

    [Fact]
    public void HoldCellStopsWavesAndGathersTheArmyAtTheCell()
    {
        var plain = new OrderRig();
        plain.Run(8000);
        Assert.True(plain.Ai.Waves > 0, "the plain AI attacks in the window");

        var rig = new OrderRig();
        CellCoord cell = rig.NearYard(6);
        rig.Run(5);
        rig.Order(1, CommanderOrderKind.HoldCell, cell);
        rig.Run(8000 - 5);
        Assert.Equal(CommanderOrderKind.HoldCell, rig.Sim.CommanderOrdersOf(0)[1].Kind);
        Assert.Equal(0, rig.Ai.Waves);
        int near = 0, far = 0;
        for (int i = 1; i < rig.Sim.Infantry.Capacity; i++)
            if (rig.Sim.Infantry.Slots.IsIndexAlive(i) && rig.Sim.Infantry.Player[i] == 0)
            { var c = new CellCoord(rig.Sim.Infantry.Position[i].X / 256, rig.Sim.Infantry.Position[i].Y / 256); if (Math.Max(Math.Abs(c.X - cell.X), Math.Abs(c.Y - cell.Y)) <= 12) near++; else far++; }
        for (int i = 1; i < rig.Sim.Units.Capacity; i++)
            if (rig.Sim.Units.Slots.IsIndexAlive(i) && rig.Sim.Units.Player[i] == 0)
            { var c = new CellCoord(rig.Sim.Units.Position[i].X / 256, rig.Sim.Units.Position[i].Y / 256); if (Math.Max(Math.Abs(c.X - cell.X), Math.Abs(c.Y - cell.Y)) <= 12) near++; else far++; }
        log.WriteLine($"near {near} far {far}");
        Assert.True(near > far, $"{near} units near the held cell, {far} elsewhere");

        // clearing the slot lets the AI attack again
        rig.Order(1, CommanderOrderKind.None, Directive.NoCell);
        rig.Run(6000);
        Assert.True(rig.Ai.Waves > 0, "waves resume once the hold is cleared");
    }

    [Fact]
    public void RaidSendsASeparateGroupThatRetreatsAndRefillsOnce()
    {
        var rig = new OrderRig();
        CellCoord cell = rig.NearYard(14);
        rig.Run(2500);   // an army exists
        rig.Order(3, CommanderOrderKind.Raid, cell, amount: 2);
        rig.Run(600);
        Assert.Equal(1, rig.Ai.OrderRaidGroups);
        Assert.True(rig.Ai.OrderRaidStrikes > 0, "the group was given orders");
        rig.KillArmy();   // the group is wiped out: it must call a retreat
        rig.Run(200);
        Assert.Equal(1, rig.Ai.OrderRaidRetreats);
        int groups = 0, retreats = 0;
        rig.Run(5000, r => { groups = r.Ai.OrderRaidGroups; retreats = r.Ai.OrderRaidRetreats; });
        log.WriteLine($"groups {groups} retreats {retreats}");
        Assert.True(groups <= 2, "at most one refill group");
        // a second wipe ends the order for good
        if (groups == 2)
        {
            rig.KillArmy();
            rig.Run(200);
            Assert.Equal(2, rig.Ai.OrderRaidRetreats);
            rig.Run(4000);
            Assert.Equal(2, rig.Ai.OrderRaidGroups);   // the order is done: no third group
        }
    }

    [Fact]
    public void RaidLeavesTheMainArmysWavesToTheUsualRules()
    {
        var plain = new OrderRig();
        plain.Run(6000);
        var rig = new OrderRig();
        CellCoord cell = rig.NearYard(14);
        rig.Run(5);
        rig.Order(0, CommanderOrderKind.Raid, cell, amount: 2);
        rig.Run(6000 - 5);
        log.WriteLine($"waves plain {plain.Ai.Waves} raid {rig.Ai.Waves} groups {rig.Ai.OrderRaidGroups}");
        Assert.True(rig.Ai.OrderRaidGroups >= 1);
        Assert.True(plain.Ai.Waves > 0 ? rig.Ai.Waves > 0 : true, "the raid does not stop the waves");
    }

    [Fact]
    public void SaveAndLoadWithAnActiveRaidGivesTheSameGame()
    {
        OrderRig Start()
        {
            var r = new OrderRig();
            CellCoord cell = r.NearYard(14);
            r.Run(2500);
            r.Order(0, CommanderOrderKind.Raid, cell, amount: 3);
            r.Run(700);
            return r;
        }
        OrderRig straight = Start();
        Assert.True(straight.Ai.OrderRaidGroups >= 1, "the raid is active at the save point");
        OrderRig resumed = Start();
        byte[] blob = resumed.CaptureAi()!;
        resumed.ResumeWithBlob(blob);
        straight.Run(2500);
        resumed.Run(2500);
        Assert.Equal(straight.Sim.ComputeStateHash(), resumed.Sim.ComputeStateHash());
        Assert.Equal(straight.Ai.OrderRaidGroups, resumed.Ai.OrderRaidGroups);
    }
}
