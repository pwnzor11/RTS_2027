// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Xunit;

namespace AI.Tests;

/// <summary>VET-6 (D-127, design 14 section 6): the Recruit AI values, retreats from and targets ranked units.</summary>
public sealed class VeterancyAiTests
{
    [Theory]
    [InlineData(800, 0, 800)]
    [InlineData(800, 1, 1000)]
    [InlineData(800, 2, 1200)]
    [InlineData(800, 3, 1400)]
    [InlineData(1, 3, 1)]      // integer division rounds down
    [InlineData(7, 1, 8)]
    [InlineData(0, 3, 0)]
    public void ValueFollowsTheFormula(int cost, int rank, int expected)
        => Assert.Equal(expected, VeterancyValue.Of(cost, rank));

    [Fact]
    public void RankIsClamped()
    {
        Assert.Equal(VeterancyValue.Of(500, 0), VeterancyValue.Of(500, -4));
        Assert.Equal(VeterancyValue.Of(500, 3), VeterancyValue.Of(500, 9));
    }

    [Fact]
    public void LargeCostsDoNotOverflow()
        => Assert.Equal(1_750_000_000, VeterancyValue.Of(1_000_000_000, 3));
}
