// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.World;
using Rts.Sim.Tests.Rules;

namespace Rts.Sim.Tests.M7;

/// <summary>D-156 (1): a water building with no mask is a floating dock: every footprint cell on water and at least one footprint cell
/// orthogonally next to land. A square island (Clear, x and y 16..31) in a 48x48 sea lets every coast be tried; player 1 (Authority) has no beachhead.</summary>
public sealed class FloatingDockTests
{
    private static (Simulation Sim, int Dock) NewWorld()
    {
        string buildings = TempRules.Real("buildings.toml") + """

            [test_xfloat]
            footprint_w = 2
            footprint_h = 2
            max_health = 300
            armor = "structure"
            sight = 3
            primary_weapon = ""
            cost = 100
            power = 0
            prerequisites = []
            provides = []
            water_building = true
            """;
        using var temp = new TempRules(("buildings.toml", buildings));
        RuleSet r = RulesLoader.Load(temp.Dir);
        const int w = 48, h = 48;
        var land = new LandType[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                land[y * w + x] = x is >= 16 and <= 31 && y is >= 16 and <= 31 ? LandType.Clear : x == 40 ? LandType.Shallow : LandType.Water;
        var map = new MapData("island", "rust_belt", w, h, new ushort[w * h], land, new byte[w * h], new OverlayKind[w * h], new byte[w * h],
            new[] { new CellCoord(20, 20), new CellCoord(22, 22) });
        var sim = new Simulation(new SimSetup(1, map, 2, r.Sim, r.RulesHash) { Factions = new[] { Faction.Expedition, Faction.Authority } });
        return (sim, SimRules.IndexOf(r.Sim.Buildings, "test_xfloat", b => b.Id));
    }

    [Theory]
    [InlineData(20, 14)]   // north coast: row 15 is sea, row 16 is land
    [InlineData(32, 20)]   // east coast
    [InlineData(14, 20)]   // west coast
    [InlineData(20, 32)]   // south coast
    public void AllWaterTouchingTheShore_IsOk(int x, int y)
    {
        var (sim, dock) = NewWorld();
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(1, dock, new CellCoord(x, y)));
        Assert.False(sim.PlaceBuilding(1, dock, new CellCoord(x, y)).IsNone);
    }

    [Theory]
    [InlineData(20, 10)]   // open sea north of the island
    [InlineData(20, 12)]   // one cell of sea between dock and shore
    [InlineData(32, 14)]   // only the diagonal corner meets land
    [InlineData(0, 0)]     // map corner, all sea
    [InlineData(38, 20)]   // Shallow around, still no land next to it
    public void AllWaterNotTouchingTheShore_IsBlocked(int x, int y)
    {
        var (sim, dock) = NewWorld();
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(1, dock, new CellCoord(x, y)));
        Assert.True(sim.PlaceBuilding(1, dock, new CellCoord(x, y)).IsNone);
    }

    [Theory]
    [InlineData(20, 15)]   // one row of land in the footprint
    [InlineData(15, 20)]
    [InlineData(31, 31)]
    [InlineData(24, 24)]   // fully inland
    public void AnyLandCellInTheFootprint_IsBlocked(int x, int y)
    {
        var (sim, dock) = NewWorld();
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(1, dock, new CellCoord(x, y)));
    }

    [Fact]
    public void ShallowWaterCountsAsWater_AndLandIsAnythingElse()
    {
        var (sim, dock) = NewWorld();
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(1, dock, new CellCoord(39, 20)));   // Shallow at x = 40 only: no land beside it
    }
}
