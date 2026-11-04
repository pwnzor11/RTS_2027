// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.World;
using static Rts.Sim.Tests.M3.Systems.SystemsFixture;

namespace Rts.Sim.Tests.M3.Systems;

/// <summary>M3-06 air movement: terrain and ground occupancy are ignored.</summary>
public sealed class AirTests
{
    [Fact]
    public void AirUnit_FliesOverWaterAndCliff_WhereAGroundUnitCannot()
    {
        Simulation sim = NewSim(barrier: true);
        EntityId air = sim.SpawnVehicle(0, Air, new CellCoord(15, 10));
        EntityId ground = sim.SpawnVehicle(0, Grunt, new CellCoord(15, 20));
        Run(sim, 1, Move(sim, 0, ActorKind.Vehicle, air, 28, 10), Move(sim, 0, ActorKind.Vehicle, ground, 28, 20));
        Run(sim, 200);
        Assert.Equal(new CellCoord(28, 10), sim.Units.Position[air.Index].ToCell());
        Assert.True(sim.Units.Position[ground.Index].ToCell().X < 20);
    }

    [Fact]
    public void AirUnit_IgnoresBuildingsAndGroundUnits_AndBlocksNobody()
    {
        Simulation sim = NewSim();
        sim.PlaceBuilding(1, Hq, new CellCoord(12, 10));
        sim.SpawnVehicle(1, Grunt, new CellCoord(14, 10));
        EntityId air = sim.SpawnVehicle(0, Air, new CellCoord(8, 10));
        Run(sim, 1, Move(sim, 0, ActorKind.Vehicle, air, 18, 10));
        Run(sim, 140);
        Assert.Equal(new CellCoord(18, 10), sim.Units.Position[air.Index].ToCell());
        Assert.True(sim.Map.VehicleAt(new CellCoord(18, 10)).IsNone);   // holds no cell
        Run(sim, 1, Cmd(sim, 0, CommandKind.Debug, default, default, new CellCoord(18, 10), (int)DebugOp.SpawnVehicle, Grunt));
        Assert.False(sim.Map.VehicleAt(new CellCoord(18, 10)).IsNone);   // a ground unit can take the cell under the flyer
    }

    [Fact]
    public void AirUnit_FliesStraight_AtItsSpeed()
    {
        Simulation sim = NewSim();
        EntityId air = sim.SpawnVehicle(0, Air, new CellCoord(5, 5));
        Run(sim, 1, Move(sim, 0, ActorKind.Vehicle, air, 25, 5));
        int x0 = sim.Units.Position[air.Index].X;
        Run(sim, 10);
        Assert.Equal(x0 + 10 * 20, sim.Units.Position[air.Index].X);   // 20 leptons per tick
    }

    [Fact]
    public void CanPlace_IgnoresAirUnitsOverTheFootprint_ButNotGroundUnits()
    {
        Simulation sim = NewSim();
        var cell = new CellCoord(12, 10);
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Hq, cell));
        sim.SpawnVehicle(1, Air, new CellCoord(12, 10));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Hq, cell));
        sim.SpawnVehicle(1, Grunt, new CellCoord(12, 10));
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(0, Hq, cell));
    }
}
