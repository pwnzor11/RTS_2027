// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Card;
using Rts.Sim.Core;

namespace Rts.Game.Tests.S19;

/// <summary>D-205 (S18 GAMEUI-06): the card's Sell needs a second click within 2 seconds.</summary>
public sealed class SellConfirmTests
{
    private static readonly EntityId A = new(4, 1);
    private static readonly EntityId B = new(5, 1);

    [Fact]
    public void The_first_click_only_arms()
    {
        var sell = new SellConfirm();
        Assert.False(sell.IsArmed(A, 0));
        Assert.False(sell.Click(A, 1000));
        Assert.True(sell.IsArmed(A, 1001));
    }

    [Fact]
    public void A_second_click_inside_two_seconds_sells_and_disarms()
    {
        var sell = new SellConfirm();
        sell.Click(A, 1000);
        Assert.True(sell.Click(A, 1000 + SellConfirm.WindowMs - 1));
        Assert.False(sell.IsArmed(A, 1000 + SellConfirm.WindowMs - 1));
        Assert.False(sell.Click(A, 4000));   // a third click starts over
    }

    [Fact]
    public void The_arm_times_out()
    {
        var sell = new SellConfirm();
        sell.Click(A, 1000);
        Assert.True(sell.IsArmed(A, 1000 + SellConfirm.WindowMs - 1));
        Assert.False(sell.IsArmed(A, 1000 + SellConfirm.WindowMs));
        Assert.False(sell.Click(A, 1000 + SellConfirm.WindowMs));      // too late: arms again, does not sell
        Assert.True(sell.IsArmed(A, 1000 + SellConfirm.WindowMs + 1));
    }

    [Fact]
    public void Another_building_is_never_sold_by_the_first_ones_arm()
    {
        var sell = new SellConfirm();
        sell.Click(A, 1000);
        Assert.False(sell.IsArmed(B, 1100));
        Assert.False(sell.Click(B, 1100));   // arms B instead
        Assert.False(sell.IsArmed(A, 1200));
        Assert.True(sell.IsArmed(B, 1200));
    }

    [Fact]
    public void No_building_is_never_armed()
    {
        var sell = new SellConfirm();
        Assert.False(sell.Click(EntityId.None, 0));
        Assert.False(sell.IsArmed(EntityId.None, 1));
        sell.Click(A, 0);
        sell.Reset();
        Assert.False(sell.IsArmed(A, 1));
    }
}
