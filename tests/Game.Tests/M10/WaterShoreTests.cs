// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M10;

/// <summary>M10-13: shore detection and the water animation curves.</summary>
public sealed class WaterShoreTests
{
    // 5x3:  C C C C C
    //       W W S W C     (W water, S shallow, C clear, B coast)
    //       W W W W B
    private static readonly LandType[] Grid =
    {
        LandType.Clear, LandType.Clear, LandType.Clear, LandType.Clear, LandType.Clear,
        LandType.Water, LandType.Water, LandType.Shallow, LandType.Water, LandType.Clear,
        LandType.Water, LandType.Water, LandType.Water, LandType.Water, LandType.Coast,
    };

    [Fact]
    public void Land_cells_have_no_shore_mask()
    {
        Assert.Equal(0, WaterShore.EdgeMask(Grid, 5, 3, 0, 0));
        Assert.Equal(0, WaterShore.EdgeMask(Grid, 5, 3, 4, 1));
    }

    [Fact]
    public void Water_cell_marks_each_side_that_touches_land()
    {
        Assert.Equal(WaterShore.North | WaterShore.East, WaterShore.EdgeMask(Grid, 5, 3, 3, 1));
        Assert.Equal(WaterShore.North, WaterShore.EdgeMask(Grid, 5, 3, 1, 1));
        Assert.Equal(WaterShore.North, WaterShore.EdgeMask(Grid, 5, 3, 2, 1));   // shallow counts as water
        Assert.Equal(WaterShore.East, WaterShore.EdgeMask(Grid, 5, 3, 3, 2));   // coast is land
    }

    [Fact]
    public void Map_edge_is_not_a_shore_and_open_water_has_none()
    {
        Assert.Equal(0, WaterShore.EdgeMask(Grid, 5, 3, 0, 2));
        Assert.Equal(0, WaterShore.EdgeMask(Grid, 5, 3, 1, 2));
        Assert.Equal(0, WaterShore.EdgeMask(Grid, 5, 3, -1, 1));
        Assert.Equal(0, WaterShore.EdgeMask(Grid, 5, 3, 9, 9));
    }

    [Fact]
    public void Build_matches_cell_by_cell()
    {
        byte[] all = WaterShore.Build(Grid, 5, 3);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 5; x++)
                Assert.Equal(WaterShore.EdgeMask(Grid, 5, 3, x, y), all[y * 5 + x]);
    }

    [Fact]
    public void Foam_width_stays_in_range_and_actually_pulses()
    {
        int min = int.MaxValue, max = int.MinValue;
        for (int t = 0; t < WaterWaves.FoamPeriod * 2; t++)
        {
            int w = WaterWaves.FoamWidthPx(3, 4, t);
            Assert.InRange(w, WaterWaves.FoamMinPx, WaterWaves.FoamMaxPx);
            min = Math.Min(min, w); max = Math.Max(max, w);
        }

        Assert.Equal(WaterWaves.FoamMinPx, min);
        Assert.Equal(WaterWaves.FoamMaxPx, max);
    }

    [Fact]
    public void Glint_is_deterministic_inside_the_cell_and_fades_to_nothing()
    {
        bool sawZero = false, sawVisible = false;
        for (int t = 0; t < WaterWaves.GlintPeriod * 3; t++)
        {
            byte a = WaterWaves.Glint(7, 2, 0, t, out Rectangle r);
            byte b = WaterWaves.Glint(7, 2, 0, t, out Rectangle r2);
            Assert.Equal(a, b);
            Assert.Equal(r, r2);
            Assert.True(r.X >= 0 && r.Y >= 0 && r.Right <= Camera.CellPx && r.Bottom <= Camera.CellPx);
            Assert.True(a <= WaterWaves.GlintMaxAlpha);
            sawZero |= a == 0;
            sawVisible |= a > 0;
        }

        Assert.True(sawZero && sawVisible);
    }
}
