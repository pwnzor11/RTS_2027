// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Xunit;

namespace Rts.Game.Tests.Ui2;

public sealed class ArmedOrderTests
{
    [Fact]
    public void A_arms_and_cancel_clears()
    {
        var orders = new OrderController(null!, new Selection());
        orders.KeyPressed(Rts.Game.Controls.GameAction.AttackMove);
        Assert.False(orders.AttackMoveArmed);   // nothing selected that can attack: does not arm (M11-10)
        Assert.False(orders.AnyArmed);
        Assert.False(orders.CancelArmed());
    }

    [Fact]
    public void Arming_rule_needs_one_unit_that_can_attack()
    {
        Assert.False(OrderController.CanArm(Array.Empty<bool>()));
        Assert.False(OrderController.CanArm(new[] { false, false }));
        Assert.True(OrderController.CanArm(new[] { false, true }));
    }
}
