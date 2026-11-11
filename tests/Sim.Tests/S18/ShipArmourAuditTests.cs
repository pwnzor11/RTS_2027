// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit, FACTION-LE-07: Authority, Directorate and Left Behind warheads were padded to 100 % against the three ship armour
/// classes, so small arms that scratch a tank do full damage to a cruiser. Computed from the shipped data.</summary>
public sealed class ShipArmourAuditTests
{
    private static int PerShot(string weapon, ArmorClass armor)
    {
        SimRules r = S18World.RealRules;
        WeaponType w = r.Weapons.Single(x => x.Id == weapon);
        return Simulation.ComputeDamage(w.Damage, r.Warheads[w.Warhead], armor);
    }

    /// <summary>Damage per 100 seconds (2000 ticks), so the table stays in whole numbers.</summary>
    private static int Dps100(string weapon, ArmorClass armor)
    {
        WeaponType w = S18World.RealRules.Weapons.Single(x => x.Id == weapon);
        return PerShot(weapon, armor) * 2000 / w.RateOfFire;
    }

    [Theory]
    // D-205 / LE-07: the small-arms and anti-armour warheads of the three land factions copy the Expedition's graded ship columns
    // (Submerged stays 100: those weapons cannot target a dived vessel). Area/flame/demolition warheads are still 100 (QUESTIONS in s19_numbers.md).
    [InlineData("auth_small_arms", 70, 30)]
    [InlineData("dir_small_arms", 70, 30)]
    [InlineData("lb_small_arms", 70, 30)]
    [InlineData("auth_ap", 90, 70)]
    [InlineData("lb_ap", 90, 70)]
    [InlineData("lb_harpoon", 90, 70)]
    [InlineData("dir_rocket", 90, 70)]
    [InlineData("dir_slug", 90, 70)]
    public void SmallArmsAndAntiArmourWarheads_AreGradedAgainstShips(string id, int light, int heavy)
    {
        WarheadType w = S18World.RealRules.Warheads.Single(x => x.Id == id);
        Assert.Equal(light, w.VersusPercent[(int)ArmorClass.ShipLight]);
        Assert.Equal(heavy, w.VersusPercent[(int)ArmorClass.ShipHeavy]);
        Assert.Equal(100, w.VersusPercent[(int)ArmorClass.Submerged]);
        WarheadType own = S18World.RealRules.Warheads.Single(x => x.Id == "exp_small_arms");
        Assert.Equal(70, own.VersusPercent[(int)ArmorClass.ShipLight]);
        Assert.Equal(30, own.VersusPercent[(int)ArmorClass.ShipHeavy]);
    }

    [Theory]
    // weapon, damage per 100 s vs a heavy tank, vs a ship_heavy hull (cruiser, destroyer, tender, Command Ship)
    [InlineData("lb_rifle", 133, 266)]          // Scavenger: 9.3 -> 2.7 dps vs a ship_heavy hull
    [InlineData("lb_rotor_gun", 250, 500)]      // Rotor: 17.5 -> 5 dps
    [InlineData("dir_rifle", 125, 125)]         // Conscript: 7.5 -> 1.25 dps (1-damage shots, integer rounding)
    [InlineData("exp_marine_rifle", 285, 428)]  // the Expedition's own rifle: 30 % vs ship_heavy
    public void SmallArms_DpsAgainstAHeavyTank_AndAgainstAHeavyShip(string weapon, int vsHeavy, int vsShipHeavy)
    {
        // D-205 / LE-07: numbers re-pinned after the ship columns were filled.
        Assert.Equal(vsHeavy, Dps100(weapon, ArmorClass.Heavy));
        Assert.Equal(vsShipHeavy, Dps100(weapon, ArmorClass.ShipHeavy));
    }

    [Fact]
    public void FourRotors_SinkACruiserInAbout60Seconds_OnPaper()
    {
        // S18 audit: pins current behaviour of FACTION-LE-07 (duel `lb_rotor:4` vs `exp_cruiser:1` measured 516 ticks incl. the approach).
        SimRules r = S18World.RealRules;
        int hp = r.Vehicles.Single(v => v.Id == "exp_cruiser").MaxHealth;
        int ticks = hp * 2000 / (4 * Dps100("lb_rotor_gun", ArmorClass.ShipHeavy)) ;
        Assert.Equal(1200, hp);
        Assert.InRange(ticks, 1190, 1210);   // was about 340 ticks (17 s)
    }

    [Fact]
    public void Desired_RifleFire_IsNoBetterAgainstAHeavyShipThanAgainstAHeavyTank()
    {
        Assert.True(Dps100("lb_rifle", ArmorClass.ShipHeavy) <= 2 * Dps100("lb_rifle", ArmorClass.Heavy));
    }
}
