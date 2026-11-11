// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit, FAD-02 / FACTION-MATH-03: the Agitprop's aura (aura_rof_permille, 1250 until D-205, now 800) multiplies RELOAD TIME. Fixed: the
/// conscripts it escorts now fire faster. Shipped data, measured as damage dealt to a building in a fixed time.</summary>
public sealed class AgitpropAuraAuditTests
{
    private const int Ticks = 800;

    /// <summary>Six conscripts shoot an enemy conyard for <see cref="Ticks"/> ticks; returns the damage dealt and the applied aura value.</summary>
    private static (int Damage, int Rof) Measure(bool withAgitprop)
    {
        var w = new S18World(Faction.Directorate, Faction.Authority);
        EntityId yard = w.Place(1, "auth_conyard", 30, 30);
        var men = new List<EntityId>();
        for (int k = 0; k < 6; k++) men.Add(w.Inf(0, "dir_conscript", 28 + k % 2, 30 + k / 2, 0));
        if (withAgitprop) w.Veh(0, "dir_agitprop", 26, 31);
        int hp0 = w.BldHp(yard);
        w.Run(1, men.Select(m => w.Attack(0, ActorKind.Infantry, m, ActorKind.Building, yard)).ToArray());
        w.Run(Ticks - 1);
        return (hp0 - w.BldHp(yard), w.Sim.Effects.RofPermille(ActorKind.Infantry, men[0].Index));
    }

    [Fact]
    public void AgitpropNearby_ConscriptsReloadFaster_AndDealMoreDamage()
    {
        // D-205 / FAD-02: aura_rof_permille 1250 -> 800 (reload x0.8).
        VehicleType agit = S18World.RealRules.Vehicles.Single(v => v.Id == "dir_agitprop");
        Assert.Equal(800, agit.AuraRofPermille);
        Assert.Equal(1000, agit.AuraSpeedPermille);

        var (plain, rofPlain) = Measure(false);
        var (escorted, rofEscorted) = Measure(true);

        Assert.Equal(1000, rofPlain);
        Assert.Equal(800, rofEscorted);
        Assert.True(plain > 0);
        Assert.True(escorted > plain, $"escorted {escorted} vs plain {plain}");
    }

    [Fact]
    public void Desired_AgitpropNearby_ConscriptsDealMoreDamage()
    {
        var (plain, _) = Measure(false);
        var (escorted, rof) = Measure(true);
        Assert.True(rof < 1000);
        Assert.True(escorted > plain);
    }
}
