// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using static Rts.Sim.Tests.M3.Ground.GroundFixture;

namespace Rts.Sim.Tests.M3.Ground;

/// <summary>M3-INT: underground actors give no vision.</summary>
public sealed class UndergroundVisionTests
{
    [Theory]
    [InlineData(ActorLayer.Tunnel, false)]
    [InlineData(ActorLayer.Burrowed, false)]
    [InlineData(ActorLayer.Passenger, false)]
    [InlineData(ActorLayer.Diving, true)]   // ASSUMED (M3-INT): still on the surface while diving
    [InlineData(ActorLayer.Surfacing, false)]
    [InlineData(ActorLayer.Surface, true)]
    public void VehicleVision_DependsOnLayer(ActorLayer layer, bool sees)
    {
        var sim = Fresh();
        EntityId v = sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(20, 20));
        Ready(sim);
        Assert.True(sim.IsVisibleTo(1, new CellCoord(20, 22)));
        sim.Tunnels.VehicleLayer[v.Index] = layer;
        Steps(sim, 1);
        Assert.Equal(sees, sim.IsVisibleTo(1, new CellCoord(20, 22)));
        sim.Tunnels.VehicleLayer[v.Index] = ActorLayer.Surface;
        Steps(sim, 1);
        Assert.True(sim.IsVisibleTo(1, new CellCoord(20, 22)));
    }

    [Fact]
    public void InfantryUnderground_GivesNoVision()
    {
        var sim = Fresh();
        EntityId f = sim.SpawnInfantry(1, Inf("g_foot"), new CellCoord(20, 20));
        Ready(sim);
        Assert.True(sim.IsVisibleTo(1, new CellCoord(20, 21)));
        sim.Tunnels.InfantryLayer[f.Index] = ActorLayer.Burrowed;
        Steps(sim, 1);
        Assert.False(sim.IsVisibleTo(1, new CellCoord(20, 21)));
    }
}
