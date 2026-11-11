// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Tests.S19;

/// <summary>S19 UI1 item 3 (D-205, GAMEREND-07): full screen is borderless at the desktop size; windowed keeps the saved size.</summary>
public sealed class DisplayPlanTests
{
    [Fact]
    public void Fullscreen_is_borderless_at_the_desktop_size()
    {
        Assert.False(DisplayPlan.HardwareModeSwitch);
        DisplayPlan plan = DisplayPlan.For(new Settings(Fullscreen: true), 2560, 1440);
        Assert.Equal(new DisplayPlan(true, 2560, 1440), plan);
    }

    [Fact]
    public void Windowed_keeps_the_saved_size()
    {
        Assert.Equal(new DisplayPlan(false, 1280, 720), DisplayPlan.For(new Settings(), 2560, 1440));
    }

    [Fact]
    public void Unknown_desktop_falls_back_to_the_saved_size()
    {
        Assert.Equal(new DisplayPlan(true, 1280, 720), DisplayPlan.For(new Settings(Fullscreen: true), 0, 0));
    }

    [Fact]
    public void The_setting_key_is_still_fullscreen()
    {
        string toml = new Settings(Fullscreen: true).ToToml();
        Assert.Contains("fullscreen = true", toml);
    }
}
