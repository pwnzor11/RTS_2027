// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Sim.Core;

namespace Rts.Game.Tests.M3;

/// <summary>M3-08 / D-075: the surfacing telegraph's darkening, cracks and shake as pure functions of kind and ticks left.</summary>
public sealed class TelegraphRendererTests
{
    private static readonly CellCoord Cell = new(7, 9);

    [Fact]
    public void Lengths_are_three_seconds_for_a_unit_and_five_for_an_entrance_at_20_hz()
    {
        Assert.Equal(60, TelegraphRenderer.TotalTicks(TelegraphKind.Unit));
        Assert.Equal(100, TelegraphRenderer.TotalTicks(TelegraphKind.Entrance));
    }

    [Theory]
    [InlineData(TelegraphKind.Unit)]
    [InlineData(TelegraphKind.Entrance)]
    public void Progress_runs_from_zero_to_one_and_clamps(TelegraphKind kind)
    {
        int total = TelegraphRenderer.TotalTicks(kind);
        Assert.Equal(0f, TelegraphRenderer.Progress(kind, total));
        Assert.Equal(0f, TelegraphRenderer.Progress(kind, total + 50));
        Assert.Equal(0.5f, TelegraphRenderer.Progress(kind, total / 2), 3);
        Assert.Equal(1f, TelegraphRenderer.Progress(kind, 0));
        Assert.Equal(1f, TelegraphRenderer.Progress(kind, -3));
    }

    [Theory]
    [InlineData(TelegraphKind.Unit)]
    [InlineData(TelegraphKind.Entrance)]
    public void Darkening_is_visible_at_the_start_and_only_deepens(TelegraphKind kind)
    {
        int total = TelegraphRenderer.TotalTicks(kind);
        Assert.InRange(TelegraphRenderer.DarkenAlpha(kind, total), 30, 80);
        Assert.Equal(TelegraphRenderer.MaxDarkenAlpha, TelegraphRenderer.DarkenAlpha(kind, 0));
        int previous = -1;
        for (int left = total; left >= 0; left--)
        {
            int a = TelegraphRenderer.DarkenAlpha(kind, left);
            Assert.True(a >= previous, $"alpha fell at {left} ticks left");
            previous = a;
        }
    }

    [Theory]
    [InlineData(TelegraphKind.Unit)]
    [InlineData(TelegraphKind.Entrance)]
    public void Cracks_appear_after_the_darkening_and_never_fade_again(TelegraphKind kind)
    {
        int total = TelegraphRenderer.TotalTicks(kind);
        Assert.Equal(0, TelegraphRenderer.CrackAlpha(kind, total));
        Assert.Equal(0, TelegraphRenderer.CrackCount(kind, total));
        Assert.True(TelegraphRenderer.CrackAlpha(kind, 0) > 200);
        Assert.True(TelegraphRenderer.CrackCount(kind, 0) >= 1);
        int previousAlpha = -1, previousCount = -1;
        for (int left = total; left >= 0; left--)
        {
            int a = TelegraphRenderer.CrackAlpha(kind, left), n = TelegraphRenderer.CrackCount(kind, left);
            Assert.True(a >= previousAlpha);
            Assert.True(n >= previousCount);
            previousAlpha = a;
            previousCount = n;
        }
    }

    [Fact]
    public void An_entrance_telegraph_is_bigger_and_cracks_more_than_a_unit_one()
    {
        Assert.Equal(0, TelegraphRenderer.RingCells(TelegraphKind.Unit));
        Assert.Equal(1, TelegraphRenderer.RingCells(TelegraphKind.Entrance));
        Assert.True(TelegraphRenderer.CrackCount(TelegraphKind.Entrance, 0) > TelegraphRenderer.CrackCount(TelegraphKind.Unit, 0));
    }

