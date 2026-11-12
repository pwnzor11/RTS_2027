// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Sim.Core;

namespace Rts.Game.Tests.M10;

/// <summary>M10-13: wake puff creation, fading and lifetime.</summary>
public sealed class WaterWakeTests
{
    private static readonly EntityId Ship = new(5, 1);

    private static IReadOnlyList<(EntityId, Vector2)> At(float x, float y) => new[] { (Ship, new Vector2(x, y)) };

    [Fact]
    public void Alpha_falls_from_max_to_zero_over_the_lifetime()
    {
        Assert.Equal(WaterWake.MaxAlpha, WaterWake.AlphaAt(0));
        Assert.True(WaterWake.AlphaAt(WaterWake.LifeTicks / 2) < WaterWake.AlphaAt(1));
        Assert.Equal(0, WaterWake.AlphaAt(WaterWake.LifeTicks));
        Assert.Equal(0, WaterWake.AlphaAt(-1));
    }

    [Fact]
    public void A_ship_that_stays_still_leaves_no_wake()
    {
        var wake = new WaterWake();
        for (int t = 0; t < 10; t++) wake.Update(t, At(100, 100));
        Assert.Empty(wake.Puffs);
    }

    [Fact]
    public void A_moving_ship_leaves_puffs_behind_it_with_its_heading()
    {
        var wake = new WaterWake();
        for (int t = 0; t < 10; t++) wake.Update(t, At(100 + t * 6, 100));
        Assert.NotEmpty(wake.Puffs);
        foreach (WakePuff p in wake.Puffs)
        {
            Assert.Equal(1f, p.Dir.X, 3);
            Assert.Equal(0f, p.Dir.Y, 3);
            Assert.True(p.Pos.X < 100 + 9 * 6);
        }
    }

    [Fact]
    public void Puffs_expire_after_their_lifetime()
    {
        var wake = new WaterWake();
        wake.Update(0, At(0, 0));
        wake.Update(1, At(20, 0));
        Assert.Single(wake.Puffs);
        wake.Update(WaterWake.LifeTicks, At(20, 0));
        Assert.Single(wake.Puffs);   // born tick 1: still one tick left
        wake.Update(1 + WaterWake.LifeTicks, At(20, 0));
        Assert.Empty(wake.Puffs);
    }

    [Fact]
    public void Same_tick_twice_adds_nothing_and_a_jump_is_not_sailing()
    {
        var wake = new WaterWake();
        wake.Update(0, At(0, 0));
        wake.Update(1, At(20, 0));
        wake.Update(1, At(40, 0));
        Assert.Single(wake.Puffs);
        wake.Update(2, At(900, 0));
        Assert.Single(wake.Puffs);
    }

    [Fact]
    public void Time_going_backwards_clears_the_wake()
    {
        var wake = new WaterWake();
        wake.Update(10, At(0, 0));
        wake.Update(11, At(20, 0));
        wake.Update(3, At(0, 0));
        Assert.Empty(wake.Puffs);
    }

    [Fact]
    public void Puff_count_is_capped()
    {
        var wake = new WaterWake();
        var ids = new List<(EntityId, Vector2)>();
        for (ushort i = 1; i <= 100; i++) ids.Add((new EntityId(i, 1), Vector2.Zero));
        wake.Update(0, ids);
        for (int t = 1; t < 20; t++)
        {
            for (int i = 0; i < ids.Count; i++) ids[i] = (ids[i].Item1, new Vector2(t * 12, 0));
            wake.Update(t, ids);
        }

        Assert.True(wake.Puffs.Count <= WaterWake.MaxPuffs);
    }
}
