// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Ui;
using Xunit;

namespace Rts.Game.Tests.Ui2;

public sealed class HudStackTests
{
    [Fact]
    public void Stacks_below_the_lowest_panel()
    {
        var panels = new[] { new Rectangle(8, 8, 100, 40), new Rectangle(8, 54, 100, 60), Rectangle.Empty };
        Assert.Equal(114 + 6, HudStack.NextTop(8, 6, panels));
    }

    [Fact]
    public void No_panels_gives_top() => Assert.Equal(8, HudStack.NextTop(8, 6, new[] { Rectangle.Empty }));
}