    [Theory]
    [InlineData(TelegraphKind.Unit, 3)]
    [InlineData(TelegraphKind.Entrance, 4)]
    public void Shake_is_small_at_the_start_grows_and_stops_when_the_telegraph_is_over(TelegraphKind kind, int maxAmplitude)
    {
        int total = TelegraphRenderer.TotalTicks(kind);
        Assert.Equal(1, TelegraphRenderer.ShakeAmplitudePx(kind, total));
        Assert.Equal(maxAmplitude, TelegraphRenderer.ShakeAmplitudePx(kind, 1));
        Assert.Equal(0, TelegraphRenderer.ShakeAmplitudePx(kind, 0));
        Assert.Equal(Point.Zero, TelegraphRenderer.ShakeOffsetPx(kind, 0, 123, Cell));
        Assert.Equal(Point.Zero, TelegraphRenderer.ShakeOffsetPx(kind, -5, 123, Cell));
    }

    [Theory]
    [InlineData(TelegraphKind.Unit)]
    [InlineData(TelegraphKind.Entrance)]
    public void Shake_offset_stays_inside_the_amplitude_moves_and_repeats_for_the_same_inputs(TelegraphKind kind)
    {
        int total = TelegraphRenderer.TotalTicks(kind);
        var seen = new HashSet<Point>();
        for (int left = total; left > 0; left--)
        {
            int tick = 1000 + (total - left);
            int amp = TelegraphRenderer.ShakeAmplitudePx(kind, left);
            Point p = TelegraphRenderer.ShakeOffsetPx(kind, left, tick, Cell);
            Assert.InRange(p.X, -amp, amp);
            Assert.InRange(p.Y, -amp, amp);
            Assert.Equal(p, TelegraphRenderer.ShakeOffsetPx(kind, left, tick, Cell));
            seen.Add(p);
        }

        Assert.True(seen.Count > 4, "the tile should actually move");
    }

    [Fact]
    public void Neighbouring_telegraphs_do_not_shake_in_lock_step()
    {
        int differing = 0;
        for (int tick = 0; tick < 40; tick++)
        {
            if (TelegraphRenderer.ShakeOffsetPx(TelegraphKind.Unit, 30, tick, new CellCoord(3, 3))
                != TelegraphRenderer.ShakeOffsetPx(TelegraphKind.Unit, 30, tick, new CellCoord(4, 3))) differing++;
        }

        Assert.True(differing > 10);
    }

    [Fact]
    public void Dust_rises_fades_in_and_out_and_is_a_pure_function()
    {
        for (int i = 0; i < 6; i++)
        {
            TelegraphRenderer.DustSpeck(i, 50, Cell, 20, out Vector2 o1, out byte a1);
            TelegraphRenderer.DustSpeck(i, 50, Cell, 20, out Vector2 o2, out byte a2);
            Assert.Equal(o1, o2);
            Assert.Equal(a1, a2);
            Assert.True(o1.Y <= 20f && o1.Y > -60f);
        }

        // Over a 24-tick cycle the speck's opacity starts and ends at zero and peaks in the middle.
        byte max = 0;
        for (int tick = 0; tick < 24; tick++)
        {
            TelegraphRenderer.DustSpeck(0, tick, Cell, 20, out _, out byte a);
            max = Math.Max(max, a);
        }

        Assert.True(max > 100);
        Assert.True(TelegraphRenderer.DustCount(TelegraphKind.Unit, 0) > TelegraphRenderer.DustCount(TelegraphKind.Unit, 60));
    }

    [Fact]
    public void A_crack_path_runs_outwards_from_the_centre_and_is_stable()
    {
        Vector2[] a = TelegraphRenderer.CrackPath(Cell, 2, 5, 30f);
        Vector2[] b = TelegraphRenderer.CrackPath(Cell, 2, 5, 30f);
        Assert.Equal(a, b);
        Assert.Equal(Vector2.Zero, a[0]);
        Assert.True(a[^1].Length() > 15f);
        Assert.NotEqual(a, TelegraphRenderer.CrackPath(Cell, 3, 5, 30f));
    }
}
