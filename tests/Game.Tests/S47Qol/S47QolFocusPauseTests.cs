// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13: the setting that turns the pause on focus loss off.</summary>
public sealed class S47QolFocusPauseTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    public void A_background_window_only_counts_as_inactive_while_the_pause_is_wanted(bool pauseWanted, bool windowActive, bool counts)
    {
        Assert.Equal(counts, FocusPause.CountsAsActive(windowActive, Settings.Default with { PauseOnFocusLoss = pauseWanted }));
    }

    [Fact]
    public void The_pause_is_on_by_default_and_the_choice_is_saved()
    {
        Assert.True(Settings.Default.PauseOnFocusLoss);
        Assert.False(Settings.Parse((Settings.Default with { PauseOnFocusLoss = false }).ToToml()).PauseOnFocusLoss);
    }
}
