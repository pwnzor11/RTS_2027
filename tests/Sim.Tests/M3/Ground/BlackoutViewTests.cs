// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using static Rts.Sim.Tests.M3.Ground.GroundFixture;

namespace Rts.Sim.Tests.M3.Ground;

/// <summary>M3-INT: Blackout is honoured by the per-viewer vision grids (PlayerView, SimSnapshot).</summary>
public sealed class BlackoutViewTests
{
    [Fact]
    public void BlackoutCells_AreNotVisibleInViewerGrids_UnlessSeenThrough()
    {
        var sim = Fresh();
        sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(10, 10));
        sim.SpawnVehicle(0, Veh("g_plain_veh"), new CellCoord(14, 10));
        Ready(sim);
        var cell = new CellCoord(14, 10);
        int idx = cell.Y * 32 + cell.X;
        Assert.Equal(2, sim.Snapshot(1).Vision[idx]);
        Assert.Equal(2, sim.ViewFor(1).VisionAt(cell));
        sim.Detonate(0, 1, Wh("g_black"), LeptonCoord.CenterOf(cell), default, false);
        Assert.Equal(1, sim.Snapshot(1).Vision[idx]);   // fogged: explored but not visible
        Assert.Equal(1, sim.ViewFor(1).VisionAt(cell));
        Assert.False(sim.ViewFor(1).IsVisible(cell));
        Assert.Equal(2, sim.Snapshot(1).Vision[10 * 32 + 9]);   // outside the cloud
        Assert.Equal(2, sim.Snapshot(0).Vision[idx]);            // the other team's own unit stands on it
    }

    [Fact]
    public void BlackoutCells_VisibleWhenSearchlightCoversThem()
    {
        var sim = Fresh();
        sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(10, 10));
        sim.PlaceBuilding(1, Bld("g_search"), new CellCoord(10, 14));
        Ready(sim);
        var cell = new CellCoord(14, 10);
        sim.Detonate(0, 1, Wh("g_black"), LeptonCoord.CenterOf(cell), default, false);
        Assert.Equal(2, sim.Snapshot(1).Vision[cell.Y * 32 + cell.X]);
        Assert.Equal(2, sim.ViewFor(1).VisionAt(cell));
    }
}
