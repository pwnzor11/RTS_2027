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
        Assert.True(orders.AttackMoveArmed);
        Assert.True(orders.AnyArmed);
        Assert.True(orders.CancelArmed());
        Assert.False(orders.AnyArmed);
        Assert.False(orders.CancelArmed());
    }
}
