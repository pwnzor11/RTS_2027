// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Superweapon;
using Rts.Sim.Core;

namespace Rts.Game.Tests.A43;

/// <summary>A43 GAMEUI-03: the legal area of an entrance-range superweapon, the refusal text and the armed range.</summary>
public sealed class SuperweaponRangeTests
{
    // An own entrance building at cells 10..12 x 10..12 (3 x 3), range 8 as Directorate Sunrise has.
    private static readonly EntranceFootprint Origin = new(10, 10, 3, 3);

    [Theory]
    [InlineData(10, 10, 0)]   // inside the footprint
    [InlineData(13, 10, 1)]   // one cell to the east
    [InlineData(8, 11, 2)]    // two cells to the west
    [InlineData(14, 14, 2)]   // diagonal: the larger of the two steps
    public void Distance_IsTheChebyshevDistanceFromTheFootprint(int x, int y, int expected) =>
        Assert.Equal(expected, SuperweaponRange.Distance(Origin, new CellCoord(x, y)));

    [Fact]
    public void Covers_StopsAtTheRange()
    {
        var origins = new[] { Origin };
        Assert.True(SuperweaponRange.Covers(origins, 8, new CellCoord(20, 10)));    // 8 cells east of the footprint edge
        Assert.False(SuperweaponRange.Covers(origins, 8, new CellCoord(21, 10)));   // 9 cells
    }

    [Fact]
    public void Covers_AnyOriginCounts()
    {
        var origins = new[] { Origin, new EntranceFootprint(40, 40, 2, 2) };
        Assert.True(SuperweaponRange.Covers(origins, 8, new CellCoord(38, 41)));
        Assert.False(SuperweaponRange.Covers(origins, 8, new CellCoord(25, 25)));
    }

    [Fact]
    public void Covers_ZeroRangeMeansAnywhere()
    {
        Assert.True(SuperweaponRange.Covers(Array.Empty<EntranceFootprint>(), 0, new CellCoord(500, 500)));
    }

    [Fact]
    public void OriginsOf_KeepsOnlyOwnLiveEntrances()
    {
        const int entranceType = 4;
        var buildings = new[]
        {
            new BuildingView(new EntityId(1, 0), 0, entranceType, new CellCoord(10, 10), 3, 3, 100, false),   // kept
            new BuildingView(new EntityId(2, 0), 1, entranceType, new CellCoord(30, 30), 3, 3, 100, false),  // another player
            new BuildingView(new EntityId(3, 0), 0, entranceType, new CellCoord(50, 50), 3, 3, 100, true),   // ghost
            new BuildingView(new EntityId(4, 0), 0, entranceType, new CellCoord(60, 60), 3, 3, 0, false),    // no health
            new BuildingView(new EntityId(5, 0), 0, 9, new CellCoord(70, 70), 3, 3, 100, false),             // not an entrance
        };
        IReadOnlyList<EntranceFootprint> origins = SuperweaponRange.OriginsOf(buildings, 0, t => t == entranceType);
        EntranceFootprint only = Assert.Single(origins);
        Assert.Equal(new EntranceFootprint(10, 10, 3, 3), only);
    }

    [Fact]
    public void Refusals_NameTheirReason()
    {
        Assert.Equal("Out of range", SuperweaponText.Placeholder(SuperweaponText.OutOfRangeKey, ""));
        Assert.Equal("Invalid target", SuperweaponText.Placeholder(SuperweaponText.CannotFireKey, ""));
    }

    [Fact]
    public void ArmedRange_IsTheRangeOfTheArmedButton()
    {
        var id = new EntityId(3, 1);
        var hud = new PlayerHud(0, 0, 0, 0, 0, Array.Empty<QueueView>(), Array.Empty<(Rts.Sim.Rules.ProductionCategory, int)>(), false)
        {
            Superweapons = new[] { new SuperweaponView(id, 7, "dir_sunrise", 200, 200, true, false, 8) },
        };
        var controller = new SuperweaponController();
        controller.Update(hud, 0);
        Assert.Equal(0, controller.ArmedRange);   // nothing armed yet
        controller.ClickButton(0);
        Assert.Equal(8, controller.ArmedRange);
        controller.Cancel();
        Assert.Equal(0, controller.ArmedRange);
    }
}
