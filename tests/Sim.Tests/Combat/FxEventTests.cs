// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.Combat;

/// <summary>M2-19: detonations and deaths raise one-tick effects events for the renderer.</summary>
public class FxEventTests
{
    [Fact]
    public void ShellDetonation_ProducesExactlyOneEvent_ThenTheListClears()
    {
        var sim = NewSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(10, 6), 1);
        sim.Step(new[] { Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Infantry, victim) });
        int detonations = 0, ticks = 0;
        while (sim.Projectiles.Slots.Count == 0 && ticks++ < 60) { sim.Step(None); detonations += Count(sim, FxKind.Detonation); }
        Assert.Equal(1, sim.Projectiles.Slots.Count);
        Assert.Equal(0, detonations);                                  // nothing exploded while the shell flew
        while (sim.Projectiles.Slots.Count > 0 && ticks++ < 120)
        {
            sim.Step(None);
            detonations += Count(sim, FxKind.Detonation);
            if (Count(sim, FxKind.Detonation) > 0)
            {
                FxEvent e = sim.Snapshot().FxEvents!.Single(x => x.Kind == FxKind.Detonation);
                Assert.Equal(new CellCoord(10, 6), e.Pos.ToCell());
            }
        }
        Assert.Equal(1, detonations);
        sim.Step(None);
        Assert.Equal(0, sim.FxEvents.Count(e => e.Kind == FxKind.Detonation));   // cleared next tick
    }

    [Fact]
    public void Deaths_RaiseUnitAndBuildingEvents()
    {
        var sim = NewSim();
        var inf = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(5, 5), 1);
        var b = sim.PlaceBuilding(1, Bld("test_refinery"), new CellCoord(20, 20));
        sim.Infantry.Health[inf.Index] = 0;
        sim.Buildings.Health[b.Index] = 0;
        sim.Step(None);
        Assert.Single(sim.FxEvents, e => e.Kind == FxKind.UnitDestroyed);
        FxEvent be = Assert.Single(sim.FxEvents, e => e.Kind == FxKind.BuildingDestroyed);
        Assert.Equal(3, be.Size);                                      // 3x3 refinery
        Assert.Equal(2, sim.Snapshot().FxEvents!.Count);
    }

    private static int Count(Simulation sim, FxKind k) => sim.FxEvents.Count(e => e.Kind == k);

    /// <summary>M10-14b: the death event names the warhead of the lethal hit; a death with no hit (health zeroed) names none.</summary>
    [Fact]
    public void UnitDestroyed_CarriesTheLethalWarheadIndex()
    {
        var sim = NewSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(10, 6), 1);
        sim.Infantry.Health[victim.Index] = 1;
        sim.Step(new[] { Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Infantry, victim) });
        FxEvent? death = null;
        for (int t = 0; t < 200 && death is null; t++)
        {
            sim.Step(None);
            foreach (FxEvent e in sim.FxEvents) if (e.Kind == FxKind.UnitDestroyed) death = e;
        }
        Assert.NotNull(death);
        Assert.InRange(death!.Value.Warhead, 0, Loaded.Sim.Warheads.Count - 1);
    }

    [Fact]
    public void UnitDestroyed_WithoutAHit_HasNoWarhead()
    {
        var sim = NewSim();
        var inf = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(5, 5), 1);
        sim.Infantry.Health[inf.Index] = 0;
        sim.Step(None);
        Assert.Equal(FxEvent.NoWarhead, Assert.Single(sim.FxEvents, e => e.Kind == FxKind.UnitDestroyed).Warhead);
    }
}
