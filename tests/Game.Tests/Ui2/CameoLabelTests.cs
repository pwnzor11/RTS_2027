// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Sidebar;
using Xunit;

namespace Rts.Game.Tests.Ui2;

public sealed class CameoLabelTests
{
    [Fact]
    public void Fits_unchanged_when_it_already_fits() =>
        Assert.Equal(2f, SidebarMetrics.FitScale(s => (int)(10 * s), 40, 2f));

    [Fact]
    public void Shrinks_until_it_fits()
    {
        float s = SidebarMetrics.FitScale(x => (int)(30 * x), 40, 2f);
        Assert.True(30 * s <= 40);
        Assert.True(s < 2f && s > 0.8f);
    }

    [Fact]
    public void Never_below_forty_percent() =>
        Assert.Equal(0.8f, SidebarMetrics.FitScale(x => 1000, 10, 2f), 3);
}
