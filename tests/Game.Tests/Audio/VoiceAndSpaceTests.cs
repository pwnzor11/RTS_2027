// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Audio;

/// <summary>The per-sound voice cap and the camera-relative volume and pan.</summary>
public sealed class VoiceAndSpaceTests
{
    [Fact]
    public void VoiceCapDropsPlaysBeyondTheLimitAndFreesThemWhenTheyEnd()
    {
        var limiter = new VoiceLimiter();
        Assert.True(limiter.TryAcquire("boom", 0, 500, 2));
        Assert.True(limiter.TryAcquire("boom", 100, 500, 2));
        Assert.False(limiter.TryAcquire("boom", 200, 500, 2));
        Assert.Equal(2, limiter.Active("boom", 200));
        Assert.True(limiter.TryAcquire("boom", 500, 500, 2)); // the first copy has ended
        Assert.False(limiter.TryAcquire("boom", 550, 500, 2));
    }

    [Fact]
    public void VoiceCapIsPerSound()
    {
        var limiter = new VoiceLimiter();
        Assert.True(limiter.TryAcquire("a", 0, 1000, 1));
        Assert.False(limiter.TryAcquire("a", 1, 1000, 1));
        Assert.True(limiter.TryAcquire("b", 1, 1000, 1));
    }

    [Fact]
    public void GainFallsWithDistanceAndPanFollowsOffset()
    {
        LeptonCoord listener = LeptonCoord.CenterOf(new CellCoord(50, 50));
        (float nearGain, float nearPan) = Spatializer.Compute(listener, listener, 48f, 24f);
        Assert.Equal(1f, nearGain);
        Assert.Equal(0f, nearPan);

        (float rightGain, float rightPan) = Spatializer.Compute(listener, LeptonCoord.CenterOf(new CellCoord(70, 50)), 48f, 24f);
        Assert.InRange(rightGain, 0.1f, 0.9f);
        Assert.True(rightPan > 0.5f);

        (float leftGain, float leftPan) = Spatializer.Compute(listener, LeptonCoord.CenterOf(new CellCoord(30, 50)), 48f, 24f);
        Assert.Equal(rightGain, leftGain, 3);
        Assert.Equal(-rightPan, leftPan, 3);

        (float farGain, _) = Spatializer.Compute(listener, LeptonCoord.CenterOf(new CellCoord(150, 50)), 48f, 24f);
        Assert.Equal(0f, farGain);
    }
}
