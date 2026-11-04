// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using static Rts.Sim.Tests.M3.Systems.SystemsFixture;

namespace Rts.Sim.Tests.M3.Systems;

/// <summary>M3-06 transports: board, ride, unload, passengers lost on death.</summary>
public sealed class TransportTests
{
    private static (Simulation Sim, EntityId Truck, EntityId[] Riders) Scene()
    {
        Simulation sim = NewSim();
        EntityId truck = sim.SpawnVehicle(0, Truck, new CellCoord(10, 10));
        var riders = new[]
        {
            sim.SpawnInfantry(0, Rifle, new CellCoord(11, 10)),
            sim.SpawnInfantry(0, Rifle, new CellCoord(9, 10)),
            sim.SpawnInfantry(0, Rifle, new CellCoord(10, 9)),
        };
        return (sim, truck, riders);
    }

    private static Command Board(Simulation sim, EntityId who, EntityId truck) =>
        Enter(sim, 0, ActorKind.Infantry, who, ActorKind.Vehicle, truck);

    [Fact]
    public void Board_FillsUpToPassengers_AndSetsLayerAndCarrier()
    {
        var (sim, truck, riders) = Scene();
        Run(sim, 1, Board(sim, riders[0], truck), Board(sim, riders[1], truck), Board(sim, riders[2], truck));
        Assert.Equal(ActorLayer.Passenger, sim.LayerOf(ActorKind.Infantry, riders[0].Index));
        Assert.Equal(ActorLayer.Passenger, sim.LayerOf(ActorKind.Infantry, riders[1].Index));
        Assert.Equal(ActorLayer.Surface, sim.LayerOf(ActorKind.Infantry, riders[2].Index));   // capacity 2
        Assert.Equal(truck, sim.Tunnels.InfantryCarrier[riders[0].Index]);
        Assert.True(sim.IsUnderground(ActorKind.Infantry, riders[0].Index));
        Assert.Equal(2, sim.PassengersIn(truck));
        Assert.True(sim.Map.InfantryAt(new CellCoord(11, 10), 0).IsNone);   // the cell was released
    }

    [Fact]
    public void Board_RejectsEnemyTransport_AndNonTransport()
    {
        var (sim, truck, riders) = Scene();
        EntityId grunt = sim.SpawnVehicle(0, Grunt, new CellCoord(12, 12));
        EntityId enemyTruck = sim.SpawnVehicle(1, Truck, new CellCoord(8, 10));
        Run(sim, 1, Board(sim, riders[0], enemyTruck), Board(sim, riders[1], grunt));
        Assert.Equal(ActorLayer.Surface, sim.LayerOf(ActorKind.Infantry, riders[0].Index));
        Assert.Equal(ActorLayer.Surface, sim.LayerOf(ActorKind.Infantry, riders[1].Index));
    }

    [Fact]
    public void Board_WalksToAFarTransportThenBoards()
    {
        var (sim, truck, _) = Scene();
        EntityId far = sim.SpawnInfantry(0, Rifle, new CellCoord(10, 20));
        Run(sim, 1, Board(sim, far, truck));
        for (int i = 0; i < 400 && sim.LayerOf(ActorKind.Infantry, far.Index) != ActorLayer.Passenger; i++) sim.Step(None);
        Assert.Equal(ActorLayer.Passenger, sim.LayerOf(ActorKind.Infantry, far.Index));
        Assert.Equal(truck, sim.Tunnels.InfantryCarrier[far.Index]);
    }

    [Fact]
    public void Passengers_RideWithTheTransport()
    {
        var (sim, truck, riders) = Scene();
        Run(sim, 1, Board(sim, riders[0], truck));
        Run(sim, 1, Move(sim, 0, ActorKind.Vehicle, truck, 25, 10));
        Run(sim, 120);
        Assert.True(sim.Units.Position[truck.Index].ToCell().X > 12);
        Assert.Equal(sim.Units.Position[truck.Index], sim.Infantry.Position[riders[0].Index]);
    }

    [Fact]
    public void Unload_PutsPassengersOnFreeCellsNextToTheTransport()
    {
        var (sim, truck, riders) = Scene();
        Run(sim, 1, Board(sim, riders[0], truck), Board(sim, riders[1], truck));
        Run(sim, 1, Cmd(sim, 0, CommandKind.Deploy, truck));
        var cells = new List<CellCoord>();
        for (int k = 0; k < 2; k++)
        {
            int i = riders[k].Index;
            Assert.Equal(ActorLayer.Surface, sim.LayerOf(ActorKind.Infantry, i));
            Assert.True(sim.Tunnels.InfantryCarrier[i].IsNone);
            CellCoord c = sim.Infantry.Position[i].ToCell();
            Assert.Equal(1, Math.Max(Math.Abs(c.X - 10), Math.Abs(c.Y - 10)));
            Assert.Equal(riders[k], sim.Map.InfantryAt(c, sim.Infantry.SubSlot[i]));
            cells.Add(c);
        }
        Assert.NotEqual(cells[0], cells[1]);
        Assert.Equal(0, sim.PassengersIn(truck));
    }

    [Fact]
    public void Unload_AfterBoardingEveryoneAndStillOrderable()
    {
        var (sim, truck, riders) = Scene();
        Run(sim, 1, Board(sim, riders[0], truck));
        Run(sim, 1, Cmd(sim, 0, CommandKind.Deploy, truck));
        Run(sim, 1, Move(sim, 0, ActorKind.Infantry, riders[0], 15, 15));
        Run(sim, 200);
        Assert.Equal(new CellCoord(15, 15), sim.Infantry.Position[riders[0].Index].ToCell());
    }

    [Fact]
    public void TransportDying_LosesThePassengers()
    {
        var (sim, truck, riders) = Scene();
        Run(sim, 1, Board(sim, riders[0], truck), Board(sim, riders[1], truck));
        Run(sim, 1, Kill(sim, ActorKind.Vehicle, truck));
        Run(sim, 2);
        Assert.False(sim.Units.Slots.IsAlive(truck));
        Assert.False(sim.Infantry.Slots.IsAlive(riders[0]));
        Assert.False(sim.Infantry.Slots.IsAlive(riders[1]));
        Assert.True(sim.Infantry.Slots.IsAlive(riders[2]));          // the one left outside is fine
    }
}
