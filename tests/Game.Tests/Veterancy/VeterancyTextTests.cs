// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Veterancy;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Veterancy;

/// <summary>VET-5: rank pips count and the info panel text (own units only).</summary>
public sealed class VeterancyTextTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    [InlineData(-1, 0)]
    public void PipCountFollowsTheRank(int rank, int pips) =>
        Assert.Equal(pips, Rts.Game.Render.RankPips.PipCount(rank));

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "Veteran")]
    [InlineData(2, "Elite")]
    [InlineData(3, "Hero")]
    public void RankNamesAreVeteranEliteHero(int rank, string? name) =>
        Assert.Equal(name, VeterancyText.RankNameKey(rank) is { } k ? VeterancyText.Placeholder(k) : null);

    [Fact]
    public void OwnRankedUnitShowsNameAndNextThreshold()
    {
        Assert.Equal(new[] { "Veteran", "Next rank: 200/1600" }, VeterancyText.InfoLines(true, 1, 200, 1600));
    }

    [Fact]
    public void RecruitWithAThresholdShowsOnlyNextRank()
    {
        Assert.Equal(new[] { "Next rank: 0/600" }, VeterancyText.InfoLines(true, 0, 0, 600));
    }

    [Fact]
    public void TopRankShowsHeroAndMaxRank()
    {
        Assert.Equal(new[] { "Hero", "Max rank" }, VeterancyText.InfoLines(true, VeterancyRules.Ranks, 4000, 0));
    }

    [Fact]
    public void UnitThatCannotRankShowsNothing()
    {
        Assert.Empty(VeterancyText.InfoLines(true, 0, 0, 0));
    }

    [Fact]
    public void AnotherPlayersUnitShowsNothingEvenWhenRanked()
    {
        Assert.Empty(VeterancyText.InfoLines(false, 2, 900, 3200));
    }
}
