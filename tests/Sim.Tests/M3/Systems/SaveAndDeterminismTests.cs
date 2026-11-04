// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using static Rts.Sim.Tests.M3.Systems.SystemsFixture;

namespace Rts.Sim.Tests.M3.Systems;

/// <summary>M3-06: every system together is deterministic and survives save/load mid-flight.</summary>
public sealed class SaveAndDeterminismTests
{
    /// <summary>Research in progress, an aura at work, a loaded transport, an engineer walking, a flyer, a dying crawler.</summary>
    private static (Simulation Sim, List<Command> Script) Scenario()
    {
        Simulation sim = NewSim(barrier: true);
        sim.PlaceBuilding(0, Hq, new CellCoord(4, 4));
        sim.PlaceBuilding(0, Lab, new CellCoord(8, 4));
        EntityId tower = sim.PlaceBuilding(1, Tower, new CellCoord(30, 30));
        sim.Buildings.Health[tower.Index] = 100;
        sim.Players.Cash[0] = 3000;
        EntityId aura = sim.SpawnVehicle(0, Aura, new CellCoord(5, 12));
        EntityId grunt = sim.SpawnVehicle(0, Grunt, new CellCoord(7, 12));
        EntityId truck = sim.SpawnVehicle(0, Truck, new CellCoord(10, 20));
        EntityId rifle = sim.SpawnInfantry(0, Rifle, new CellCoord(11, 20));
        EntityId eng = sim.SpawnInfantry(0, Engineer, new CellCoord(25, 30));
        EntityId air = sim.SpawnVehicle(0, Air, new CellCoord(15, 8));
        EntityId crawler = sim.SpawnVehicle(0, Crawler, new CellCoord(30, 5));
        sim.SpawnVehicle(1, Grunt, new CellCoord(31, 5));
        _ = aura;
        var script = new List<Command>
        {
            new(1, 0, 1, CommandKind.Build, default, default, default, (int)Rts.Sim.Rules.ProductionCategory.Upgrade, 0),
            new(2, 0, 2, CommandKind.Move, grunt, default, new CellCoord(18, 12), (int)ActorKind.Vehicle),
            new(2, 0, 3, CommandKind.Enter, rifle, truck, default, (int)ActorKind.Infantry | ((int)ActorKind.Vehicle << 8)),
            new(5, 0, 4, CommandKind.Move, truck, default, new CellCoord(14, 24), (int)ActorKind.Vehicle),
            new(3, 0, 5, CommandKind.Enter, eng, tower, default, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8)),
            new(3, 0, 6, CommandKind.Move, air, default, new CellCoord(35, 35), (int)ActorKind.Vehicle),
            new(60, 0, 7, CommandKind.Debug, crawler, default, default, (int)DebugOp.Kill, (int)ActorKind.Vehicle),
        };
        return (sim, script);
    }

    private static void StepTo(Simulation sim, List<Command> script, int tick)
    {
        while (sim.Tick < tick) sim.Step(script);
    }

    [Fact]
    public void SameScript_SameHash()
    {
        var (a, sa) = Scenario();
        var (b, sb) = Scenario();
        StepTo(a, sa, 300); StepTo(b, sb, 300);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Assert.True(a.HasUpgrade(0, 0));                       // research finished meanwhile
    }

    [Fact]
    public void SaveLoad_MidScenario_ContinuesIdentically()
    {
        var (a, sa) = Scenario();
        StepTo(a, sa, 70);
        Assert.Equal(1, a.PassengersIn(a.Units.Slots.IdOf(3)));   // the truck holds the rifleman
        Simulation loaded = Roundtrip(a);
        Assert.Equal(a.ComputeSubHashes(), loaded.ComputeSubHashes());
        Assert.Equal(1, loaded.PassengersIn(loaded.Units.Slots.IdOf(3)));
        for (int t = 70; t < 300; t++)
        {
            a.Step(sa); loaded.Step(sa);
            if (t % 10 == 0) Assert.Equal(a.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.Equal(a.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.True(loaded.HasUpgrade(0, 0));
    }

    [Fact]
    public void SaveLoad_KeepsAuraMultipliersAndQueue()
    {
        Simulation sim = NewSim();
        sim.PlaceBuilding(0, Hq, new CellCoord(4, 4));
        sim.PlaceBuilding(0, Lab, new CellCoord(8, 4));
        sim.Players.Cash[0] = 1000;
        sim.SpawnVehicle(0, Aura, new CellCoord(20, 20));
        EntityId g = sim.SpawnVehicle(0, Grunt, new CellCoord(22, 20));
        Run(sim, 1, BuildUpgrade(sim, 0));
        Run(sim, 25);
        Simulation loaded = Roundtrip(sim);
        Assert.Equal(1500, loaded.Effects.VehicleSpeedPermille[g.Index]);
        Assert.Equal(1, loaded.Production.QueueLength(0, Rts.Sim.Rules.ProductionCategory.Upgrade));
        Assert.Equal(sim.Production.CreditsSpent(0, Rts.Sim.Rules.ProductionCategory.Upgrade),
            loaded.Production.CreditsSpent(0, Rts.Sim.Rules.ProductionCategory.Upgrade));
    }
}
