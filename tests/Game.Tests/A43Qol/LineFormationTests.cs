// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Tests.A43Qol;

/// <summary>A43-QOL-16: the maths of a right-drag line move.</summary>
public sealed class LineFormationTests
{
    [Fact]
    public void A_drag_shorter_than_two_cells_is_not_a_line()
    {
        Assert.False(LineFormation.IsLine(new CellCoord(5, 5), new CellCoord(6, 6)));
        Assert.True(LineFormation.IsLine(new CellCoord(5, 5), new CellCoord(7, 5)));
        Assert.True(LineFormation.IsLine(new CellCoord(5, 5), new CellCoord(5, 2)));
    }

    [Fact]
    public void Slots_include_both_ends_and_are_evenly_spread()
    {
        List<CellCoord> slots = LineFormation.Slots(new CellCoord(10, 10), new CellCoord(18, 10), 5);
        Assert.Equal(new[] { 10, 12, 14, 16, 18 }, slots.Select(s => s.X));
        Assert.All(slots, s => Assert.Equal(10, s.Y));
    }

    [Fact]
    public void Slots_follow_a_diagonal_and_a_backwards_drag()
    {
        List<CellCoord> slots = LineFormation.Slots(new CellCoord(8, 8), new CellCoord(2, 2), 3);
        Assert.Equal(new[] { new CellCoord(8, 8), new CellCoord(5, 5), new CellCoord(2, 2) }, slots);
    }

    [Fact]
    public void One_unit_takes_the_middle_and_none_takes_nothing()
    {
        Assert.Equal(new CellCoord(6, 4), Assert.Single(LineFormation.Slots(new CellCoord(2, 4), new CellCoord(10, 4), 1)));
        Assert.Empty(LineFormation.Slots(new CellCoord(2, 4), new CellCoord(10, 4), 0));
    }

    [Fact]
    public void Units_take_slots_in_the_order_they_stand_along_the_line()
    {
        // drag west to east; the unit that stands furthest west takes the west end whatever the input order
        var west = new UnitKey(ActorKind.Vehicle, new EntityId(3, 1));
        var mid = new UnitKey(ActorKind.Infantry, new EntityId(1, 1));
        var east = new UnitKey(ActorKind.Vehicle, new EntityId(2, 1));
        var units = new List<(UnitKey, LeptonCoord)>
        {
            (east, LeptonCoord.CenterOf(new CellCoord(30, 7))),
            (west, LeptonCoord.CenterOf(new CellCoord(2, 9))),
            (mid, LeptonCoord.CenterOf(new CellCoord(15, 3))),
        };
        var result = LineFormation.Assign(units, new CellCoord(10, 20), new CellCoord(20, 20));
        Assert.Equal(new[] { (west, new CellCoord(10, 20)), (mid, new CellCoord(15, 20)), (east, new CellCoord(20, 20)) }, result);
        units.Reverse();
        Assert.Equal(result, LineFormation.Assign(units, new CellCoord(10, 20), new CellCoord(20, 20)));
    }
}
