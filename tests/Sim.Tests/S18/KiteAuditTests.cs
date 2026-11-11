// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit, FACTION-LE-14: the Kite is "one-use" only because its own allied-hitting splash equals its hit points. The bomb
/// reloads in one tick and no rule removes the Kite after a drop.</summary>
public sealed class KiteAuditTests
{
    [Fact]
    public void Audit_KiteData_HasNoOneUseRule_OnlyASplashThatEqualsItsHealth()
    {
        // S18 audit: pins current behaviour of FACTION-LE-14; flip the assertion when fixed.
        SimRules r = S18World.RealRules;
        VehicleType kite = r.Vehicles.Single(v => v.Id == "lb_kite");
        WeaponType bomb = r.Weapons[kite.PrimaryWeapon];
        WarheadType blast = r.Warheads[bomb.Warhead];
        Assert.Equal("lb_kite_bomb", bomb.Id);
        Assert.Equal(1, bomb.RateOfFire);                       // reloads every tick
        Assert.Equal(kite.PrimaryWeapon, kite.DeathWeapon);     // and drops the same bomb again when it dies
        Assert.True(blast.HitsAllies);
        Assert.Equal(1, blast.Splash);
        Assert.False(kite.RaiderOneUse);
        // half splash on its own light armour = exactly its health; one point more health and it would survive a drop
        Assert.Equal(kite.MaxHealth, Simulation.ComputeDamage(bomb.Damage, blast, kite.Armor) / 2);
    }

    /// <summary>One Kite attacks an enemy Refinery. Returns the damage the Refinery took and whether the Kite died.</summary>
    private static (int Damage, bool KiteDead, int PerBomb) Bomb(int rank)
    {
        var w = new S18World(Faction.LeftBehind, Faction.Authority);
        EntityId refinery = w.Place(1, "auth_refinery", 26, 20);
        EntityId kite = w.Veh(0, "lb_kite", 20, 21);
        if (rank > 0) Assert.Equal(rank, w.Sim.ScriptSetRank(ActorKind.Vehicle, kite, rank));
        WeaponType bomb = w.W("lb_kite_bomb");
        int perBomb = Simulation.ComputeDamage(bomb.Damage, w.Rules.Warheads[bomb.Warhead], ArmorClass.Structure);
        int max = w.BldHp(refinery);
        w.Run(1, w.Attack(0, ActorKind.Vehicle, kite, ActorKind.Building, refinery));
        w.Run(120);
        return (max - w.BldHp(refinery), !w.Sim.Units.Slots.IsAlive(kite), perBomb);
    }

    [Fact]
    public void Audit_Kite_DropsOneBomb_DiesOfItsOwnSplash_AndItsDeathDropsASecond()
    {
        // S18 audit: pins current behaviour of FACTION-LE-14; flip the assertion when fixed.
        var (damage, dead, perBomb) = Bomb(0);
        Assert.Equal(144, perBomb);
        Assert.True(dead);
        // the bomb (full, on the footprint) plus the death bomb (full or half, depending on which cell the Kite died over)
        Assert.Contains(damage, new[] { perBomb + perBomb, perBomb + perBomb / 2 });
    }

    [Fact]
    public void Audit_HeroKite_StillDiesOnItsFirstDrop_BecauseRankAlsoRaisesItsOwnBomb()
    {
        // S18 audit: FACTION-LE-14 said any veterancy rank would make the Kite reusable. Measured: it does not (rank raises the bomb
        // damage more than it lowers damage taken), so no current rule keeps a Kite alive. The mechanism is still an accident of numbers.
        var (damage, dead, perBomb) = Bomb(3);
        Assert.True(dead);
        Assert.True(damage < 3 * (perBomb * 13 / 10), $"damage {damage}");   // not more than two Hero bombs
    }
}
