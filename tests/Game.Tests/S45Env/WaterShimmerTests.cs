// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S45Env;

/// <summary>M16-V-04: which water cells shimmer (band position, fog, land) and where the sparkles sit. Pure maths, no GPU.</summary>
public sealed class WaterShimmerTests
{
    private static LandType[] Lake(int w, int h)
    {
        var land = new LandType[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                land[y * w + x] = x >= w / 2 ? LandType.Water : LandType.Clear;
        return land;
    }

    [Fact]
    public void Band_is_brightest_on_its_centre_line_and_dark_far_from_it()
    {
        // centre = tick / 3; cell (0,0) sits at band unit 0
        Assert.Equal(WaterShimmer.MaxAlpha, WaterShimmer.BandAlpha(0, 0, 0));
        Assert.True(WaterShimmer.BandAlpha(0, 0, 3 * 3) < WaterShimmer.MaxAlpha);
        Assert.Equal(0, WaterShimmer.BandAlpha(0, 0, 3 * WaterShimmer.BandHalfWidth));
        Assert.Equal(0, WaterShimmer.BandAlpha(0, 0, 3 * WaterShimmer.BandPeriod / 2));
    }

    [Fact]
    public void Band_moves_with_the_tick_and_repeats_every_period()
    {
        int period = WaterShimmer.TicksPerStep * WaterShimmer.BandPeriod;
        for (int x = 0; x < 20; x++)
            Assert.Equal(WaterShimmer.BandAlpha(x, 3, 100), WaterShimmer.BandAlpha(x, 3, 100 + period));
        Assert.NotEqual(WaterShimmer.BandAlpha(5, 5, 0), WaterShimmer.BandAlpha(5, 5, 60));
    }

    [Fact]
    public void Only_water_cells_are_selected()
    {
        const int w = 16, h = 16;
        var lit = new List<WaterShimmer.LitCell>();
        for (int tick = 0; tick < 240; tick += 3)
            WaterShimmer.Select(Lake(w, h), w, h, null, 0, 0, w - 1, h - 1, tick, lit);
        Assert.NotEmpty(lit);
        Assert.All(lit, c => Assert.True(c.X >= w / 2));
        Assert.All(lit, c => Assert.InRange((int)c.Alpha, 1, WaterShimmer.MaxAlpha));
    }

    [Fact]
    public void Cells_not_visible_now_are_skipped()
    {
        const int w = 16, h = 16;
        var land = Lake(w, h);
        var all = new List<WaterShimmer.LitCell>();
        WaterShimmer.Select(land, w, h, null, 0, 0, w - 1, h - 1, 0, all);
        Assert.NotEmpty(all);

        var seen = new byte[w * h];
        Array.Fill(seen, (byte)1);                  // seen before, not visible now
        var lit = new List<WaterShimmer.LitCell>();
        WaterShimmer.Select(land, w, h, seen, 0, 0, w - 1, h - 1, 0, lit);
        Assert.Empty(lit);

        Array.Fill(seen, (byte)0);                  // never seen
        WaterShimmer.Select(land, w, h, seen, 0, 0, w - 1, h - 1, 0, lit);
        Assert.Empty(lit);

        Array.Fill(seen, (byte)2);                  // visible now
        WaterShimmer.Select(land, w, h, seen, 0, 0, w - 1, h - 1, 0, lit);
        Assert.Equal(all, lit);

        // a vision list of the wrong size is ignored (omniscient), not trusted
        lit.Clear();
        WaterShimmer.Select(land, w, h, new byte[3], 0, 0, w - 1, h - 1, 0, lit);
        Assert.Equal(all, lit);
    }

    [Fact]
    public void Selection_respects_the_cell_range_and_map_edges()
    {
        const int w = 16, h = 16;
        var land = new LandType[w * h];
        Array.Fill(land, LandType.Shallow);
        var lit = new List<WaterShimmer.LitCell>();
        for (int tick = 0; tick < 240; tick += 3) WaterShimmer.Select(land, w, h, null, 4, 4, 7, 7, tick, lit);
        Assert.NotEmpty(lit);
        Assert.All(lit, c => Assert.True(c.X is >= 4 and <= 7 && c.Y is >= 4 and <= 7));
        lit.Clear();
        WaterShimmer.Select(land, w, h, null, -5, -5, 99, 99, 0, lit);   // out-of-map range is clamped, no exception
        Assert.All(lit, c => Assert.True(c.X < w && c.Y < h && c.X >= 0 && c.Y >= 0));
    }

    [Fact]
    public void Sparkles_stay_inside_the_cell_and_hop_over_time()
    {
        var seen = new HashSet<int>();
        for (int tick = 0; tick < 600; tick++)
        {
            for (int i = 0; i < WaterShimmer.SparklesPerCell; i++)
            {
                var r = WaterShimmer.Sparkle(7, 9, i, tick);
                Assert.True(r.X >= 0 && r.Right <= Camera.CellPx && r.Y >= 0 && r.Bottom <= Camera.CellPx);
                Assert.Equal(r, WaterShimmer.Sparkle(7, 9, i, tick));
                if (i == 0) seen.Add(r.X * 100 + r.Y);
            }
        }

        Assert.True(seen.Count > 5);
        Assert.Equal(WaterShimmer.Sparkle(7, 9, 0, 0), WaterShimmer.Sparkle(7, 9, 0, WaterShimmer.HopTicks - 1));   // steady within a hop
    }
}
