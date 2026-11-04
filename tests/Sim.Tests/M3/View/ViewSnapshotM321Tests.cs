// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.M3.Burrow;
using Rts.Sim.Tests.M3.Hardening;
using Rts.Sim.Tests.M3.Superweapon;

namespace Rts.Sim.Tests.M3.View;

/// <summary>M3-21: turret facing, the dive flag, gate open state and superweapon charge in the per-player snapshot (fog rules included).</summary>
public sealed class ViewSnapshotM321Tests
{
    private static readonly Command[] None = Array.Empty<Command>();

    private static Simulation TurretSim(out EntityId own, out EntityId far, out EntityId hull)
    {
        VehicleType tank = ScriptedGame.Vehicle("tank", 40, 100) with { HasTurret = true, TurretTurnRate = 8 };
        VehicleType plain = ScriptedGame.Vehicle("plain", 40, 100);
        var setup = new SimSetup(3, MapData.CreateFlat(64, 64), 2, ScriptedGame.RulesWith(new[] { plain, tank }, Array.Empty<InfantryType>()), 1)
        { AllowDebug = true };
        var sim = new Simulation(setup);
        own = sim.SpawnVehicle(0, 1, new CellCoord(5, 5));
        far = sim.SpawnVehicle(1, 1, new CellCoord(55, 55));
        hull = sim.SpawnVehicle(0, 0, new CellCoord(8, 5));
        sim.Step(None);
        return sim;
    }

    [Fact]
    public void TurretFacing_IsTheHullFacingUntilAimed_ThenTheTurretOwnFacing()
    {
        Simulation sim = TurretSim(out EntityId own, out _, out EntityId hull);
        UnitView v = Assert.Single(sim.Snapshot(0).Units, u => u.Id == own);
        Assert.Equal(sim.Units.Facing[own.Index], v.TurretFacing);

        ref var entry = ref sim.CombatData.At(ActorKind.Vehicle, own.Index);
        entry.TurretInit = true;
        entry.TurretFacing = 200;
        UnitView aimed = Assert.Single(sim.Snapshot(0).Units, u => u.Id == own);
        Assert.Equal((byte?)200, aimed.TurretFacing);
        Assert.NotEqual(aimed.Facing, aimed.TurretFacing);   // the turret turns independently of the hull

        // a type without a turret carries none
        Assert.Null(Assert.Single(sim.Snapshot(0).Units, u => u.Id == hull).TurretFacing);
    }

    [Fact]
    public void TurretFacing_OfAnUnseenEnemy_IsNotInTheViewersSnapshot_ButIsInTheOmniscientOne()
    {
        Simulation sim = TurretSim(out _, out EntityId far, out _);
        Assert.DoesNotContain(sim.Snapshot(0).Units, u => u.Id == far && u.Player == 1);
        Assert.Contains(sim.Snapshot(-1).Units, u => u.Id == far && u.TurretFacing.HasValue);
    }

    [Fact]
    public void DiveFlag_IsSetWhileDiving_AndClearOnceUnderground()
    {
        var sim = BurrowWorld.NewSim();
        EntityId u = sim.SpawnVehicle(0, BurrowWorld.Burrower, new CellCoord(10, 10));
        BurrowWorld.Step(sim, BurrowWorld.MoveTo(sim, u, 40, 10));
        Assert.Equal(ActorLayer.Diving, BurrowWorld.Layer(sim, u));
        UnitView diving = Assert.Single(sim.Snapshot(0).Units, x => x.Id == u);
        Assert.True(diving.Diving);
        Assert.False(diving.Underground);

        BurrowWorld.RunUntil(sim, () => BurrowWorld.Layer(sim, u) == ActorLayer.Burrowed);
        UnitView under = Assert.Single(sim.Snapshot(0).Units, x => x.Id == u);
        Assert.False(under.Diving);
        Assert.True(under.Underground);
    }

