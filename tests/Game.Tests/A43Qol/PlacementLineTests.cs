// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;

namespace Rts.Game.Tests.A43Qol;

/// <summary>A43-QOL-14: the cells of a Shift + drag placement row.</summary>
public sealed class PlacementLineTests
{
    [Fact]
    public void A_horizontal_drag_lays_one_wall_per_cell()
    {
        List<CellCoord> row = PlacementLine.TopLefts(new CellCoord(4, 9), new CellCoord(8, 10), 1, 1);
        Assert.Equal(new[] { 4, 5, 6, 7, 8 }, row.Select(c => c.X));
        Assert.All(row, c => Assert.Equal(9, c.Y));
    }

    [Fact]
    public void A_vertical_backwards_drag_steps_one_footprint_at_a_time()
    {
        List<CellCoord> row = PlacementLine.TopLefts(new CellCoord(4, 20), new CellCoord(5, 13), 2, 2);
        Assert.Equal(new[] { 20, 18, 16, 14 }, row.Select(c => c.Y));
        Assert.All(row, c => Assert.Equal(4, c.X));
    }

    [Fact]
    public void A_click_without_a_drag_is_one_structure()
    {
        Assert.Single(PlacementLine.TopLefts(new CellCoord(4, 4), new CellCoord(4, 4), 1, 1));
    }

    [Fact]
    public void The_row_is_capped_by_a_sanity_limit()
    {
        Assert.Equal(PlacementLine.MaxCells, PlacementLine.TopLefts(new CellCoord(0, 0), new CellCoord(500, 0), 1, 1).Count);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 0, 1)]
    [InlineData(true, 3, 4)]
    [InlineData(false, 2, 2)]
    public void Only_as_many_as_are_ready_or_queued(bool ready, int queued, int expected)
    {
        Assert.Equal(expected, PlacementLine.Allowed(ready, queued));
    }

    [Fact]
    public void Cap_keeps_the_first_cells()
    {
        List<CellCoord> row = PlacementLine.TopLefts(new CellCoord(0, 0), new CellCoord(9, 0), 1, 1);
        Assert.Equal(new[] { 0, 1, 2 }, PlacementLine.Cap(row, 3).Select(c => c.X));
        Assert.Empty(PlacementLine.Cap(row, 0));
        Assert.Equal(10, PlacementLine.Cap(row, 99).Count);
    }
}
