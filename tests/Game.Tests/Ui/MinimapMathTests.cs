// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Ui;

public sealed class MinimapMathTests
{
    private static readonly Rectangle Area = new(1000, 500, 200, 200);

    [Fact]
    public void Square_map_fills_the_area()
    {
        MinimapMath m = MinimapMath.Fit(Area, 64, 64);
        Assert.Equal(Area, m.Image);
        Assert.Equal(200f / 64f, m.Scale, 3);
    }

    [Fact]
    public void Wide_and_tall_maps_are_letterboxed_and_centred()
    {
        MinimapMath wide = MinimapMath.Fit(Area, 100, 50);
        Assert.Equal(new Rectangle(1000, 550, 200, 100), wide.Image);
        MinimapMath tall = MinimapMath.Fit(Area, 40, 80);
        Assert.Equal(new Rectangle(1050, 500, 100, 200), tall.Image);
    }

    [Fact]
    public void Empty_inputs_give_an_empty_image_and_safe_results()
    {
        MinimapMath m = MinimapMath.Fit(Rectangle.Empty, 64, 64);
        Assert.True(m.Image.IsEmpty);
        Assert.Equal(Vector2.Zero, m.CellFromPoint(new Point(5, 5)));
        Assert.False(m.Contains(new Point(0, 0)));
        Assert.True(MinimapMath.Fit(Area, 0, 0).Image.IsEmpty);
    }

    [Fact]
    public void Points_and_cells_round_trip()
    {
        MinimapMath m = MinimapMath.Fit(Area, 64, 64);
        Assert.Equal(new Vector2(0, 0), m.CellFromPoint(new Point(1000, 500)));
        Assert.Equal(new Vector2(32, 32), m.CellFromPoint(new Point(1100, 600)));
        Assert.Equal(new Vector2(64, 64), m.CellFromPoint(new Point(1200, 700)));
        Vector2 back = m.PointFromCell(new Vector2(16, 48));
        Assert.Equal(new Vector2(1050, 650), back);
        Vector2 cell = m.CellFromPoint(new Point(1050, 650));
        Assert.Equal(16f, cell.X, 3);
        Assert.Equal(48f, cell.Y, 3);
    }

    [Fact]
    public void Points_outside_are_clamped_to_the_map_edge()
    {
        MinimapMath m = MinimapMath.Fit(Area, 64, 64);
        Assert.Equal(new Vector2(0, 64), m.CellFromPoint(new Point(-50, 9999)));
        Assert.Equal(new CellCoord(63, 63), m.OrderCell(new Point(5000, 5000)));
        Assert.Equal(new CellCoord(0, 0), m.OrderCell(new Point(0, 0)));
        Assert.Equal(new CellCoord(32, 32), m.OrderCell(new Point(1100, 600)));
    }

    [Fact]
    public void Viewport_rectangle_covers_the_cells_the_camera_shows()
    {
        MinimapMath m = MinimapMath.Fit(Area, 64, 64);
        // 16 x 8 cells starting at cell (8, 4).
        Rectangle r = m.ViewportRect(new Vector2(8 * Camera.CellPx, 4 * Camera.CellPx), new Vector2(16 * Camera.CellPx, 8 * Camera.CellPx));
        Assert.Equal(new Rectangle(1025, 512, 50, 26), r);   // 3.125 px per cell; corners (12.5, 37.5) round to even
        Assert.True(m.ViewportRect(Vector2.Zero, Vector2.Zero).Width >= 1);
    }

    [Fact]
    public void Clicking_centres_the_camera_and_keeps_it_inside_the_map()
    {
        MinimapMath m = MinimapMath.Fit(Area, 64, 64);
        var view = new Vector2(20 * Camera.CellPx, 10 * Camera.CellPx);
        Vector2 mid = m.CameraTopLeftFor(new Point(1100, 600), view);   // map centre (32, 32)
        Assert.Equal(new Vector2((32 - 10) * Camera.CellPx, (32 - 5) * Camera.CellPx), mid);
        Assert.Equal(Vector2.Zero, m.CameraTopLeftFor(new Point(1000, 500), view));
        Assert.Equal(new Vector2((64 - 20) * Camera.CellPx, (64 - 10) * Camera.CellPx), m.CameraTopLeftFor(new Point(1200, 700), view));
        // A view bigger than the map is pinned to the corner.
        Assert.Equal(Vector2.Zero, m.CameraTopLeftFor(new Point(1100, 600), new Vector2(100 * Camera.CellPx, 100 * Camera.CellPx)));
    }

    [Fact]
    public void Click_then_viewport_rectangle_is_centred_on_the_click()
    {
        MinimapMath m = MinimapMath.Fit(Area, 64, 64);
        var view = new Vector2(16 * Camera.CellPx, 16 * Camera.CellPx);
        Point click = new(1090, 610);
        Rectangle r = m.ViewportRect(m.CameraTopLeftFor(click, view), view);
        Assert.InRange(r.Center.X, click.X - 1, click.X + 1);
        Assert.InRange(r.Center.Y, click.Y - 1, click.Y + 1);
    }
}
