// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests.S18;

/// <summary>S18 audit AI-08 (3): PlacementSearch sorts equally distant candidates by (y, x), so on a flat map two yards that are mirror
/// images of each other put their first structure at the same offset (a translation), north-west of the yard, not at mirror images.</summary>
public class PlacementTieBreakAuditTests(ITestOutputHelper output)
{
    private const int Size = 64;

    private (int Dx, int Dy, int W) FirstPower(CellCoord yard0, CellCoord yard1, byte p)
    {
        SimRules rules = TestWorld.Rules.Sim;
        int yard = SimRules.IndexOf(rules.Buildings, "auth_conyard", b => b.Id), power = SimRules.IndexOf(rules.Buildings, "auth_power", b => b.Id);
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(Size, Size), 2, rules, TestWorld.Rules.RulesHash));
        Assert.False(sim.PlaceBuilding(0, yard, yard0).IsNone);
        Assert.False(sim.PlaceBuilding(1, yard, yard1).IsNone);
        var k = new RecruitKnowledge();
        k.Refresh(sim.ViewFor(p), "auth_conyard");
        Assert.True(new PlacementSearch().Find(k, power, false, out CellCoord cell));
        CellCoord y = p == 0 ? yard0 : yard1;
        output.WriteLine($"p{p} yard {y}: first power plant at {cell} (offset {cell.X - y.X},{cell.Y - y.Y})");
        return (cell.X - y.X, cell.Y - y.Y, rules.Buildings[power].FootprintW);
    }

    // M13-10 (D-205 item 11, AI-08): ties break toward the map centre in a mirrored order, so mirrored yards place at mirrored offsets.
    [Fact]
    public void MirroredYards_PlaceTheFirstStructureAtTheMirrorImage_MirrorX()
    {
        var yard0 = new CellCoord(12, 30);
        var yard1 = new CellCoord(Size - 12 - 3, 30);   // mirror_x image of a 3x3 yard
        var a = FirstPower(yard0, yard1, 0);
        var b = FirstPower(yard0, yard1, 1);
        Assert.Equal(a.Dy, b.Dy);
        Assert.Equal(-a.Dx - a.W + 3, b.Dx);
    }

    [Fact]
    public void MirroredYards_PlaceTheFirstStructureAtTheHalfTurnImage()
    {
        var yard0 = new CellCoord(12, 20);
        var yard1 = new CellCoord(Size - 12 - 3, Size - 20 - 3);
        var a = FirstPower(yard0, yard1, 0);
        var b = FirstPower(yard0, yard1, 1);
        Assert.Equal(-a.Dx - a.W + 3, b.Dx);
        Assert.Equal(-a.Dy - a.W + 3, b.Dy);   // power plant footprint is square
    }
}
