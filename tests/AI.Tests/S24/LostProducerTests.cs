// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Xunit;

namespace AI.Tests.S24;

/// <summary>s24 (M13-23, D-242): a broke side rebuilds a lost unit producer it can pay for.</summary>
public sealed class LostProducerTests
{
    private static SimRules Rules => TestWorld.Rules.Sim;
    private static int B(string id) => SimRules.IndexOf(Rules.Buildings, id, b => b.Id);

    private static RecruitKnowledge K(int barracks, int factory, params string[] buildable)
    {
        var list = new List<(ProductionCategory, int)>();
        foreach (string id in buildable) list.Add((Rules.Buildings[B(id)].Category, B(id)));
        var k = new RecruitKnowledge { Rules = Rules, Hud = new PlayerHud(0, 0, 5000, 100, 50, Array.Empty<QueueView>(), list, false) };
        k.BuildingCount = new int[Rules.Buildings.Count];
        k.BuildingCount[B("auth_barracks")] = barracks;
        k.BuildingCount[B("auth_factory")] = factory;
        return k;
    }

    [Fact]
    public void LostFactoryIsRebuiltWhenAffordableAndBuildable()
    {
        int cost = Rules.Buildings[B("auth_factory")].Cost;
        Assert.Equal(B("auth_factory"), BuildOrderManager.LostProducer(K(1, 0, "auth_factory"), "auth_barracks", "auth_factory", cost));
        Assert.Equal(-1, BuildOrderManager.LostProducer(K(1, 0, "auth_factory"), "auth_barracks", "auth_factory", cost - 1));
        Assert.Equal(-1, BuildOrderManager.LostProducer(K(1, 0), "auth_barracks", "auth_factory", cost));
    }

    [Fact]
    public void BarracksComesFirstAndNothingIsRebuiltWhenBothStand()
    {
        Assert.Equal(B("auth_barracks"), BuildOrderManager.LostProducer(K(0, 0, "auth_barracks", "auth_factory"), "auth_barracks", "auth_factory", 5000));
        Assert.Equal(-1, BuildOrderManager.LostProducer(K(1, 1, "auth_barracks", "auth_factory"), "auth_barracks", "auth_factory", 5000));
    }
}
