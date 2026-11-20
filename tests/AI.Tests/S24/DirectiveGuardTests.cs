// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Rts.Sim.Core;
using Xunit;

namespace AI.Tests.S24;

/// <summary>S36 (M5-14, D-278): the directive guard drops exactly the parts that hold credits or the army back, and keeps the order slots.</summary>
public sealed class DirectiveGuardTests
{
    [Fact]
    public void HeldBackKindsAreHoldReserveAndTech()
    {
        Assert.True(DirectiveGuard.IsHeldBack(CommanderOrderKind.HoldCell));
        Assert.True(DirectiveGuard.IsHeldBack(CommanderOrderKind.Reserve));
        Assert.True(DirectiveGuard.IsHeldBack(CommanderOrderKind.TechTo));
        Assert.False(DirectiveGuard.IsHeldBack(CommanderOrderKind.AttackAt));
        Assert.False(DirectiveGuard.IsHeldBack(CommanderOrderKind.Raid));
        Assert.False(DirectiveGuard.IsHeldBack(CommanderOrderKind.ExpandTo));
        Assert.False(DirectiveGuard.HoldBlocksWaves);
    }

    [Fact]
    public void WhileBehindKeepsPostureMixAndExpandAndDropsTheReserveAndGates()
    {
        var d = new Directive(true, DirectiveCodec.PostureAggressive, DirectiveCodec.FocusTech, DirectiveCodec.AnyTarget, DirectiveCodec.MixVehicles,
            true, new CellCoord(9, 9), 600, true);
        DirectiveBias full = DirectiveBias.For(d);
        (DirectiveBias b, _) = DirectiveGuard.WhileBehind(full, Array.Empty<CommanderOrder>());
        DirectiveBias plain = DirectiveBias.Plain;
        Assert.True(b.Set);
        Assert.Equal(full.WavePermille, b.WavePermille);
        Assert.Equal(full.AttackCell, b.AttackCell);
        Assert.Equal(full.Mix, b.Mix);
        Assert.Equal(full.MixPermille, b.MixPermille);
        Assert.True(b.Expand);
        Assert.Equal(plain.UnitReserve, b.UnitReserve);
        Assert.Equal(plain.GatePermille, b.GatePermille);
        Assert.Equal(plain.Tier3ArmyPermille, b.Tier3ArmyPermille);
        Assert.Equal(plain.ForceTech, b.ForceTech);
        Assert.Equal(plain.UpgradeMinArmy, b.UpgradeMinArmy);
    }

    [Fact]
    public void WhileBehindEmptiesHeldBackSlotsInPlace()
    {
        var orders = new[]
        {
            new CommanderOrder(CommanderOrderKind.AttackAt, new CellCoord(5, 5), 1200, 0, 600),
            new CommanderOrder(CommanderOrderKind.Reserve, Directive.NoCell, 800, 0, 600),
            new CommanderOrder(CommanderOrderKind.HoldCell, new CellCoord(7, 7), 0, 0, 600),
            new CommanderOrder(CommanderOrderKind.TechTo, Directive.NoCell, 4, 0, 600),
        };
        (_, IReadOnlyList<CommanderOrder> kept) = DirectiveGuard.WhileBehind(DirectiveBias.Plain, orders);
        Assert.Equal(4, kept.Count);
        Assert.Equal(CommanderOrderKind.AttackAt, kept[0].Kind);
        Assert.Equal(CommanderOrder.Empty, kept[1]);
        Assert.Equal(CommanderOrder.Empty, kept[2]);
        Assert.Equal(CommanderOrder.Empty, kept[3]);
    }
}
