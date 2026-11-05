// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Rules;

namespace Rts.Sim.Tests.M7;

/// <summary>Simfu-3: the effect attributes <c>shipment_interval</c> (target class:player) and <c>harvest_capacity</c>, and the two
/// Expedition upgrades that use them.</summary>
public sealed class ExpeditionEffectTests
{
    private static SimRules LoadReal()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        return RulesLoader.Load(dir!).Sim;
    }

    [Fact]
    public void FastCharging_ShortensTheShipmentInterval_ForItsOwnerOnly()
    {
        SimRules s = LoadReal();
        int up = s.UpgradeIndex("exp_up_fast_charging");
        SimRules d = RulesDeriver.Derive(s, 1UL << up);
        Assert.Equal(s.Shipments.IntervalTicks * 850 / 1000, d.Shipments.IntervalTicks);
        Assert.Equal(s.Shipments.Value, d.Shipments.Value);
        Assert.Same(s, RulesDeriver.Derive(s, 0));
    }

    [Fact]
    public void DeepSurvey_RaisesTheSkimmerHopper_AndNothingElse()
    {
        SimRules s = LoadReal();
        SimRules d = RulesDeriver.Derive(s, 1UL << s.UpgradeIndex("exp_up_deep_survey"));
        int skimmer = SimRules.IndexOf(s.Vehicles, "exp_skimmer", v => v.Id);
        Assert.Equal(s.Vehicles[skimmer].HarvestCapacity + 2, d.Vehicles[skimmer].HarvestCapacity);
        for (int i = 0; i < s.Vehicles.Count; i++)
            if (i != skimmer) Assert.Equal(s.Vehicles[i].HarvestCapacity, d.Vehicles[i].HarvestCapacity);
    }

    [Fact]
    public void HarvestCapacityEffect_NeverSwitchesAHarvesterOff()
    {
        SimRules s = LoadReal();
        int skimmer = SimRules.IndexOf(s.Vehicles, "exp_skimmer", v => v.Id);
        var effects = new List<EffectDef>(s.Effects) { new("zz_fx", "set", "exp_skimmer", "harvest_capacity", 0) };
        var ups = new List<UpgradeType>(s.Upgrades) { new UpgradeType("zz_up", Faction.Expedition, 1, 1, Array.Empty<string>(), "") with { Effects = new[] { "zz_fx" } } };
        SimRules r = s with { Effects = effects.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray(), Upgrades = ups.OrderBy(u => u.Id, StringComparer.Ordinal).ToArray() };
        SimRules d = RulesDeriver.Derive(r, 1UL << r.UpgradeIndex("zz_up"));
        Assert.Equal(1, d.Vehicles[skimmer].HarvestCapacity);
    }

    [Theory]
    [InlineData("class:player", "max_health")]
    [InlineData("class:vehicle", "shipment_interval")]
    public void WrongTargetAttrPairs_AreLoadErrors(string target, string attr)
    {
        using var temp = new TempRules(("techs.toml",
            TempRules.Real("techs.toml") + $"\n[[effect]]\nid = \"test_bad\"\nop = \"add\"\ntarget = \"{target}\"\nattr = \"{attr}\"\nvalue = 1\n"));
        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));
        Assert.Contains(ex.Problems, p => p.Key == "test_bad.attr");
    }
}
