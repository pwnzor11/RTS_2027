// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;

namespace Rts.Game.Tests.Controls;

public sealed class CameraScrollTests
{
    private static readonly Rectangle Window = new(0, 0, 1600, 900);

    private static Vector2 Dir(int x, int y, bool enabled = true, bool focused = true, bool busy = false) =>
        CameraScroll.EdgeDirection(new Point(x, y), Window, enabled, focused, busy);

    [Theory]
    [InlineData(0, 450, -1, 0)]
    [InlineData(11, 450, -1, 0)]
    [InlineData(1599, 450, 1, 0)]       // the right window edge scrolls even though the sidebar is there
    [InlineData(800, 0, 0, -1)]
    [InlineData(800, 899, 0, 1)]
    public void Edge_band_gives_a_direction(int x, int y, float dx, float dy) => Assert.Equal(new Vector2(dx, dy), Dir(x, y));

    [Theory]
    [InlineData(12, 450)]               // just outside the 12 px band
    [InlineData(800, 450)]
    [InlineData(1587, 450)]             // the sidebar's own left edge is not a window edge (here: 1288)
    [InlineData(1288, 450)]
    public void Inside_the_band_only(int x, int y) => Assert.Equal(Vector2.Zero, Dir(x, y));

    [Fact]
    public void Corners_are_diagonal_and_normalised()
    {
        Vector2 d = Dir(0, 0);
        Assert.True(d.X < 0 && d.Y < 0);
        Assert.Equal(1f, d.Length(), 3);
        Vector2 br = Dir(1599, 899);
        Assert.True(br.X > 0 && br.Y > 0);
    }

    [Fact]
    public void Off_when_disabled_unfocused_busy_or_outside_the_window()
    {
        Assert.Equal(Vector2.Zero, Dir(0, 450, enabled: false));
        Assert.Equal(Vector2.Zero, Dir(0, 450, focused: false));
        Assert.Equal(Vector2.Zero, Dir(0, 450, busy: true));
        Assert.Equal(Vector2.Zero, Dir(-5, 450));
        Assert.Equal(Vector2.Zero, Dir(1600, 450));
    }

    [Fact]
    public void Shift_doubles_the_speed_and_a_frame_delta_never_exceeds_one_speed()
    {
        Assert.Equal(900f, CameraScroll.Speed(900, false));
        Assert.Equal(1800f, CameraScroll.Speed(900, true));
        Vector2 d = CameraScroll.FrameDelta(new Vector2(1f, 1f), 900, false, 1f);   // key plus edge, same diagonal
        Assert.Equal(900f, d.Length(), 2);
        Assert.Equal(new Vector2(0f, 90f), CameraScroll.FrameDelta(new Vector2(0f, 1f), 900, false, 0.1f));
        Assert.Equal(Vector2.Zero, CameraScroll.FrameDelta(Vector2.Zero, 900, true, 1f));
    }

    [Fact]
    public void Default_speed_crosses_a_1600_px_window_in_about_a_second_and_a_half()
    {
        Assert.Equal(900, Settings.DefaultScrollSpeed);
        float seconds = 1280f / Settings.DefaultScrollSpeed;   // the map view of a 1600 px window
        Assert.InRange(seconds, 1.2f, 1.6f);
    }

    // ---- M11-10b: windowed mode -------------------------------------------------------------------

    [Fact]
    public void The_band_is_measured_in_client_pixels_not_back_buffer_pixels()
    {
        Rectangle client = CameraScroll.ClientRect(1280, 720, 1600, 900);   // window resized smaller than the back buffer
        Assert.Equal(new Rectangle(0, 0, 1280, 720), client);
        Assert.Equal(new Vector2(1f, 0f), CameraScroll.EdgeDirection(new Point(1279, 300), client, true, true, false));   // right edge of the window
        Assert.Equal(Vector2.Zero, CameraScroll.EdgeDirection(new Point(1590, 300), client, true, true, false));        // outside the window: no scroll
        Assert.Equal(Vector2.Zero, CameraScroll.EdgeDirection(new Point(1279, 300), Window, true, true, false));        // the old back-buffer rectangle would not scroll here
    }

    [Theory]
    [InlineData(400, -20)]     // over the title bar
    [InlineData(-3, 300)]      // over another window on the left
    [InlineData(1280, 300)]    // just past the right edge
    [InlineData(400, 720)]     // just below the bottom edge
    public void A_cursor_outside_the_client_area_never_scrolls(int x, int y) =>
        Assert.Equal(Vector2.Zero, CameraScroll.EdgeDirection(new Point(x, y), CameraScroll.ClientRect(1280, 720, 1600, 900), true, true, false));

    [Fact]
    public void An_unknown_client_size_falls_back_to_the_back_buffer()
    {
        Assert.Equal(new Rectangle(0, 0, 1600, 900), CameraScroll.ClientRect(0, 0, 1600, 900));
        Assert.Equal(new Rectangle(0, 0, 1600, 900), CameraScroll.ClientRect(-1, 720, 1600, 900));
    }

    [Fact]
    public void The_right_band_scrolls_over_the_sidebar_in_a_resized_window()
    {
        Rectangle client = CameraScroll.ClientRect(1280, 720, 1600, 900);
        Assert.Equal(new Vector2(1f, 0f), CameraScroll.EdgeDirection(new Point(1270, 360), client, true, true, false));
        Assert.Equal(Vector2.Zero, CameraScroll.EdgeDirection(new Point(1270, 360), client, true, false, false));   // window not focused
    }
}
