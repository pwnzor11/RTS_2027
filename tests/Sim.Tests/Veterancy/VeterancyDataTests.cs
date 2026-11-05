// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Veterancy;

/// <summary>VET-4: the shipped data/rules turn generic veterancy on with the design 14 §2 table, and exactly the damage-less utility
/// types opt out.</summary>
public sealed class VeterancyDataTests
{
    private static SimRules LoadReal()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        return RulesLoader.Load(dir!).Sim;
    }

    [Fact]
    public void ShippedRules_EnableVeterancy_WithTheDesignTable()
    {
        VeterancyRules v = LoadReal().Veterancy;
        Assert.True(v.Enabled);
        Assert.Equal(new[] { 600, 1600, 3200 }, v.Thresholds);
        Assert.Equal((1000, 500), (v.KillPermilleUnit, v.KillPermilleBuilding));
        Assert.Equal(new[] { 1100, 1200, 1300 }, v.DamagePermille);
        Assert.Equal(new[] { 950, 900, 800 }, v.TakenPermille);
        Assert.Equal(new[] { 950, 900, 800 }, v.ReloadPermille);
        Assert.Equal(new[] { 1000, 1050, 1100 }, v.SpeedPermille);
        Assert.Equal((20, 100), (v.HealIntervalTicks, v.HealQuietTicks));
        Assert.Equal(new[] { 0, 0, 10 }, v.HealPermille);
    }

    [Fact]
    public void EveryArmedType_Ranks_ExceptTheDamagelessUtilityTypes()
    {
        SimRules s = LoadReal();
        string[] optedOut = { "auth_decon_truck", "auth_foam_truck", "dir_ash_sower", "dir_dirigible", "lb_dust_sail" };
        foreach (VehicleType v in s.Vehicles)
        {
            bool armed = v.PrimaryWeapon >= 0;
            bool damaging = armed && s.Weapons[v.PrimaryWeapon].Damage > 0;
            Assert.Equal(damaging, v.CanRank);
            if (Array.IndexOf(optedOut, v.Id) >= 0) { Assert.False(v.Veteran ?? true); Assert.True(armed && !damaging, v.Id); }
        }
        foreach (InfantryType i in s.Infantry)
            Assert.Equal(i.PrimaryWeapon >= 0 && s.Weapons[i.PrimaryWeapon].Damage > 0, i.CanRank);
    }
}
