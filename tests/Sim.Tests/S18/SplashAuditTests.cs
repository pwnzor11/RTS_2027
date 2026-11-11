// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit on the shipped data. FAD-06: splash damage ignores the air / ground split (flak shooting an aircraft hurts the
/// owner's building under it; a ground shell hurts an aircraft above its target). FAD-13: weapons with damage 0 still deal 1 per hit.</summary>
public sealed class SplashAuditTests
{
    [Fact]
    public void FlakShootingAnAircraft_DoesNotDamageItsOwnersBuildingUnderneath()
    {
        // S19 (D-205, FAD-06): flipped from the S18 audit pin; flak splash hurts aircraft only.
        var w = new S18World();
        EntityId plant = w.Place(0, "auth_power", 20, 20);
        w.Place(0, "auth_flak", 23, 23);
        EntityId skyhook = w.Veh(1, "exp_skyhook", 20, 20);   // an unarmed enemy aircraft hovering over the plant
        Assert.Equal(-1, w.Rules.Vehicles[w.V("exp_skyhook")].PrimaryWeapon);
        WeaponType flak = w.W("auth_flak_gun");
        Assert.Equal(WeaponTargets.Air, flak.Targets);
        int perShell = Simulation.ComputeDamage(flak.Damage, w.Rules.Warheads[flak.Warhead], ArmorClass.Structure);
        int plantMax = w.BldHp(plant), skyMax = w.VehHp(skyhook);

        w.Run(200);

        Assert.True(w.VehHp(skyhook) < skyMax, "the flak did engage the aircraft");
        Assert.True(perShell > 0);
        Assert.Equal(plantMax, w.BldHp(plant));   // the only weapon on the map is the plant owner's own flak
    }

    [Fact]
    public void GroundShellLandingOnABuilding_DoesNotDamageTheAircraftAboveIt()
    {
        // S19 (D-205, FAD-06): flipped from the S18 audit pin.
        var w = new S18World();
        EntityId target = w.Place(1, "auth_power", 28, 30);
        EntityId howitzer = w.Veh(0, "auth_howitzer", 20, 30);
        EntityId skyhook = w.Veh(1, "exp_skyhook", 28, 30);   // above the footprint cell nearest the gun
        Assert.Equal(WeaponTargets.Ground, w.W("auth_howitzer_gun").Targets);
        int skyMax = w.VehHp(skyhook);
        w.Run(1, w.Attack(0, ActorKind.Vehicle, howitzer, ActorKind.Building, target));
        w.Run(200);
        Assert.True(w.BldHp(target) < w.Rules.Buildings[w.B("auth_power")].MaxHealth, "the shell did land");
        Assert.Equal(skyMax, w.VehHp(skyhook));
    }

    [Fact]
    public void Desired_FlakShootingAnAircraft_LeavesTheBuildingUnderneathAlone()
    {
        var w = new S18World();
        EntityId plant = w.Place(0, "auth_power", 20, 20);
        w.Place(0, "auth_flak", 23, 23);
        w.Veh(1, "exp_skyhook", 20, 20);
        int plantMax = w.BldHp(plant);
        w.Run(200);
        Assert.Equal(plantMax, w.BldHp(plant));
    }

    [Theory]
    [InlineData("auth_decon_truck", "auth_decon_spray")]
    [InlineData("auth_foam_truck", "auth_foam_spray")]
    [InlineData("dir_ash_sower", "dir_ash_sow")]
    public void ZeroDamageSpray_TakesNoHitPoints(string truck, string weapon)
    {
        // S19 (D-205, FAD-13): flipped from the S18 audit pin.
        var w = new S18World();
        WeaponType spray = w.W(weapon);
        Assert.Equal(0, spray.Damage);
        Assert.Equal(weapon, w.Rules.Weapons[w.Rules.Vehicles[w.V(truck)].PrimaryWeapon].Id);
        EntityId silo = w.Place(1, "auth_silo", 23, 20);
        EntityId t = w.Veh(0, truck, 20, 20);
        int max = w.BldHp(silo);
        w.Run(1, w.Attack(0, ActorKind.Vehicle, t, ActorKind.Building, silo));
        w.Run(260);
        int lost = max - w.BldHp(silo);
        Assert.Equal(0, lost);
    }

    [Fact]
    public void EveryZeroDamageWeapon_ComputesToZero()
    {
        // S19 (D-205, FAD-13): flipped from the S18 audit pin.
        SimRules r = S18World.RealRules;
        WeaponType[] zero = r.Weapons.Where(w => w.Damage == 0).ToArray();
        Assert.Equal(new[] { "auth_decon_spray", "auth_foam_spray", "dir_ash_sow", "dir_blackout_drop", "lb_dust_drop" }, zero.Select(w => w.Id).OrderBy(x => x, StringComparer.Ordinal));
        foreach (WeaponType w in zero)
            foreach (ArmorClass a in Enum.GetValues<ArmorClass>())
                Assert.Equal(0, Simulation.ComputeDamage(w.Damage, r.Warheads[w.Warhead], a));
    }

    [Fact]
    public void Desired_ZeroDamageSpray_DealsNoDamage()
    {
        var w = new S18World();
        EntityId silo = w.Place(1, "auth_silo", 23, 20);
        EntityId t = w.Veh(0, "auth_decon_truck", 20, 20);
        int max = w.BldHp(silo);
        w.Run(1, w.Attack(0, ActorKind.Vehicle, t, ActorKind.Building, silo));
        w.Run(260);
        Assert.Equal(max, w.BldHp(silo));
    }
}
