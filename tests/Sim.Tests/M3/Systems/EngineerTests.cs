// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using static Rts.Sim.Tests.M3.Systems.SystemsFixture;

namespace Rts.Sim.Tests.M3.Systems;

/// <summary>M3-06 engineers: capture enemy buildings below half health, repair friendly ones, never wasted.</summary>
public sealed class EngineerTests
{
    private static Command Go(Simulation sim, byte player, EntityId eng, EntityId building) =>
        Enter(sim, player, ActorKind.Infantry, eng, ActorKind.Building, building);

    [Fact]
    public void Engineer_CapturesADamagedEnemyBuilding_AndIsConsumed()
    {
        Simulation sim = NewSim();
        EntityId tower = sim.PlaceBuilding(1, Tower, new CellCoord(12, 10));
        EntityId eng = sim.SpawnInfantry(0, Engineer, new CellCoord(5, 10));
        sim.Buildings.Health[tower.Index] = 150;   // of 400
        Run(sim, 1, Go(sim, 0, eng, tower));
        for (int i = 0; i < 400 && sim.Buildings.Player[tower.Index] != 0; i++) sim.Step(None);
        Assert.Equal((byte)0, sim.Buildings.Player[tower.Index]);
        Assert.False(sim.Infantry.Slots.IsAlive(eng));
        Assert.Equal(150, sim.Buildings.Health[tower.Index]);   // capture does not heal
        Assert.True(sim.Players.Defeated[1]);   // its only building changed hands
    }

    [Fact]
    public void Engineer_CannotCaptureAHealthyEnemyBuilding_AndSurvives()
    {
        Simulation sim = NewSim();
        EntityId tower = sim.PlaceBuilding(1, Tower, new CellCoord(12, 10));
        EntityId eng = sim.SpawnInfantry(0, Engineer, new CellCoord(11, 10));
        Run(sim, 1, Go(sim, 0, eng, tower));
        Run(sim, 60);
        Assert.Equal((byte)1, sim.Buildings.Player[tower.Index]);
        Assert.True(sim.Infantry.Slots.IsAlive(eng));
    }

    [Fact]
    public void Engineer_RepairsADamagedOwnBuildingToFull()
    {
        Simulation sim = NewSim();
        EntityId hq = sim.PlaceBuilding(0, Hq, new CellCoord(12, 10));
        EntityId eng = sim.SpawnInfantry(0, Engineer, new CellCoord(8, 10));
        sim.Buildings.Health[hq.Index] = 100;
        Run(sim, 1, Go(sim, 0, eng, hq));
        for (int i = 0; i < 400 && sim.Infantry.Slots.IsAlive(eng); i++) sim.Step(None);
        Assert.Equal(400, sim.Buildings.Health[hq.Index]);
        Assert.False(sim.Infantry.Slots.IsAlive(eng));
        Assert.Equal((byte)0, sim.Buildings.Player[hq.Index]);
    }

    [Fact]
    public void Engineer_IsNotWastedOnAnUndamagedOwnBuilding_AndOthersCannotEnter()
    {
        Simulation sim = NewSim();
        EntityId hq = sim.PlaceBuilding(0, Hq, new CellCoord(12, 10));
        EntityId eng = sim.SpawnInfantry(0, Engineer, new CellCoord(11, 10));
        EntityId rifle = sim.SpawnInfantry(0, Rifle, new CellCoord(11, 12));
        sim.Buildings.Health[hq.Index] = 100;
        EntityId enemyTower = sim.PlaceBuilding(1, Tower, new CellCoord(20, 20));
        sim.Buildings.Health[enemyTower.Index] = 10;
        Run(sim, 1, Go(sim, 0, rifle, enemyTower));
        Run(sim, 80);
        Assert.Equal((byte)1, sim.Buildings.Player[enemyTower.Index]);
        Assert.True(sim.Infantry.Slots.IsAlive(rifle));
        sim.Buildings.Health[hq.Index] = 400;
        Run(sim, 1, Go(sim, 0, eng, hq));
        Run(sim, 40);
        Assert.True(sim.Infantry.Slots.IsAlive(eng));
    }

    [Fact]
    public void Capture_ChangesWhoCanBeHit_AndOwnerOfThePlayersBase()
    {
        Simulation sim = NewSim();
        EntityId tower = sim.PlaceBuilding(1, Tower, new CellCoord(12, 10));
        sim.PlaceBuilding(1, Hq, new CellCoord(20, 20));
        EntityId eng = sim.SpawnInfantry(0, Engineer, new CellCoord(11, 10));
        sim.Buildings.Health[tower.Index] = 100;
        Run(sim, 1, Go(sim, 0, eng, tower));
        Run(sim, 60);
        Assert.Equal((byte)0, sim.Buildings.Player[tower.Index]);
        Assert.True(sim.RelationOf(0, sim.Buildings.Player[tower.Index]) == Relation.Self);
        Assert.False(sim.Players.Defeated[1]);   // player 1 still has its hq
    }
}
