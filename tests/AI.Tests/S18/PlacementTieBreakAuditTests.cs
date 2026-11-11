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

    // S18 audit: pins current behaviour of AI-08; flip the assertion when fixed.
    [Fact]
    public void Audit_MirroredYards_PlaceTheFirstStructureAtTheSameOffset_NotTheMirrorImage()
    {
        var yard0 = new CellCoord(12, 30);
        var yard1 = new CellCoord(Size - 12 - 3, 30);   // mirror_x image of a 3x3 yard
        var a = FirstPower(yard0, yard1, 0);
        var b = FirstPower(yard0, yard1, 1);
        Assert.Equal((a.Dx, a.Dy), (b.Dx, b.Dy));                 // translation
        Assert.NotEqual(a.Dx, -b.Dx - b.W + 3);                    // not the mirror image (image offset = -dx - w + 3 for a 3-wide yard)
        Assert.True(a.Dx <= 0 || a.Dy <= 0, "the tie goes north or west");
    }
}
