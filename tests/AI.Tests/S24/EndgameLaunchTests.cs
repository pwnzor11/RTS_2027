// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Xunit;

namespace AI.Tests.S24;

/// <summary>s24 (M13-23, D-242): the endgame-broke decision and the endgame launch.</summary>
public sealed class EndgameLaunchTests
{
    [Fact]
    public void RescueBrokeIsEndgameAtAnyTick() => Assert.True(EndgameLaunch.Broke(true, 2, 5000));

    [Fact]
    public void HarvestersAliveEarlyIsNotBroke() => Assert.False(EndgameLaunch.Broke(false, 0, EndgameLaunch.LateTick - 1));

    [Fact]
    public void NoHarvesterAfterTheLateTickIsBroke() => Assert.True(EndgameLaunch.Broke(false, 0, EndgameLaunch.LateTick));

    [Fact]
    public void HarvestersAliveLateAndNotStalledIsNotBroke() => Assert.False(EndgameLaunch.Broke(false, 1, 25000));

    [Fact]
    public void LaunchNeedsTwoUnits()
    {
        Assert.False(EndgameLaunch.Launch(true, 0, 20000, EndgameLaunch.MinArmy - 1));
        Assert.True(EndgameLaunch.Launch(true, 0, 20000, EndgameLaunch.MinArmy));
    }

    [Fact]
    public void GuardIsZeroOnlyInTheEndgame()
    {
        Assert.Equal(0, EndgameLaunch.Guard(true, 3));
        Assert.Equal(3, EndgameLaunch.Guard(false, 3));
    }
}
