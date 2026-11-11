// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;
using Rts.Game.Render;

namespace Rts.Game.Tests.S18;

/// <summary>S18 GAMEUI-02 (zoom about the cursor) and GAMEUI-04 (a slightly moving click is a click).</summary>
public sealed class UiCameraAndDragTests
{
    [Theory]
    [InlineData(1, 100, 100)]
    [InlineData(1, 640, 360)]
    [InlineData(-1, 1000, 50)]
    [InlineData(-1, 3, 700)]
    public void Zoom_keeps_the_world_point_under_the_cursor_fixed(int steps, int mx, int my)
    {
        var camera = new Camera();
        camera.PanScreen(new Vector2(1234, 876));
        var cursor = new Point(mx, my);
        Vector2 before = camera.WorldFromScreen(cursor);
        camera.ZoomBy(steps, cursor);
        Assert.NotEqual(1f, camera.Zoom);
        Vector2 after = camera.WorldFromScreen(cursor);
        Assert.Equal(before.X, after.X, 3);
        Assert.Equal(before.Y, after.Y, 3);
    }

    [Fact]
    public void Zooming_past_the_last_level_changes_nothing()
    {
        var camera = new Camera();
        camera.PanScreen(new Vector2(500, 500));
        camera.ZoomBy(1, new Point(200, 200));
        Vector2 at = camera.WorldFromScreen(new Point(0, 0));
        camera.ZoomBy(1, new Point(200, 200));   // already at 2
        Assert.Equal(2f, camera.Zoom);
        Assert.Equal(at, camera.WorldFromScreen(new Point(0, 0)));
    }

    [Fact]
    public void A_press_that_moves_a_few_pixels_is_still_a_click_and_a_real_drag_is_not()
    {
        var start = new Point(300, 300);
        Assert.False(ClickDrag.IsDrag(start, new Point(300, 300), 1f));
        Assert.False(ClickDrag.IsDrag(start, new Point(305, 304), 1f));   // 5.8 px: used to start a box
        Assert.False(ClickDrag.IsDrag(start, new Point(306, 305), 1f));   // 7.8 px
        Assert.True(ClickDrag.IsDrag(start, new Point(309, 300), 1f));
    }

    [Fact]
    public void The_threshold_grows_with_the_ui_scale()
    {
        var start = new Point(300, 300);
        Assert.True(ClickDrag.IsDrag(start, new Point(310, 300), 1f));
        Assert.False(ClickDrag.IsDrag(start, new Point(310, 300), 2f));
        Assert.True(ClickDrag.IsDrag(start, new Point(320, 300), 2f));
        Assert.False(ClickDrag.IsDrag(start, new Point(305, 300), 0.5f));   // never below the base threshold
    }
}
