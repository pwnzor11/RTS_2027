// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit;

namespace AI.Tests.S20;

/// <summary>S18-AI-NEW-3: the free lane in front of a factory exit used to be a fixed 3x4 strip south of it, whatever side of the base was open.
/// The lane now has an outlet at its far end on the side facing the map centre; mirrored starts get mirrored outlets.</summary>
public sealed class LaneSideTests
{
    private const int Size = 64;

    /// <summary>Plants structures around a yard and a factory with the shared search and returns every cell they cover.</summary>
    private static (HashSet<(int X, int Y)> Covered, CellCoord Exit) Fill(int yardX, int yardY, int factoryDx)
    {
        SimRules rules = TestWorld.Rules.Sim;
        int yard = SimRules.IndexOf(rules.Buildings, "auth_conyard", b => b.Id), power = SimRules.IndexOf(rules.Buildings, "auth_power", b => b.Id);
        int factory = SimRules.IndexOf(rules.Buildings, "auth_factory", b => b.Id);
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(Size, Size), 2, rules, TestWorld.Rules.RulesHash));
        Assert.False(sim.PlaceBuilding(0, yard, new CellCoord(yardX, yardY)).IsNone);
        Assert.False(sim.PlaceBuilding(1, yard, new CellCoord(Size - yardX - 3, Size - yardY - 3)).IsNone);
        var fpos = new CellCoord(yardX + factoryDx, yardY);
        Assert.False(sim.PlaceBuilding(0, factory, fpos).IsNone);
        BuildingType ft = rules.Buildings[factory];
        var covered = new HashSet<(int, int)>();
        var search = new PlacementSearch();
        for (int i = 0; i < 24; i++)
        {
            var k = new RecruitKnowledge();
            k.Refresh(sim.ViewFor(0), "auth_conyard");
            Assert.True(search.Find(k, power, false, out CellCoord c));
            Assert.False(sim.PlaceBuilding(0, power, c).IsNone);
            BuildingType pt = rules.Buildings[power];
            for (int y = c.Y; y < c.Y + pt.FootprintH; y++)
                for (int x = c.X; x < c.X + pt.FootprintW; x++) covered.Add((x, y));
        }
        return (covered, new CellCoord(fpos.X + ft.Exit.X, fpos.Y + ft.Exit.Y));
    }

    [Fact]
    public void WestBase_KeepsTheOutletFreeOnTheEastSide()
    {
        var (covered, exit) = Fill(10, 20, 4);
        for (int y = exit.Y + 2; y <= exit.Y + 3; y++)
            for (int x = exit.X + 2; x <= exit.X + 3; x++)
                Assert.DoesNotContain((x, y), covered);
    }

    [Fact]
    public void EastBase_KeepsTheOutletFreeOnTheWestSide()
    {
        var (covered, exit) = Fill(Size - 10 - 3, 20, -6);
        for (int y = exit.Y + 2; y <= exit.Y + 3; y++)
            for (int x = exit.X - 3; x <= exit.X - 2; x++)
                Assert.DoesNotContain((x, y), covered);
    }
}
