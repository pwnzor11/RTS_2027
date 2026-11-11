// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Economy;
using Xunit.Abstractions;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit (SP-04): a harvester picks fields by distance with no reachability test. Scene (32x32): refinery at (4,4), harvester
/// at (8,8), a NEAR 3x3 field at (14,8) that may be walled in by a ring of rock, and a FAR open 3x3 field at (22,20).</summary>
public sealed class HarvesterUnreachableFieldAuditTests
{
    private readonly ITestOutputHelper _out;

    public HarvesterUnreachableFieldAuditTests(ITestOutputHelper output) => _out = output;

    private static readonly CellCoord Near = new(14, 8), FarField = new(22, 20);

    private (int Harvested, int FarLeft, int NearLeft, CellCoord At) Play(bool nearField, bool sealNear, int ticks = 4000)
    {
        const int w = 32, n = w * w;
        var overlay = new OverlayKind[n];
        var dens = new byte[n];
        var land = new LandType[n];
        void Field(CellCoord tl)
        {
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++) { overlay[(tl.Y + y) * w + tl.X + x] = OverlayKind.Ashfall; dens[(tl.Y + y) * w + tl.X + x] = 12; }
        }
        if (nearField) Field(Near);
        Field(FarField);
        if (sealNear)
            for (int y = Near.Y - 1; y <= Near.Y + 3; y++)
                for (int x = Near.X - 1; x <= Near.X + 3; x++)
                    if (x == Near.X - 1 || x == Near.X + 3 || y == Near.Y - 1 || y == Near.Y + 3) land[y * w + x] = LandType.Rock;
        var map = new MapData("eco", "rust_belt", w, w, new ushort[n], land, new byte[n], overlay, dens, new[] { new CellCoord(2, 2), new CellCoord(29, 29) });
        Simulation sim = EconomyFixture.NewSim(map);
        sim.PlaceBuilding(0, Bld("test_refinery"), EconomyFixture.Refinery);
        EntityId h = sim.SpawnVehicle(0, EconomyFixture.Harvester, new CellCoord(8, 8));
        int Sum(CellCoord tl) { int s = 0; for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) s += sim.Map.DensityAt(new CellCoord(tl.X + x, tl.Y + y)); return s; }
        Run(sim, ticks);
        var r = (sim.StatsOf(0).CreditsHarvested, Sum(FarField), Sum(Near), sim.Units.Position[h.Index].ToCell());
        _out.WriteLine($"near {nearField} sealed {sealNear}: {r}");
        return r;
    }

    [Fact]
    public void Control_BothFieldsOpen_TheHarvesterEarns()
    {
        var r = Play(nearField: true, sealNear: false);
        Assert.True(r.Harvested > 0);
    }

    [Fact]
    public void Control_OnlyTheFarField_TheHarvesterFindsIt()
    {
        var r = Play(nearField: false, sealNear: true);
        Assert.True(r.Harvested > 0);
        Assert.True(r.FarLeft < 108);
    }

    // S18 audit: pins current behaviour of SP-04; flip the assertion when fixed.
    // Measured at S18: after 4000 ticks (200 s) nothing harvested, the open far field untouched, the harvester parked by the rock ring.
    [Fact]
    public void Audit_WalledInNearField_TrapsTheHarvester_WhileAnOpenFieldExists()
    {
        var r = Play(nearField: true, sealNear: true);
        Assert.Equal(0, r.Harvested);
        Assert.Equal(108, r.FarLeft);    // 9 cells x 12: never touched
        Assert.Equal(108, r.NearLeft);
        Assert.True(Math.Max(Math.Abs(r.At.X - 15), Math.Abs(r.At.Y - 9)) <= 4, $"harvester should be loitering by the sealed field, is at {r.At}");
    }

    [Fact(Skip = "S18 SP-04: awaiting owner decision")]
    public void Desired_WalledInNearField_IsSkippedForTheReachableOne()
    {
        var r = Play(nearField: true, sealNear: true);
        Assert.True(r.Harvested > 0);
        Assert.True(r.FarLeft < 108);
    }
}
