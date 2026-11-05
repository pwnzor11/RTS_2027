// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Veterancy;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Veterancy;

/// <summary>VET-5: promotions are detected client-side when a listed rank rises; the flash lasts a fixed number of ticks.</summary>
public sealed class PromotionTrackerTests
{
    private static readonly RankKey A = new(UnitKind.Vehicle, new EntityId(1, 1));
    private static readonly RankKey B = new(UnitKind.Infantry, new EntityId(1, 1));

    private static List<RankKey> Step(PromotionTracker t, int tick, params (RankKey, int)[] ranks)
    {
        var promoted = new List<RankKey>();
        t.Update(tick, ranks, promoted);
        return promoted;
    }

    [Fact]
    public void FirstSightingIsNeverAPromotion()
    {
        var t = new PromotionTracker();
        Assert.Empty(Step(t, 1, (A, 3)));
    }

    [Fact]
    public void RiseIsReportedOnceAndNotRepeatedAtTheSameRank()
    {
        var t = new PromotionTracker();
        Step(t, 1, (A, 0));
        Assert.Equal(new[] { A }, Step(t, 2, (A, 1)));
        Assert.Empty(Step(t, 3, (A, 1)));
        Assert.Empty(Step(t, 4, (A, 1)));
    }

    [Fact]
    public void EqualAndLowerRanksAreNotPromotions()
    {
        var t = new PromotionTracker();
        Step(t, 1, (A, 2));
        Assert.Empty(Step(t, 2, (A, 2)));
        Assert.Empty(Step(t, 3, (A, 1)));
    }

    [Fact]
    public void ANewIdIsNotAPromotionEvenAtAHighRank()
    {
        var t = new PromotionTracker();
        Step(t, 1, (A, 0));
        Assert.Empty(Step(t, 2, (A, 0), (B, 3)));
    }

    [Fact]
    public void AnIdThatLeavesTheListIsForgottenSoItsReturnStartsFresh()
    {
        var t = new PromotionTracker();
        Step(t, 1, (A, 1));
        Step(t, 2);
        Assert.Equal(0, t.Tracked);
        Assert.Empty(Step(t, 3, (A, 2)));
    }

    [Fact]
    public void FlashRunsForFixedTicksFromThePromotion()
    {
        var t = new PromotionTracker();
        Step(t, 5, (A, 0));
        Step(t, 6, (A, 1));
        Assert.True(t.IsFlashing(A, 6));
        Assert.True(t.IsFlashing(A, 6 + PromotionTracker.FlashTicks - 1));
        Assert.False(t.IsFlashing(A, 6 + PromotionTracker.FlashTicks));
        Assert.False(t.IsFlashing(B, 6));
    }

    [Fact]
    public void FlashEndsWhenTheUnitLeavesTheList()
    {
        var t = new PromotionTracker();
        Step(t, 1, (A, 0));
        Step(t, 2, (A, 1));
        Step(t, 3);
        Assert.False(t.IsFlashing(A, 3));
    }
}
