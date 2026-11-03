// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.Combat;

/// <summary>M2-19: a splash-0 projectile hurts ONE eligible actor in the impact cell, not the whole stack.</summary>
public class SingleHitProjectileTests
{
    private static (Simulation Sim, EntityId Tank, EntityId[] Squad) Setup()
    {
        var sim = NewSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var squad = new EntityId[5];
        for (int s = 0; s < 5; s++) squad[s] = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(10, 6), s);
        return (sim, tank, squad);
    }

    [Fact]
    public void Shell_HurtsOnlyTheTargetOfAFiveManStack()
    {
        var (sim, tank, squad) = Setup();
        EntityId target = squad[3];
        Run(sim, 40, Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Infantry, target));
        Assert.Equal(40, sim.Infantry.Health[target.Index]);          // ap vs unarmored: 10
        foreach (EntityId e in squad)
            if (e != target) Assert.Equal(50, sim.Infantry.Health[e.Index]);
    }

    [Fact]
    public void Shell_WhoseTargetIsGone_HurtsTheFirstEligibleBySlotOrder()
    {
        var (sim, tank, squad) = Setup();
        EntityId target = squad[3];
        sim.Step(new[] { Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Infantry, target) });
        for (int t = 0; t < 40 && sim.Projectiles.Slots.Count == 0; t++) sim.Step(None);
        Assert.Equal(1, sim.Projectiles.Slots.Count);
        sim.Infantry.Health[target.Index] = 0;                         // dies before the shell lands
        for (int t = 0; t < 40 && sim.Projectiles.Slots.Count > 0; t++) sim.Step(None);
        int hurt = 0;
        foreach (EntityId e in squad)
            if (e != target && sim.Infantry.Health[e.Index] < 50) { hurt++; Assert.Equal(squad[0], e); }
        Assert.Equal(1, hurt);
    }
}
