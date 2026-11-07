// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Sidebar;
using Xunit;

namespace Rts.Game.Tests.Ui2;

public sealed class PowerReadoutTests
{
    [Fact]
    public void Text_is_output_slash_use() => Assert.Equal("120 / 150", SidebarMetrics.PowerText(120, 150));

    [Fact]
    public void Negative_counts_as_zero() => Assert.Equal("0 / 5", SidebarMetrics.PowerText(-3, 5));

    [Fact]
    public void Bar_is_always_lit_when_power_is_fine()
    {
        for (int t = 0; t < 100; t++) Assert.True(SidebarMetrics.PowerBarLit(false, t));
    }

    [Fact]
    public void Bar_blinks_while_low()
    {
        Assert.True(SidebarMetrics.PowerBarLit(true, 0));
        Assert.False(SidebarMetrics.PowerBarLit(true, SidebarMetrics.PowerBlinkHalfPeriodTicks));
        Assert.True(SidebarMetrics.PowerBarLit(true, 2 * SidebarMetrics.PowerBlinkHalfPeriodTicks));
    }

    [Fact]
    public void Layout_has_a_power_row_above_the_bar()
    {
        SidebarLayout l = SidebarLayout.Compute(1600, 900, 1f);
        Assert.True(l.PowerRow.Height > 0);
        Assert.True(l.PowerRow.Bottom <= l.PowerBar.Top);
    }
}
