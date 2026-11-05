// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Xunit;

namespace AI.Tests.M4;

/// <summary>M4-16 round 4: the wave strength gate and the escalation decisions.</summary>
public sealed class WaveGateTests
{
    [Fact]
    public void Tier1IsNeverGated()
        => Assert.True(WaveGate.Strong(false, 100, 10000, 5000, 3, 4000, 3900));

    [Fact]
    public void NoEnemySeenMeansNoGate()
        => Assert.True(WaveGate.Strong(true, 100, 0, 0, 8, 4000, 3900));

    [Fact]
    public void WaveWaitsWhenSmallerThanTheEnemyArmy()
        => Assert.False(WaveGate.Strong(true, 1999, 2000, 0, 10, 4000, 3500));

    [Fact]
    public void WaveGoesAtEqualValue()
        => Assert.True(WaveGate.Strong(true, 2000, 2000, 0, 10, 4000, 3500));

    [Fact]
    public void GateLiftsAfterTheWaitAndAtTheUnitCap()
    {
        Assert.False(WaveGate.Strong(true, 500, 5000, 0, 10, 8999, 3000));
        Assert.True(WaveGate.Strong(true, 500, 5000, 0, 10, 9000, 3000));
        Assert.False(WaveGate.Strong(true, 500, 5000, 0, WaveGate.GateCapUnits - 1, 4000, 3500));
        Assert.True(WaveGate.Strong(true, 500, 5000, 0, WaveGate.GateCapUnits, 4000, 3500));
    }

    [Fact]
    public void EscalationRaisesTheRequiredValue()
    {
        Assert.Equal(3000, WaveGate.Escalate(true, 0, 1500));
        Assert.Equal(4000, WaveGate.Escalate(true, 4000, 1500));   // never lowered
        Assert.Equal(WaveGate.EscalateCap, WaveGate.Escalate(true, 0, 50000));
        Assert.Equal(0, WaveGate.Escalate(false, 0, 1500));        // tier 1 unchanged
    }

    [Fact]
    public void RequiredValueBlocksAWaveEvenAgainstAWeakEnemy()
    {
        Assert.False(WaveGate.Strong(true, 2500, 100, 3000, 10, 4000, 3500));
        Assert.True(WaveGate.Strong(true, 3000, 100, 3000, 10, 4000, 3500));
    }
}
