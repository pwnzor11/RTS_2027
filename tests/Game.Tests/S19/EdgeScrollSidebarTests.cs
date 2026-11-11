// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;

namespace Rts.Game.Tests.S19;

/// <summary>D-205 (S18 GAMEUI-09): over the sidebar only the outer 3 px of the screen edge scroll.</summary>
public sealed class EdgeScrollSidebarTests
{
    private static readonly Rectangle Window = new(0, 0, 1600, 900);
    private static readonly Rectangle Sidebar = new(1288, 0, 312, 900);

    private static Vector2 Dir(int x, int y) => CameraScroll.EdgeDirection(new Point(x, y), Window, true, true, false, sidebar: Sidebar);

    [Theory]
    [InlineData(1599, 450, 1, 0)]     // the outer 3 px of the right screen edge still scroll
    [InlineData(1597, 450, 1, 0)]
    [InlineData(1500, 0, 0, -1)]      // the top edge over the sidebar
    [InlineData(1500, 2, 0, -1)]
    [InlineData(1500, 899, 0, 1)]
    public void Over_the_sidebar_the_outer_three_pixels_scroll(int x, int y, float dx, float dy) => Assert.Equal(new Vector2(dx, dy), Dir(x, y));

    [Theory]
    [InlineData(1596, 450)]           // 4 px from the edge: inside the normal 12 px band but not the sidebar's 3 px
    [InlineData(1590, 450)]
    [InlineData(1500, 3)]
    [InlineData(1500, 11)]
    [InlineData(1500, 888)]
    public void Over_the_sidebar_the_rest_of_the_old_band_does_not_scroll(int x, int y) => Assert.Equal(Vector2.Zero, Dir(x, y));

    [Fact]
    public void Off_the_sidebar_the_normal_band_is_unchanged()
    {
        Assert.Equal(new Vector2(-1, 0), Dir(11, 450));
        Assert.Equal(new Vector2(0, -1), Dir(800, 11));
        Assert.Equal(Vector2.Zero, Dir(12, 450));
        Assert.Equal(new Vector2(0, -1), CameraScroll.EdgeDirection(new Point(1500, 11), Window, true, true, false));   // no sidebar given: as before
    }
}
