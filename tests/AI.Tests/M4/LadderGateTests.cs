// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Rts.Data;
using Xunit;

namespace AI.Tests.M4;

/// <summary>M4-16 s12: the tier-dependent attack ratio and the predicted-loss retreat decisions.</summary>
public sealed class LadderGateTests
{
    [Fact]
    public void RatioZeroKeepsTheDefaultGate()
    {
        Assert.False(WaveGate.Strong(true, 1999, 2000, 0, 10, 4000, 3500, 0));
        Assert.True(WaveGate.Strong(true, 2000, 2000, 0, 10, 4000, 3500, 0));
    }

    [Fact]
    public void HigherRatioNeedsMoreValue()
    {
        Assert.False(WaveGate.Strong(true, 2499, 2000, 0, 10, 4000, 3500, 125));
        Assert.True(WaveGate.Strong(true, 2500, 2000, 0, 10, 4000, 3500, 125));
        Assert.True(WaveGate.Strong(true, 100, 0, 0, 10, 4000, 3500, 150));   // no enemy seen: no gate
    }

    [Fact]
    public void RatioGateStillLiftsAtTheUnitCapAndTheWait()
    {
        Assert.True(WaveGate.Strong(true, 500, 5000, 0, WaveGate.GateCapUnits, 4000, 3500, 150));
        Assert.True(WaveGate.Strong(true, 500, 5000, 0, 10, 9000, 3000, 150));
    }

    [Fact]
    public void LosingNeedsARealEnemyForce()
    {
        Assert.False(WaveGate.Losing(0, WaveGate.RetreatMinEnemy - 1, 0));
        Assert.True(WaveGate.Losing(0, WaveGate.RetreatMinEnemy, 0));
    }

    [Fact]
    public void LosingUsesTheRatio()
    {
        Assert.False(WaveGate.Losing(1400, 2000, 70));   // exactly 70 %
        Assert.True(WaveGate.Losing(1399, 2000, 70));
        Assert.True(WaveGate.Losing(1399, 2000, 0));     // 0 = default 70
        Assert.False(WaveGate.Losing(1399, 2000, 50));
    }

    [Fact]
    public void ShippedLadderRaisesTheRatioWithTier()
    {
        string? file = DifficultyLoader.FindFile(AppContext.BaseDirectory);
        Assert.NotNull(file);
        var profiles = DifficultyLoader.Load(file!);
        int last = 0;
        foreach (DifficultyProfile p in profiles.Where(p => p.Tier >= 2))
        {
            Assert.True(p.AttackRatioPct >= last, $"tier {p.Tier} ratio {p.AttackRatioPct} below {last}");
            last = p.AttackRatioPct;
        }
    }
}
