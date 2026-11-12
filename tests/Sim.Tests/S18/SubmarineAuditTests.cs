// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit of the Missile Submarine on the shipped data. FACTION-LE-05 / FACTION-MATH-02: a dived sub still fires and no
/// Authority, Directorate or Left Behind weapon or sensor can touch it. FACTION-LE-06: the sub keeps <c>submerged</c> armour on the
/// surface, so every Expedition weapon does the 1-point minimum to it.</summary>
public sealed class SubmarineAuditTests
{
    private static readonly string[] LandPrefixes = ["auth_", "dir_", "lb_"];

    private static bool IsLandFactionId(string id) => LandPrefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal));

    [Fact]
    public void Audit_NoLandFactionWeaponCanTargetSubmerged_AndNoLandFactionTypeHasSonar()
    {
        // S18 audit: pins current behaviour of FACTION-LE-05 / FACTION-MATH-02; flip the assertion when fixed.
        SimRules r = S18World.RealRules;
        WeaponType[] land = r.Weapons.Where(w => IsLandFactionId(w.Id)).ToArray();
        Assert.True(land.Length >= 20, $"only {land.Length} land-faction weapons found");
        Assert.All(land, w => Assert.True(w.Targets is not (WeaponTargets.Submerged or WeaponTargets.NavalAndSubmerged), w.Id));

        // every weapon actually mounted (primary or death weapon) on a land-faction type, whatever its id
        var mounted = new List<int>();
        foreach (VehicleType v in r.Vehicles.Where(v => v.Faction != Faction.Expedition)) { mounted.Add(v.PrimaryWeapon); mounted.Add(v.DeathWeapon); }
        foreach (InfantryType i in r.Infantry.Where(i => i.Faction != Faction.Expedition)) { mounted.Add(i.PrimaryWeapon); mounted.Add(i.DeathWeapon); }
        foreach (BuildingType b in r.Buildings.Where(b => b.Faction != Faction.Expedition)) mounted.Add(b.PrimaryWeapon);
        Assert.All(mounted.Where(x => x >= 0), x => Assert.True(r.Weapons[x].Targets is not (WeaponTargets.Submerged or WeaponTargets.NavalAndSubmerged), r.Weapons[x].Id));

        Assert.DoesNotContain(r.Vehicles, v => v.Faction != Faction.Expedition && v.SonarRadius > 0);
        Assert.DoesNotContain(r.Buildings, b => b.Faction != Faction.Expedition && b.SonarRadius > 0);
        // the whole anti-submarine kit in the game: one weapon, two sonar carriers, all Expedition
        Assert.Equal(new[] { "exp_launch_charge" }, r.Weapons.Where(w => w.Targets is WeaponTargets.Submerged or WeaponTargets.NavalAndSubmerged).Select(w => w.Id));
        Assert.Equal(new[] { "exp_destroyer" }, r.Vehicles.Where(v => v.SonarRadius > 0).Select(v => v.Id));
        Assert.Equal(new[] { "exp_picket" }, r.Buildings.Where(b => b.SonarRadius > 0).Select(b => b.Id));
    }

    /// <summary>An Expedition sub 4 cells off an Authority shore held by a powered Turret and a Tank.</summary>
    private static (S18World W, EntityId Sub, EntityId Turret, EntityId Tank) Coast()
    {
        var w = new S18World(Faction.Expedition, Faction.Authority, waterFromX: 40, keepAlive: false);
        w.Place(1, "auth_power", 30, 26);
        w.Place(1, "auth_power", 30, 34);
        EntityId turret = w.Place(1, "auth_turret", 39, 30);
        EntityId tank = w.Veh(1, "auth_tank", 39, 32);
        EntityId sub = w.Veh(0, "exp_missile_sub", 43, 30);
        return (w, sub, turret, tank);
    }

    [Fact]
    public void Audit_SameCoast_SurfacedSub_IsShotByTheTurretAndTheTank()
    {
        // the comparison case: the defenders are in range and do fight a sub they can see
        var (w, sub, turret, _) = Coast();
        int max = w.VehHp(sub);
        w.Run(1, w.Attack(0, ActorKind.Vehicle, sub, ActorKind.Building, turret));
        w.Run(150);
        Assert.True(w.VehHp(sub) < max);
    }

    [Fact]
    public void Desired_DivedSubThatFiresOnACoast_CanBeAnswered()
    {
        var (w, sub, turret, _) = Coast();
        int max = w.VehHp(sub);
        w.Run(1, w.Cmd(0, CommandKind.Deploy, sub, default, default, (int)ActorKind.Vehicle));
        w.Run(1, w.Attack(0, ActorKind.Vehicle, sub, ActorKind.Building, turret));
        w.Run(700);
        Assert.True(w.VehHp(sub) < max || w.BldHp(turret) > 0);
    }

    [Fact]
    public void Desired_SurfacedSub_TakesRealDamageFromADestroyerGun()
    {
        var world = new S18World(Faction.Expedition, Faction.Expedition, waterFromX: 40, keepAlive: false);
        world.Place(0, "exp_landing_depot", 30, 10);
        world.Place(1, "exp_landing_depot", 30, 50);
        EntityId sub = world.Veh(0, "exp_missile_sub", 44, 30);
        EntityId destroyer = world.Veh(1, "exp_destroyer", 44, 25);
        int max = world.VehHp(sub);
        world.Run(1, world.Attack(1, ActorKind.Vehicle, destroyer, ActorKind.Vehicle, sub));
        world.Run(200);
        Assert.True(max - world.VehHp(sub) >= 60);
    }
}
