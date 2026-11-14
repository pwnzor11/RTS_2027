// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Xunit;

namespace AI.Tests.S24;

/// <summary>S24 (M5-12): a strong anti-armour / anti-infantry directive makes the production manager pick the unit with the best counter score.</summary>
public sealed class CounterMixTests
{
    private static SimRules Rules => TestWorld.Rules.Sim;

    private static (ProductionManager Pm, RecruitKnowledge K, int[] Types) Rig(byte mix)
    {
        var faction = RecruitFaction.For(Faction.Authority);
        var pm = new ProductionManager(faction, null);
        pm.Start(new AiRandom(1, 0));
        pm.Bias = DirectiveBias.Plain with { Mix = mix, MixPermille = 1000 };
        var buildable = new List<(ProductionCategory, int)>();
        var types = new List<int>();
        for (int i = 0; i < Rules.Infantry.Count; i++)
            if (Rules.Infantry[i].Faction == Faction.Authority && Rules.Infantry[i].Cost > 0) { buildable.Add((ProductionCategory.Infantry, i)); types.Add(i); }
        var k = new RecruitKnowledge { Rules = Rules, Hud = new PlayerHud(0, 0, 5000, 100, 50, Array.Empty<QueueView>(), buildable, false) };
        return (pm, k, types.ToArray());
    }

    private static long Score(ProductionManager pm, int type) =>
        pm.Bias.CounterScore(Rules, Rules.Infantry[type].PrimaryWeapon, Rules.Infantry[type].Cost);

    [Theory]
    [InlineData(DirectiveCodec.MixAntiArmour)]
    [InlineData(DirectiveCodec.MixAntiInfantry)]
    public void StrongCounterMixPicksTheBestCounterUnit(byte mix)
    {
        var (pm, k, types) = Rig(mix);
        long best = 0;
        foreach (int t in types) best = Math.Max(best, Score(pm, t));
        Assert.True(best > 0);
        int pick = pm.PreferMix(k, ProductionCategory.Infantry, types, types[0]);
        Assert.Equal(best, Score(pm, pick));
    }

    [Fact]
    public void TheTwoCounterMixesPickDifferentUnits()
    {
        var (a, ka, types) = Rig(DirectiveCodec.MixAntiArmour);
        var (b, kb, _) = Rig(DirectiveCodec.MixAntiInfantry);
        Assert.NotEqual(a.PreferMix(ka, ProductionCategory.Infantry, types, types[0]), b.PreferMix(kb, ProductionCategory.Infantry, types, types[0]));
    }
}
