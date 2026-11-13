// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Tools;

namespace Rts.Tools.Tests;

/// <summary>S24 (M13-23, D-242): the score adjudication of `ai match` games that reach the time limit.</summary>
public class MatchAdjudicationTests
{
    [Theory]
    [InlineData(110, 100, 0)]     // exactly 10 % above the other side: A wins
    [InlineData(100, 110, 1)]     // and B the same way round
    [InlineData(1000, 900, 0)]    // 1000 >= 900 * 1.1 = 990
    [InlineData(109, 100, -1)]    // 9 % above: even
    [InlineData(100, 100, -1)]    // equal: even
    [InlineData(0, 0, -1)]        // nothing left on either side: even
    [InlineData(1, 0, 0)]         // any score beats an empty side by the margin
    [InlineData(0, 1, 1)]
    [InlineData(5000, 4000, 0)]   // 25 % above
    public void Higher_score_wins_only_when_it_beats_the_other_by_ten_percent(long a, long b, int expected)
    {
        Assert.Equal(expected, MatchAdjudication.Adjudicate(a, b));
    }

    [Fact]
    public void The_margin_is_read_from_the_lower_score()
    {
        // 1100 vs 1000: 10 % of the lower score is exactly 100, so the higher side wins; 1099 vs 1000 is even
        Assert.Equal(0, MatchAdjudication.Adjudicate(1100, 1000));
        Assert.Equal(MatchAdjudication.Even, MatchAdjudication.Adjudicate(1099, 1000));
    }

    [Fact]
    public void Labels_are_A_B_or_even()
    {
        Assert.Equal("A", MatchAdjudication.Label(0));
        Assert.Equal("B", MatchAdjudication.Label(1));
        Assert.Equal("even", MatchAdjudication.Label(MatchAdjudication.Even));
    }

    [Fact]
    public void Breakdown_total_is_army_plus_structures_plus_credits()
    {
        var b = new MatchAdjudication.Breakdown(Army: 1200, Structures: 800, Credits: 300);
        Assert.Equal(2300, b.Total);
    }
}
