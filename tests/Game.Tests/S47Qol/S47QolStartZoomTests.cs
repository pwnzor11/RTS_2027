// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Screens;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13 (cnc#42): the zoom a match starts at.</summary>
public sealed class S47QolStartZoomTests
{
    [Theory]
    [InlineData(50, 0.5f)]
    [InlineData(100, 1f)]
    [InlineData(200, 2f)]
    [InlineData(160, 2f)]
    [InlineData(60, 0.5f)]
    public void The_camera_takes_the_nearest_zoom_level(int percent, float zoom)
    {
        var camera = new Camera();
        camera.SetZoomPercent(percent);
        Assert.Equal(zoom, camera.Zoom);
    }

    [Fact]
    public void Stepping_wraps_round_the_three_levels_and_the_default_is_normal()
    {
        Settings s = Settings.Default;
        Assert.Equal(100, s.StartZoomPercent);
        Assert.Equal(200, PlaySettingsScreen.StepStartZoom(s, 1).StartZoomPercent);
        Assert.Equal(50, PlaySettingsScreen.StepStartZoom(PlaySettingsScreen.StepStartZoom(s, 1), 1).StartZoomPercent);
        Assert.Equal(50, PlaySettingsScreen.StepStartZoom(s, -1).StartZoomPercent);
    }

    [Fact]
    public void The_start_zoom_is_saved_and_a_stray_value_snaps()
    {
        Assert.Equal(200, Settings.Parse((Settings.Default with { StartZoomPercent = 200 }).ToToml()).StartZoomPercent);
        Assert.Equal(100, Settings.Parse("version = 2\nstart_zoom_percent = 90\n").StartZoomPercent);
    }
}
