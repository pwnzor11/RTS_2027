// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.M3.Ground.GroundFixture;
using CF = Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.M3.Ground;

/// <summary>M3-05: radiation, Blackout, seismic, stealth, jam, and the combat targeting filters.</summary>
public sealed class GroundTests
{
    private static LeptonCoord Centre(int x, int y) => LeptonCoord.CenterOf(new CellCoord(x, y));

    private static void Blast(Simulation sim, string warhead, int x, int y) =>
        sim.Detonate(0, 1, Wh(warhead), Centre(x, y), default, false);

    // ---- radiation --------------------------------------------------------------------------

    [Fact]
    public void Contaminate_AddsLevelInSplash_AndClampsAt255()
    {
        var sim = NewSim();
        Blast(sim, "g_cont", 10, 10);
        Assert.Equal(100, sim.Ground.RadiationAt(new CellCoord(10, 10)));
        Assert.Equal(100, sim.Ground.RadiationAt(new CellCoord(11, 11)));
        Assert.Equal(0, sim.Ground.RadiationAt(new CellCoord(12, 10)));
        Blast(sim, "g_cont", 10, 10); Blast(sim, "g_cont", 10, 10);
        Assert.Equal(255, sim.Ground.RadiationAt(new CellCoord(10, 10)));
    }

    [Fact]
    public void Radiation_DecaysOnePerDecayInterval()
    {
        var sim = NewSim();
        sim.Ground.Radiation[sim.Ground.IndexOf(new CellCoord(5, 5))] = 50;
        Steps(sim, 79);   // ticks 1..79 contain the decay at tick 40 only
        Assert.Equal(49, sim.Ground.RadiationAt(new CellCoord(5, 5)));
        Steps(sim, 1);    // tick 80
        Assert.Equal(48, sim.Ground.RadiationAt(new CellCoord(5, 5)));
    }

    [Fact]
    public void Radiation_HurtsActorsOnHotCells_ScaledByResist_AndSkipsColdCells()
    {
        var sim = Fresh();
        var plain = sim.SpawnInfantry(0, Inf("g_foot"), new CellCoord(10, 10), 0);
        var half = sim.SpawnInfantry(0, Inf("g_foot_half"), new CellCoord(10, 10), 1);
        var immune = sim.SpawnVehicle(0, Veh("g_immune"), new CellCoord(10, 10));
        var cold = sim.SpawnInfantry(0, Inf("g_foot"), new CellCoord(14, 10), 0);
        var weak = sim.SpawnInfantry(0, Inf("g_foot"), new CellCoord(16, 10), 0);
        Ready(sim);
        sim.Ground.Radiation[sim.Ground.IndexOf(new CellCoord(10, 10))] = 100;
        sim.Ground.Radiation[sim.Ground.IndexOf(new CellCoord(16, 10))] = 31;   // below the threshold
        Steps(sim, 19);   // ticks 1..19: nothing yet
        Assert.Equal(100, sim.Infantry.Health[plain.Index]);
        Steps(sim, 1);    // tick 20: level 100 x 100 permille = 10
        Assert.Equal(90, sim.Infantry.Health[plain.Index]);
        Assert.Equal(95, sim.Infantry.Health[half.Index]);
        Assert.Equal(500, sim.Units.Health[immune.Index]);
        Assert.Equal(100, sim.Infantry.Health[cold.Index]);
        Assert.Equal(100, sim.Infantry.Health[weak.Index]);
    }

    [Fact]
    public void Decontaminate_RemovesLevel_AndStopsAtZero()
    {
        var sim = NewSim();
        Blast(sim, "g_cont", 10, 10);
        Blast(sim, "g_decon", 10, 10);
        Assert.Equal(40, sim.Ground.RadiationAt(new CellCoord(10, 10)));
        Blast(sim, "g_decon", 10, 10);
        Assert.Equal(0, sim.Ground.RadiationAt(new CellCoord(10, 10)));
    }

    [Fact]
    public void BonusVsIrradiated_AppliesToIrradiatedTypesAndToTargetsInRadiation()
    {
        var sim = Fresh();
        var irr = sim.SpawnVehicle(1, Veh("g_irr"), new CellCoord(10, 10));
        var normal = sim.SpawnVehicle(1, Veh("g_plain_veh"), new CellCoord(14, 10));
        var inRad = sim.SpawnVehicle(1, Veh("g_immune"), new CellCoord(18, 10));   // immune to radiation damage itself
        Ready(sim);
        sim.Ground.Radiation[sim.Ground.IndexOf(new CellCoord(18, 10))] = 64;
        foreach (var (t, x) in new[] { (irr, 10), (normal, 14), (inRad, 18) })
            sim.Detonate(0, 10, Wh("g_bonus"), Centre(x, 10), new ActorRef(ActorKind.Vehicle, t), true);
        sim.Step(CF.None);
        Assert.Equal(500 - 20, sim.Units.Health[irr.Index]);
        Assert.Equal(500 - 10, sim.Units.Health[normal.Index]);
        Assert.Equal(500 - 20, sim.Units.Health[inRad.Index]);
    }

