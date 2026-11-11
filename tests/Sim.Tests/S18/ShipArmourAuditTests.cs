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

    [Fact]
    public void Audit_EveryNonExpeditionWarhead_Is100PercentOrZeroAgainstAllThreeShipClasses()
    {
        // S18 audit: pins current behaviour of FACTION-LE-07; flip the assertion when fixed.
        SimRules r = S18World.RealRules;
        WarheadType[] land = r.Warheads.Where(w => !w.Id.StartsWith("exp_", StringComparison.Ordinal)).ToArray();
        Assert.True(land.Length >= 20);
        foreach (WarheadType w in land)
        {
            int[] ship = { w.VersusPercent[(int)ArmorClass.ShipLight], w.VersusPercent[(int)ArmorClass.ShipHeavy], w.VersusPercent[(int)ArmorClass.Submerged] };
            Assert.True(ship.All(p => p == 100) || ship.All(p => p == 0), $"{w.Id}: {string.Join(",", ship)}");
        }
        // the Expedition's own rifle warhead, for comparison, is graded
        WarheadType own = r.Warheads.Single(w => w.Id == "exp_small_arms");
        Assert.Equal(70, own.VersusPercent[(int)ArmorClass.ShipLight]);
        Assert.Equal(30, own.VersusPercent[(int)ArmorClass.ShipHeavy]);
    }

    [Theory]
    // weapon, damage per 100 s vs a heavy tank, vs a ship_heavy hull (cruiser, destroyer, tender, Command Ship)
    [InlineData("lb_rifle", 133, 933)]          // Scavenger: 1.3 -> 9.3 dps
    [InlineData("lb_rotor_gun", 250, 1750)]     // Rotor: 2.5 -> 17.5 dps
    [InlineData("dir_rifle", 125, 750)]         // Conscript
    [InlineData("exp_marine_rifle", 285, 428)]  // the Expedition's own rifle: 30 % vs ship_heavy
    public void Audit_SmallArms_DpsAgainstAHeavyTank_AndAgainstAHeavyShip(string weapon, int vsHeavy, int vsShipHeavy)
    {
        // S18 audit: pins current behaviour of FACTION-LE-07; flip the assertion when fixed.
        Assert.Equal(vsHeavy, Dps100(weapon, ArmorClass.Heavy));
        Assert.Equal(vsShipHeavy, Dps100(weapon, ArmorClass.ShipHeavy));
    }

    [Fact]
    public void Audit_FourRotors_SinkACruiserInAbout17Seconds_OnPaper()
    {
        // S18 audit: pins current behaviour of FACTION-LE-07 (duel `lb_rotor:4` vs `exp_cruiser:1` measured 516 ticks incl. the approach).
        SimRules r = S18World.RealRules;
        int hp = r.Vehicles.Single(v => v.Id == "exp_cruiser").MaxHealth;
        int ticks = hp * 2000 / (4 * Dps100("lb_rotor_gun", ArmorClass.ShipHeavy)) ;
        Assert.Equal(1200, hp);
        Assert.InRange(ticks, 330, 350);
    }

    [Fact(Skip = "S18 FACTION-LE-07: awaiting owner decision")]
    public void Desired_RifleFire_IsNoBetterAgainstAHeavyShipThanAgainstAHeavyTank()
    {
        Assert.True(Dps100("lb_rifle", ArmorClass.ShipHeavy) <= 2 * Dps100("lb_rifle", ArmorClass.Heavy));
    }
}
