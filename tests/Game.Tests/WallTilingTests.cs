// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Sim.Core;

namespace Rts.Game.Tests;

/// <summary>Wall neighbour mask to sprite state (D-072). No GPU needed.</summary>
public sealed class WallTilingTests
{
    [Theory]
    [InlineData(0, "post")]
    [InlineData(1, "end_n")]
    [InlineData(2, "end_e")]
    [InlineData(3, "corner_ne")]
    [InlineData(4, "end_s")]
    [InlineData(5, "v")]
    [InlineData(6, "corner_se")]
    [InlineData(7, "t_e")]
    [InlineData(8, "end_w")]
    [InlineData(9, "corner_nw")]
    [InlineData(10, "h")]
    [InlineData(11, "t_n")]
    [InlineData(12, "corner_sw")]
    [InlineData(13, "t_w")]
    [InlineData(14, "t_s")]
    [InlineData(15, "cross")]
    public void Every_mask_maps_to_its_state(int mask, string state)
    {
        Assert.Equal(state, WallTiling.StateFor(mask, damaged: false));
        Assert.Equal(state + "_damaged", WallTiling.StateFor(mask, damaged: true));
    }

    [Fact]
    public void All_sixteen_masks_give_distinct_states()
    {
        var seen = new HashSet<string>();
        for (int m = 0; m < 16; m++) Assert.True(seen.Add(WallTiling.StateFor(m, false)));
    }

    [Fact]
    public void Mask_reads_the_four_neighbours()
    {
        var walls = new HashSet<CellCoord> { new(5, 5), new(5, 4), new(6, 5), new(5, 6), new(4, 5), new(9, 9) };
        Assert.Equal(15, WallTiling.MaskOf(new CellCoord(5, 5), walls.Contains));
        Assert.Equal(WallTiling.South, WallTiling.MaskOf(new CellCoord(5, 4), walls.Contains));
        Assert.Equal(0, WallTiling.MaskOf(new CellCoord(9, 9), walls.Contains));
    }

    [Fact]
    public void A_two_by_two_square_uses_the_four_corners()
    {
        var snapshotWalls = new List<BuildingView>();
        ushort id = 1;
        foreach ((int x, int y) in new[] { (3, 3), (4, 3), (3, 4), (4, 4) })
        {
            snapshotWalls.Add(new BuildingView(new EntityId(id++, 0), 0, 7, new CellCoord(x, y), 1, 1, 200, false));
        }

        var tiler = new WallTiler();
        tiler.Observe(10, snapshotWalls, t => t == 7);
        string State(int i) => WallTiling.StateFor(tiler.Mask(snapshotWalls[i]), false);

        Assert.Equal("corner_se", State(0));   // top-left block continues east and south
        Assert.Equal("corner_sw", State(1));
        Assert.Equal("corner_ne", State(2));
        Assert.Equal("corner_nw", State(3));
    }

    [Fact]
    public void Only_own_walls_count_and_a_removed_block_updates_the_neighbours()
    {
        var a = new BuildingView(new EntityId(1, 0), 0, 7, new CellCoord(2, 2), 1, 1, 200, false);
        var b = new BuildingView(new EntityId(2, 0), 0, 7, new CellCoord(3, 2), 1, 1, 200, false);
        var enemy = new BuildingView(new EntityId(3, 0), 1, 7, new CellCoord(1, 2), 1, 1, 200, false);
        var tiler = new WallTiler();

        tiler.Observe(1, new[] { a, b, enemy }, t => t == 7);
        Assert.Equal(WallTiling.East, tiler.Mask(a));

        tiler.Observe(2, new[] { a, enemy }, t => t == 7);
        Assert.Equal(0, tiler.Mask(a));
    }
}
