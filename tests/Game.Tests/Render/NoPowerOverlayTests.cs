// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;

namespace Rts.Game.Tests.Render;

/// <summary>M11-11 / D-170: when the no-power marker shows, and its blink phase.</summary>
public sealed class NoPowerOverlayTests
{
    [Fact]
    public void Shows_only_when_every_condition_holds()
    {
        Assert.True(NoPowerOverlay.ShouldShow(needsPower: true, powerBand: 1, isLocalPlayer: true, isAlive: true));
        Assert.False(NoPowerOverlay.ShouldShow(needsPower: false, powerBand: 1, isLocalPlayer: true, isAlive: true));
        Assert.False(NoPowerOverlay.ShouldShow(needsPower: true, powerBand: 0, isLocalPlayer: true, isAlive: true));
        Assert.False(NoPowerOverlay.ShouldShow(needsPower: true, powerBand: 1, isLocalPlayer: false, isAlive: true));
        Assert.False(NoPowerOverlay.ShouldShow(needsPower: true, powerBand: 1, isLocalPlayer: true, isAlive: false));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void Power_band_zero_is_full_power_and_hides_the_marker(int band, bool expected)
    {
        Assert.Equal(expected, NoPowerOverlay.ShouldShow(needsPower: true, powerBand: band, isLocalPlayer: true, isAlive: true));
    }

    [Theory]
    [InlineData(0f, true)]
    [InlineData(0.25f, true)]
    [InlineData(0.49f, true)]
    [InlineData(0.5f, false)]
    [InlineData(0.75f, false)]
    [InlineData(0.99f, false)]
    [InlineData(1.0f, true)]
    [InlineData(1.49f, true)]
    [InlineData(1.5f, false)]
    [InlineData(10.0f, true)]
    [InlineData(10.5f, false)]
    public void Blink_is_on_for_half_a_second_then_off_for_half_a_second(float seconds, bool expected)
    {
        Assert.Equal(expected, NoPowerOverlay.BlinkOn(seconds));
    }
}
