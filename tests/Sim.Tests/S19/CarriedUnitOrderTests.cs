// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Tests.M3.Systems;
using Gf = Rts.Sim.Tests.M4.GarrisonFixture;

namespace Rts.Sim.Tests.S19;

/// <summary>S19 D-205 (M13-04): unit orders addressed to a carried unit (garrisoned soldier, vehicle passenger) are dropped by the sim;
/// orders addressed to the carrier keep working.</summary>
public sealed class CarriedUnitOrderTests
{
    private const int Inf = (int)ActorKind.Infantry;

    [Theory]
    [InlineData(CommandKind.Move)]
    [InlineData(CommandKind.Stop)]
    [InlineData(CommandKind.Guard)]
    [InlineData(CommandKind.Scatter)]
    [InlineData(CommandKind.AttackMove)]
    public void Passenger_IgnoresUnitOrders(CommandKind kind)
    {
        Simulation sim = SystemsFixture.NewSim();
        EntityId truck = sim.SpawnVehicle(0, SystemsFixture.Truck, new CellCoord(10, 10));
        EntityId rider = sim.SpawnInfantry(0, SystemsFixture.Rifle, new CellCoord(11, 10));
        SystemsFixture.Run(sim, 1, SystemsFixture.Enter(sim, 0, ActorKind.Infantry, rider, ActorKind.Vehicle, truck));
        Assert.Equal(ActorLayer.Passenger, sim.LayerOf(ActorKind.Infantry, rider.Index));
        MissionKind before = sim.Infantry.Mission[rider.Index];

        SystemsFixture.Run(sim, 1, SystemsFixture.Cmd(sim, 0, kind, rider, default, new CellCoord(25, 10), Inf));
        SystemsFixture.Run(sim, 20);

        Assert.Equal(ActorLayer.Passenger, sim.LayerOf(ActorKind.Infantry, rider.Index));
        Assert.Equal(before, sim.Infantry.Mission[rider.Index]);
        Assert.Equal(sim.Units.Position[truck.Index], sim.Infantry.Position[rider.Index]);
        Assert.Equal(1, sim.PassengersIn(truck));
    }

    [Fact]
    public void Passenger_StillUnloadsWhenTheTransportIsOrderedToDeploy()
    {
        Simulation sim = SystemsFixture.NewSim();
        EntityId truck = sim.SpawnVehicle(0, SystemsFixture.Truck, new CellCoord(10, 10));
        EntityId rider = sim.SpawnInfantry(0, SystemsFixture.Rifle, new CellCoord(11, 10));
        SystemsFixture.Run(sim, 1, SystemsFixture.Enter(sim, 0, ActorKind.Infantry, rider, ActorKind.Vehicle, truck));
        SystemsFixture.Run(sim, 1, SystemsFixture.Cmd(sim, 0, CommandKind.Move, rider, default, new CellCoord(25, 10), Inf));   // dropped
        SystemsFixture.Run(sim, 1, SystemsFixture.Cmd(sim, 0, CommandKind.Deploy, truck));                                      // carrier order
        SystemsFixture.Run(sim, 60);
        Assert.Equal(ActorLayer.Surface, sim.LayerOf(ActorKind.Infantry, rider.Index));
        Assert.Equal(0, sim.PassengersIn(truck));
    }

    [Fact]
    public void Garrisoned_IgnoresEnterToAnotherBuilding_AndStaysInside()
    {
        Simulation sim = Gf.NewSim();
        EntityId bunker = sim.PlaceBuilding(0, Gf.Bunker, new CellCoord(10, 10));
        EntityId other = sim.PlaceBuilding(0, Gf.Tenement, new CellCoord(20, 10));
        EntityId man = sim.SpawnInfantry(0, Gf.Rifle, new CellCoord(12, 10));
        Gf.Run(sim, 1, Gf.EnterCmd(sim, 0, man, bunker));
        Assert.Equal(ActorLayer.Garrisoned, sim.LayerOf(ActorKind.Infantry, man.Index));

        Gf.Run(sim, 300, Gf.EnterCmd(sim, 0, man, other));

        Assert.Equal(ActorLayer.Garrisoned, sim.LayerOf(ActorKind.Infantry, man.Index));
        Assert.Equal(1, sim.OccupantsIn(bunker));
        Assert.Equal(0, sim.OccupantsIn(other));
        Assert.Equal(MissionKind.Garrisoned, sim.Infantry.Mission[man.Index]);
    }

    [Fact]
    public void Garrisoned_UnloadOrderToTheBuilding_StillWalksTheSoldierOut()
    {
        Simulation sim = Gf.NewSim();
        EntityId bunker = sim.PlaceBuilding(0, Gf.Bunker, new CellCoord(10, 10));
        EntityId man = sim.SpawnInfantry(0, Gf.Rifle, new CellCoord(12, 10));
        Gf.Run(sim, 1, Gf.EnterCmd(sim, 0, man, bunker));
        Gf.Run(sim, 1, Gf.Cmd(sim, 0, CommandKind.Move, man, default, new CellCoord(25, 10), Inf));   // dropped
        Assert.Equal(1, sim.OccupantsIn(bunker));
        Gf.Run(sim, 1, Gf.Unload(sim, 0, bunker));
        Gf.Run(sim, 5);
        Assert.Equal(0, sim.OccupantsIn(bunker));
        Assert.Equal(ActorLayer.Surface, sim.LayerOf(ActorKind.Infantry, man.Index));
    }

    [Fact]
    public void AnOrderToASurfaceSoldier_IsStillObeyed()
    {
        Simulation sim = Gf.NewSim();
        EntityId man = sim.SpawnInfantry(0, Gf.Rifle, new CellCoord(12, 10));
        Gf.Run(sim, 200, Gf.Cmd(sim, 0, CommandKind.Move, man, default, new CellCoord(25, 10), Inf));
        Assert.True(sim.Infantry.Position[man.Index].ToCell().X > 14);
    }
}