    // ---- Blackout ---------------------------------------------------------------------------

    [Fact]
    public void Blackout_SetsTimerOnSplashCells_AndCountsDown()
    {
        var sim = NewSim();
        Blast(sim, "g_black", 10, 10);
        Assert.Equal(30, sim.Ground.BlackoutAt(new CellCoord(11, 9)));
        Assert.Equal(0, sim.Ground.BlackoutAt(new CellCoord(12, 10)));
        Steps(sim, 10);
        Assert.Equal(20, sim.Ground.BlackoutAt(new CellCoord(10, 10)));
        Steps(sim, 25);
        Assert.Equal(0, sim.Ground.BlackoutAt(new CellCoord(10, 10)));
    }

    [Fact]
    public void Blackout_SuppressesVision_UnlessAdjacentOrSearchlight()
    {
        var sim = Fresh();
        sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(10, 10));
        sim.SpawnVehicle(0, Veh("g_plain_veh"), new CellCoord(14, 10));
        Ready(sim);
        var target = new CellCoord(14, 10);
        Assert.True(sim.IsVisibleTo(1, target));
        Blast(sim, "g_black", 14, 10);
        Assert.False(sim.IsVisibleTo(1, target));
        Assert.True(sim.IsVisibleTo(1, new CellCoord(9, 10)));   // outside the cloud: unaffected
        // a unit within 1 cell sees into the cloud
        Spawn(sim, 1, false, Veh("g_plain_veh"), 13, 10);
        Assert.True(sim.IsVisibleTo(1, target));
    }

    [Fact]
    public void Blackout_Searchlight_SeesThrough_AndCloudExpires()
    {
        var target = new CellCoord(14, 10);
        WeaponType w = TestRules.Weapons[Wp("g_ground")];
        foreach (bool searchlight in new[] { false, true })
        {
            var sim = Fresh();
            sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(10, 10));
            sim.SpawnVehicle(0, Veh("g_plain_veh"), new CellCoord(14, 10));
            if (searchlight) sim.PlaceBuilding(1, Bld("g_search"), new CellCoord(10, 14));
            Ready(sim);
            Blast(sim, "g_black", 14, 10);
            Assert.Equal(searchlight, sim.IsVisibleTo(1, target));
            Assert.Equal(!searchlight, sim.AcquireTarget(1, Centre(10, 10), w).Id.IsNone);
        }
    }

    [Fact]
    public void Blackout_Expires_RestoringVision()
    {
        var sim = Fresh();
        sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(10, 10));
        Ready(sim);
        Blast(sim, "g_black", 14, 10);
        Assert.False(sim.IsVisibleTo(1, new CellCoord(14, 10)));
        Steps(sim, 31);
        Assert.True(sim.IsVisibleTo(1, new CellCoord(14, 10)));
    }

    // ---- seismic, stealth, jam ----------------------------------------------------------------

    [Fact]
    public void Seismic_RevealsBurrowedUnitsInRadiusOnly()
    {
        foreach (bool seismo in new[] { false, true })
        {
            var sim = Fresh();
            var mole = sim.SpawnVehicle(0, Veh("g_plain_veh"), new CellCoord(20, 20));
            sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(20, 18));   // sees the cell, but burrowed stays hidden
            if (seismo) sim.PlaceBuilding(1, Bld("g_seismo"), new CellCoord(26, 20));   // 6 cells away: in radius
            Ready(sim);
            sim.Tunnels.VehicleLayer[mole.Index] = ActorLayer.Burrowed;
            Assert.Equal(seismo, sim.IsSeismicRevealed(1, new CellCoord(20, 20)));
            Assert.Equal(!seismo, sim.IsHiddenFrom(1, ActorKind.Vehicle, mole.Index));
            Assert.False(sim.IsSeismicRevealed(1, new CellCoord(19, 27)));   // outside the radius
            Assert.False(sim.IsSeismicRevealed(0, new CellCoord(20, 20)));   // the seismograph belongs to player 1
        }
    }

    [Fact]
    public void Stealth_HiddenWhenStill_RevealedInRange_ResetByMoving()
    {
        var sim = Fresh();
        var sneak = sim.SpawnInfantry(0, Inf("g_sneak"), new CellCoord(10, 10), 0);
        sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(15, 10));   // sees it (sight 8) from 5 cells
        Steps(sim, 59);
        Assert.False(sim.IsStealthedNow(ActorKind.Infantry, sneak.Index));
        Assert.False(sim.IsHiddenFrom(1, ActorKind.Infantry, sneak.Index));
        Steps(sim, 2);
        Assert.True(sim.IsStealthedNow(ActorKind.Infantry, sneak.Index));
        Assert.True(sim.IsHiddenFrom(1, ActorKind.Infantry, sneak.Index));
        Assert.False(sim.IsHiddenFrom(0, ActorKind.Infantry, sneak.Index));   // never hidden from its own team
        // an enemy within 1 cell reveals it
        Spawn(sim, 1, true, Inf("g_foot"), 11, 10);
        Assert.False(sim.IsHiddenFrom(1, ActorKind.Infantry, sneak.Index));
        // moving resets the timer
        sim.Step(new[] { CF.Move(sim, 0, ActorKind.Infantry, sneak, 3, 3) });
        Steps(sim, 4);
        Assert.False(sim.IsStealthedNow(ActorKind.Infantry, sneak.Index));
        Assert.True(sim.Effects.InfantryStill[sneak.Index] < 3);
    }

    [Fact]
    public void Stealth_SearchlightRevealsAtSightRange()
    {
        foreach (bool searchlight in new[] { false, true })
        {
            var sim = Fresh();
            var sneak = sim.SpawnInfantry(0, Inf("g_sneak"), new CellCoord(10, 10), 0);
            sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(18, 10));
            if (searchlight) sim.PlaceBuilding(1, Bld("g_search"), new CellCoord(10, 15));   // sight 6 covers 5 cells
            Steps(sim, 65);
            Assert.Equal(!searchlight, sim.IsHiddenFrom(1, ActorKind.Infantry, sneak.Index));
        }
    }

    [Fact]
    public void Jam_CoversEnemyRadiusOnly()
    {
        var sim = Fresh();
        sim.SpawnVehicle(0, Veh("g_jam"), new CellCoord(10, 10));
        Ready(sim);
        Assert.True(sim.IsJammedFor(1, new CellCoord(15, 10)));
        Assert.False(sim.IsJammedFor(1, new CellCoord(16, 10)));
        Assert.False(sim.IsJammedFor(0, new CellCoord(12, 10)));   // own jammer does not blind its owner
    }

    // ---- targeting ----------------------------------------------------------------------------

    [Fact]
    public void Underground_NotTargetable_AndCannotFire()
    {
        var sim = Fresh();
        var gun = sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(10, 10));
        var victim = sim.SpawnVehicle(0, Veh("g_plain_veh"), new CellCoord(13, 10));
        sim.PlaceBuilding(1, Bld("g_seismo"), new CellCoord(8, 10));   // reveals the burrowed victim, which stays untargetable
        Ready(sim);
        var target = new ActorRef(ActorKind.Vehicle, victim);
        WeaponType w = TestRules.Weapons[Wp("g_ground")];
        Assert.True(sim.IsValidTarget(1, target));
        Assert.True(sim.IsValidAutoTarget(1, target));
        Assert.False(sim.AcquireTarget(1, Centre(10, 10), w).Id.IsNone);

        sim.Tunnels.VehicleLayer[victim.Index] = ActorLayer.Burrowed;
        Assert.False(sim.IsHiddenFrom(1, ActorKind.Vehicle, victim.Index));
        Assert.False(sim.IsValidTarget(1, target));
        Assert.False(sim.IsValidAutoTarget(1, target));
        Assert.True(sim.AcquireTarget(1, Centre(10, 10), w).Id.IsNone);
        sim.Step(new[] { CF.Attack(sim, 1, ActorKind.Vehicle, gun, ActorKind.Vehicle, victim) });
        Assert.True(sim.CombatData.At(ActorKind.Vehicle, gun.Index).AttackTarget.Id.IsNone);

        // an underground shooter cannot fire; on the surface it can
        sim.Tunnels.VehicleLayer[victim.Index] = ActorLayer.Surface;
        sim.Tunnels.VehicleLayer[gun.Index] = ActorLayer.Tunnel;
        Assert.False(sim.Fire(ActorKind.Vehicle, gun.Index, Wp("g_ground"), target, Centre(13, 10)));
        sim.Tunnels.VehicleLayer[gun.Index] = ActorLayer.Surface;
        Assert.True(sim.Fire(ActorKind.Vehicle, gun.Index, Wp("g_ground"), target, Centre(13, 10)));
    }

    [Fact]
    public void StealthedEnemy_IsNotAcquired_UntilRevealed()
    {
        var sim = Fresh();
        sim.SpawnInfantry(0, Inf("g_sneak"), new CellCoord(13, 10), 0);
        sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(10, 10));
        Steps(sim, 65);
        WeaponType w = TestRules.Weapons[Wp("g_ground")];
        Assert.True(sim.AcquireTarget(1, Centre(10, 10), w).Id.IsNone);
        Spawn(sim, 1, true, Inf("g_foot"), 12, 10);
        Assert.False(sim.AcquireTarget(1, Centre(10, 10), w).Id.IsNone);
    }

    [Fact]
    public void WeaponTargets_AirVersusGround()
    {
        var sim = Fresh();
        sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(10, 10));
        var ground = sim.SpawnVehicle(0, Veh("g_plain_veh"), new CellCoord(12, 10));
        var plane = sim.SpawnVehicle(0, Veh("g_plane"), new CellCoord(11, 11));
        Ready(sim);
        WeaponType gw = TestRules.Weapons[Wp("g_ground")], aa = TestRules.Weapons[Wp("g_aa")], both = TestRules.Weapons[Wp("g_both")];

        ActorRef g = sim.AcquireTarget(1, Centre(10, 10), gw);   // plane is closer but out of reach of a ground weapon
        Assert.Equal(new ActorRef(ActorKind.Vehicle, ground), g);
        ActorRef a = sim.AcquireTarget(1, Centre(10, 10), aa);
        Assert.Equal(new ActorRef(ActorKind.Vehicle, plane), a);
        Assert.Equal(new ActorRef(ActorKind.Vehicle, plane), sim.AcquireTarget(1, Centre(10, 10), both));

        // Fire refuses a mismatched target and an explicit Attack order is dropped
        var gun = FirstGun(sim);
        Assert.False(sim.Fire(ActorKind.Vehicle, gun, Wp("g_ground"), new ActorRef(ActorKind.Vehicle, plane), Centre(11, 11)));
        Assert.False(sim.Fire(ActorKind.Vehicle, gun, Wp("g_aa"), new ActorRef(ActorKind.Vehicle, ground), Centre(12, 10)));
        Assert.True(sim.Fire(ActorKind.Vehicle, gun, Wp("g_aa"), new ActorRef(ActorKind.Vehicle, plane), Centre(11, 11)));
    }

    private static int FirstGun(Simulation sim)
    {
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == 1) return i;
        throw new InvalidOperationException();
    }

    // ---- save/load and determinism ------------------------------------------------------------

    private static Simulation Scenario()
    {
        var sim = Fresh(7);
        sim.SpawnInfantry(0, Inf("g_sneak"), new CellCoord(10, 10), 0);
        sim.SpawnInfantry(0, Inf("g_foot"), new CellCoord(20, 20), 0);
        sim.SpawnVehicle(1, Veh("g_gun"), new CellCoord(12, 10));
        sim.SpawnVehicle(0, Veh("g_jam"), new CellCoord(25, 25));
        Ready(sim);
        Blast(sim, "g_cont", 20, 20);
        Blast(sim, "g_black", 12, 12);
        return sim;
    }

    [Fact]
    public void SaveLoad_PreservesGroundAndEffects_AndStaysInSync()
    {
        var sim = Scenario();
        Steps(sim, 25);
        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        var loaded = Simulation.Load(ms, Setup(7));
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(sim.Ground.Radiation, loaded.Ground.Radiation);
        Assert.Equal(sim.Ground.Blackout, loaded.Ground.Blackout);
        Assert.Equal(sim.Effects.InfantryStill, loaded.Effects.InfantryStill);
        for (int i = 0; i < 80; i++)
        {
            sim.Step(CF.None); loaded.Step(CF.None);
            Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.True(loaded.IsStealthedNow(ActorKind.Infantry, 1));
    }

    [Fact]
    public void Determinism_SameSeedSameHashes()
    {
        var a = Scenario(); var b = Scenario();
        for (int i = 0; i < 200; i++)
        {
            a.Step(CF.None); b.Step(CF.None);
            Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        }
        Assert.NotEqual(0, a.Ground.Radiation.Sum(x => (int)x) + a.Effects.InfantryStill.Sum());
    }
}
