// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;

namespace Rts.Game.Tests.S45Sys;

/// <summary>U-07 (GAMESYS-03): the window size is remembered, and only sizes that fit the desktop are offered.</summary>
public sealed class WindowSizeTests
{
    [Fact]
    public void A_size_fits_only_when_both_sides_fit_the_desktop()
    {
        Assert.True(WindowSizeRule.Fits(1920, 1080, 1920, 1080));
        Assert.False(WindowSizeRule.Fits(2560, 1440, 1920, 1080));
        Assert.False(WindowSizeRule.Fits(1280, 1200, 1920, 1080));
        Assert.True(WindowSizeRule.Fits(2560, 1440, 0, 0));   // unknown desktop: everything fits
    }

    [Fact]
    public void Stepping_skips_sizes_larger_than_the_desktop()
    {
        Assert.Equal((1600, 900), SettingsScreen.NextWindowSize(1280, 720, 1, 1920, 1080));
        Assert.Equal((1280, 720), SettingsScreen.NextWindowSize(1920, 1080, 1, 1920, 1080));   // 2560 x 1440 is never offered here
        Assert.Equal((1920, 1080), SettingsScreen.NextWindowSize(1280, 720, -1, 1920, 1080));
    }

    [Fact]
    public void A_size_that_no_longer_fits_starts_at_the_first_that_does()
    {
        Assert.Equal((1280, 720), SettingsScreen.NextWindowSize(2560, 1440, 1, 1920, 1080));
        Assert.Equal((1920, 1080), SettingsScreen.NextWindowSize(2560, 1440, -1, 1920, 1080));
    }

    [Fact]
    public void Every_preset_is_offered_on_a_big_desktop()
    {
        Assert.Equal((2560, 1440), SettingsScreen.NextWindowSize(1920, 1080, 1, 2560, 1440));
    }

    [Fact]
    public void A_resized_window_is_remembered()
    {
        var s = Settings.Default;
        Settings r = WindowSizeRule.Remember(s, fullScreen: false, clientWidth: 1366, clientHeight: 768);
        Assert.Equal(1366, r.WindowWidth);
        Assert.Equal(768, r.WindowHeight);
    }

    [Fact]
    public void Nothing_is_remembered_for_full_screen_tiny_or_unchanged_sizes()
    {
        var s = Settings.Default;
        Assert.Same(s, WindowSizeRule.Remember(s, fullScreen: true, 2560, 1440));
        Assert.Same(s, WindowSizeRule.Remember(s, fullScreen: false, 200, 150));
        Assert.Same(s, WindowSizeRule.Remember(s, fullScreen: false, s.WindowWidth, s.WindowHeight));
    }

    [Fact]
    public void A_saved_window_larger_than_the_desktop_is_clamped_when_windowed()
    {
        var s = new Settings(WindowWidth: 2560, WindowHeight: 1440);
        Assert.Equal(new DisplayPlan(false, 1920, 1080), DisplayPlan.For(s, 1920, 1080));
        Assert.Equal(new DisplayPlan(false, 2560, 1440), DisplayPlan.For(s, 0, 0));
    }
}
