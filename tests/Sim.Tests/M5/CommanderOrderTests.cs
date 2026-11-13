// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Tests.Determinism;
using Xunit;

namespace Rts.Sim.Tests.M5;

/// <summary>S24 (M5-12, D-241): commander orders in the sim ("commander_orders" part, command, player view) and the directive Strong flag.</summary>
public class CommanderOrderTests
{
    private static readonly CellCoord Explored = new(3, 3);
    private static readonly CellCoord Shrouded = new(62, 20);

    private static Simulation Warm()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        for (int i = 0; i < 3; i++) sim.Step(Array.Empty<Command>());
        return sim;
    }

    private static ushort _seq;

    private static void Order(Simulation sim, byte player, CommanderOrderKind kind, int slot, CellCoord cell, int amount, int due = 0) =>
        sim.Step(new[] { CommanderOrderCodec.ToCommand(sim.Tick, player, ++_seq, kind, slot, cell, amount, due) });

    [Fact]
    public void NoOrders_ViewIsAllEmpty_AndPartIsOneByte()
    {
        var sim = Warm();
        var orders = sim.ViewFor(0).CommanderOrders;
        Assert.Equal(CommanderOrderCodec.Slots, orders.Count);
        Assert.All(orders, o => Assert.Equal(CommanderOrder.Empty, o));
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) sim.CommanderOrders.Write(w);
        Assert.Single(ms.ToArray());
    }

    [Fact]
    public void Apply_Replace_Clear_PerSlot()
    {
        var sim = Warm();
        int tick = sim.Tick;
        Order(sim, 0, CommanderOrderKind.AttackAt, 1, Explored, 900, tick + 50);
        Assert.Equal(new CommanderOrder(CommanderOrderKind.AttackAt, Explored, 900, tick + 50, tick), sim.ViewFor(0).CommanderOrders[1]);
        Assert.False(sim.ViewFor(0).CommanderOrders[0].IsSet);
        Assert.False(sim.ViewFor(1).CommanderOrders[1].IsSet);
        Order(sim, 0, CommanderOrderKind.Raid, 1, Explored, 4);
        Assert.Equal(CommanderOrderKind.Raid, sim.ViewFor(0).CommanderOrders[1].Kind);
        Assert.Equal(0, sim.ViewFor(0).CommanderOrders[1].DueTick);
        Order(sim, 0, CommanderOrderKind.Reserve, 3, Directive.NoCell, 500);
        Assert.Equal(new CommanderOrder(CommanderOrderKind.Reserve, Directive.NoCell, 500, 0, sim.Tick - 1), sim.ViewFor(0).CommanderOrders[3]);
        Order(sim, 0, CommanderOrderKind.None, 1, Directive.NoCell, 0);
        Assert.Equal(CommanderOrder.Empty, sim.ViewFor(0).CommanderOrders[1]);
        Assert.True(sim.ViewFor(0).CommanderOrders[3].IsSet);
    }

    [Fact]
    public void Invalid_KindSlotCell_AreDropped()
    {
        var sim = Warm();
        ulong before = sim.ComputeStateHash();
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.CommanderOrder, default, default, Explored, 7 | 0 << 4) });          // kind 7
        sim.Step(new[] { new Command(sim.Tick, 0, 2, CommandKind.CommanderOrder, default, default, Explored, (int)CommanderOrderKind.HoldCell | 4 << 4) });   // slot 4
        Order(sim, 0, CommanderOrderKind.HoldCell, 0, Shrouded, 0);
        Order(sim, 0, CommanderOrderKind.ExpandTo, 0, new CellCoord(500, 3), 0);
        Assert.All(sim.ViewFor(0).CommanderOrders, o => Assert.Equal(CommanderOrder.Empty, o));
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) sim.CommanderOrders.Write(w);
        Assert.Single(ms.ToArray());
        Assert.NotEqual(0UL, before);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(3, 3)]
    [InlineData(9, 5)]
    public void TechTo_AmountClamps(int amount, int want)
    {
        var sim = Warm();
        Order(sim, 0, CommanderOrderKind.TechTo, 2, Shrouded, amount);   // a cell is ignored for TechTo
        CommanderOrder o = sim.ViewFor(0).CommanderOrders[2];
        Assert.Equal(want, o.Amount);
        Assert.Equal(Directive.NoCell, o.Cell);
    }

    [Fact]
    public void StrongBit_RoundTrips_AndMixClampsTo0to6()
    {
        var sim = Warm();
        int arg = DirectiveCodec.PackStrong(DirectiveCodec.Pack(2, 2, 255, DirectiveCodec.MixAntiInfantry, false), true);
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Directive, default, default, Directive.NoCell, arg) });
        Directive d = sim.ViewFor(0).Directive;
        Assert.True(d.Strong);
        Assert.Equal(DirectiveCodec.MixAntiInfantry, d.Mix);
        Assert.False(sim.ViewFor(1).Directive.Strong);
        sim.Step(new[] { new Command(sim.Tick, 0, 2, CommandKind.Directive, default, default, Directive.NoCell, DirectiveCodec.Pack(2, 2, 255, 7, false)) });
        Assert.Equal((DirectiveCodec.MixAuto, false), (sim.ViewFor(0).Directive.Mix, sim.ViewFor(0).Directive.Strong));
        sim.Step(new[] { new Command(sim.Tick, 1, 3, CommandKind.Directive, default, default, Directive.NoCell,
            DirectiveCodec.PackStrong(DirectiveCodec.Pack(1, 1, 255, DirectiveCodec.MixAntiArmour, false), true)) });
        Assert.Equal((DirectiveCodec.MixAntiArmour, true), (sim.ViewFor(1).Directive.Mix, sim.ViewFor(1).Directive.Strong));
    }

    [Fact]
    public void OrdersAndStrong_SurviveSaveAndLoad()
    {
        var sim = Warm();
        Order(sim, 1, CommanderOrderKind.TechTo, 0, Directive.NoCell, 4);
        Order(sim, 0, CommanderOrderKind.AttackAt, 2, Explored, 700, 400);
        sim.Step(new[] { new Command(sim.Tick, 0, 9, CommandKind.Directive, default, default, Directive.NoCell,
            DirectiveCodec.PackStrong(DirectiveCodec.Pack(2, 2, 255, DirectiveCodec.MixAntiArmour, false), true)) });
        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        var loaded = Simulation.Load(ms, sim.Setup);
        for (byte p = 0; p < 2; p++)
        {
            Assert.Equal(sim.ViewFor(p).CommanderOrders, loaded.ViewFor(p).CommanderOrders);
            Assert.Equal(sim.ViewFor(p).Directive, loaded.ViewFor(p).Directive);
        }
        Assert.True(loaded.ViewFor(0).Directive.Strong);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        for (int i = 0; i < 30; i++) { sim.Step(Array.Empty<Command>()); loaded.Step(Array.Empty<Command>()); }
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }

    [Fact]
    public void SameCommands_SameHash_AndOrdersMoveOnlyTheirPart()
    {
        var a = Warm();
        var b = Warm();
        var c = Warm();
        foreach (var s in new[] { a, b })
        {
            Order(s, 0, CommanderOrderKind.HoldCell, 0, Explored, 0);
            for (int i = 0; i < 20; i++) s.Step(Array.Empty<Command>());
        }
        c.Step(Array.Empty<Command>());
        for (int i = 0; i < 20; i++) c.Step(Array.Empty<Command>());
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        var diff = a.ComputeSubHashes().Zip(c.ComputeSubHashes()).Where(p => p.First.Hash != p.Second.Hash).Select(p => p.First.Part).ToArray();
        Assert.Equal(new[] { "commander_orders" }, diff);
    }
}
