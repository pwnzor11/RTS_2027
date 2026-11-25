// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S45Fx;

/// <summary>S45 V-04: the tread mark pool (cap, recycling, expiry), the fade and the ground rule.</summary>
public sealed class TreadMarkTests
{
    private static TreadMark Mark(int born, float x = 0) => new(x, 0, 0, born, true);

    [Fact]
    public void Pool_recycles_the_oldest_at_the_cap()
    {
        var pool = new TreadMarkPool(4);
        for (int i = 0; i < 6; i++) pool.Add(Mark(i, i));
        Assert.Equal(4, pool.Count);
        Assert.Equal(2f, pool[0].X);   // 0 and 1 were recycled
        Assert.Equal(5f, pool[3].X);
    }

    [Fact]
    public void Default_pool_is_capped_at_512()
    {
        var pool = new TreadMarkPool();
        for (int i = 0; i < 2000; i++) pool.Add(Mark(i));
        Assert.Equal(512, pool.Capacity);
        Assert.Equal(512, pool.Count);
        Assert.Equal(2000 - 512, pool[0].Born);
    }

    [Fact]
    public void Prune_drops_expired_marks_oldest_first()
    {
        var pool = new TreadMarkPool(8);
        pool.Add(Mark(0));
        pool.Add(Mark(100));
        pool.Add(Mark(300));
        pool.Prune(TreadTiming.LifeTicks);          // the first is exactly at the end of its life
        Assert.Equal(2, pool.Count);
        pool.Prune(100 + TreadTiming.LifeTicks);
        Assert.Equal(1, pool.Count);
        Assert.Equal(300, pool[0].Born);
    }

    [Fact]
    public void Prune_clears_marks_from_the_future()
    {
        var pool = new TreadMarkPool(8);
        pool.Add(Mark(500));
        pool.Prune(10);   // a load went back in time
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void Alpha_holds_then_fades_to_nothing_over_about_twenty_seconds()
    {
        Assert.Equal(20 * 20, TreadTiming.LifeTicks);
        Assert.Equal(0, TreadTiming.Alpha(true, -1));
        Assert.Equal(TreadTiming.HeavyAlpha, TreadTiming.Alpha(true, TreadTiming.HoldTicks));
        int mid = TreadTiming.Alpha(true, (TreadTiming.HoldTicks + TreadTiming.LifeTicks) / 2);
        Assert.InRange(mid, 1, TreadTiming.HeavyAlpha - 1);
        Assert.True(TreadTiming.Alpha(true, TreadTiming.LifeTicks - 1) >= 1);
        Assert.Equal(0, TreadTiming.Alpha(true, TreadTiming.LifeTicks));
    }

    [Fact]
    public void Alpha_never_rises_with_age_after_the_hold_and_wheels_are_fainter()
    {
        int last = int.MaxValue;
        for (int age = TreadTiming.HoldTicks; age < TreadTiming.LifeTicks; age++)
        {
            int a = TreadTiming.Alpha(true, age);
            Assert.True(a <= last);
            last = a;
        }

        Assert.True(TreadTiming.Alpha(false, 30) < TreadTiming.Alpha(true, 30));
    }

    [Fact]
    public void Marks_press_in_instead_of_popping()
    {
        Assert.True(TreadTiming.Alpha(true, 0) < TreadTiming.Alpha(true, TreadTiming.FadeInTicks));
    }

    [Theory]
    [InlineData(LandType.Clear, true)]
    [InlineData(LandType.Ash, true)]
    [InlineData(LandType.Rough, true)]
    [InlineData(LandType.Road, false)]
    [InlineData(LandType.Water, false)]
    [InlineData(LandType.Shallow, false)]
    [InlineData(LandType.Rock, false)]
    [InlineData(LandType.Cliff, false)]
    public void Only_soft_ground_takes_marks(LandType land, bool expected) =>
        Assert.Equal(expected, TreadTiming.SoftGround(land));
}