    [Fact]
    public void DiveFlag_OfAnEnemyOnTheSurface_IsVisibleLikeAnyVisibleUnit_AndHiddenOnceItIsUnderground()
    {
        var sim = BurrowWorld.NewSim();
        EntityId mine = sim.SpawnVehicle(1, BurrowWorld.Enemy, new CellCoord(12, 10));   // gives player 1 eyes
        EntityId u = sim.SpawnVehicle(0, BurrowWorld.Burrower, new CellCoord(10, 10));
        BurrowWorld.Step(sim, BurrowWorld.MoveTo(sim, u, 40, 10));
        Assert.Equal(ActorLayer.Diving, BurrowWorld.Layer(sim, u));
        Assert.Contains(sim.Snapshot(1).Units, x => x.Id == u && x.Player == 0 && x.Diving);
        BurrowWorld.RunUntil(sim, () => BurrowWorld.Layer(sim, u) == ActorLayer.Burrowed);
        Assert.DoesNotContain(sim.Snapshot(1).Units, x => x.Id == u && x.Player == 0);
        Assert.True(mine.Index > 0);
    }

    [Fact]
    public void GateOpen_IsFalseUntilATeamUnitStandsInTheGate()
    {
        var sim = new Simulation(HardeningWorld.Setup());
        var ring = HardeningWorld.BuildRing(sim);
        EntityId gate = ring[HardeningWorld.GateCell];
        Assert.False(Assert.Single(sim.Snapshot(0).Buildings, b => b.Id == gate).GateOpen);

        EntityId tank = sim.SpawnVehicle(0, HardeningWorld.Tank, HardeningWorld.GateCell);
        Assert.False(tank.IsNone);
        sim.Step(None);
        Assert.True(Assert.Single(sim.Snapshot(0).Buildings, b => b.Id == gate).GateOpen);
        Assert.True(Assert.Single(sim.Snapshot(-1).Buildings, b => b.Id == gate).GateOpen);
        // walls never read open, whatever stands next to them
        Assert.DoesNotContain(sim.Snapshot(0).Buildings, b => b.GateOpen && b.Id != gate);
    }

    [Fact]
    public void GateOpen_IgnoresAnEnemyStandingInTheGate()
    {
        var sim = new Simulation(HardeningWorld.Setup());
        var ring = HardeningWorld.BuildRing(sim);
        EntityId gate = ring[HardeningWorld.GateCell];
        sim.SpawnVehicle(1, HardeningWorld.Tank, HardeningWorld.GateCell);   // not the owner's team: the gate stays a wall
        sim.Step(None);
        Assert.False(Assert.Single(sim.Snapshot(-1).Buildings, b => b.Id == gate).GateOpen);
    }

    [Fact]
    public void Superweapon_ChargeIsTheOwnersOnly_AndReadyWhenFull()
    {
        (Simulation sim, EntityId sun) = SuperweaponFixture.Base();
        SuperweaponFixture.Run(sim, 10);

        SuperweaponView mine = Assert.Single(sim.Snapshot(0).Superweapons);
        Assert.Equal(sun, mine.Building);
        Assert.Equal(0, mine.Owner);
        Assert.InRange(mine.Charge, 1, SuperweaponFixture.Total - 1);
        Assert.False(mine.Ready);
        Assert.Equal(5, mine.BlastRadius);
        Assert.Equal(sim.HudFor(0).Superweapons[0], mine);   // the same record the HUD button reads

        Assert.Empty(sim.Snapshot(1).Superweapons);       // nobody is told another player's superweapon charges
        Assert.Single(sim.Snapshot(-1).Superweapons);     // the omniscient view lists it

        SuperweaponFixture.Run(sim, SuperweaponFixture.Total);
        SuperweaponView ready = Assert.Single(sim.Snapshot(0).Superweapons);
        Assert.True(ready.Ready);
        Assert.Equal(ready.Total, ready.Charge);
    }

    [Fact]
    public void SnapshotCarriesNoNewState_SoTheHashIsUnchangedBySnapshotting()
    {
        (Simulation sim, _) = SuperweaponFixture.Base();
        SuperweaponFixture.Run(sim, 5);
        ulong before = sim.ComputeStateHash();
        _ = sim.Snapshot(0); _ = sim.Snapshot(1); _ = sim.Snapshot(-1);
        Assert.Equal(before, sim.ComputeStateHash());
    }
}
