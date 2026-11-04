// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.M4.RulesTierFixture;

namespace Rts.Sim.Tests.M4;

/// <summary>M4-04: TierOf, tier-sugar gating (locks until the gate exists, re-locks when it dies, Q-M4-e option A) and upgrade-id
/// prerequisites on units.</summary>
public sealed class TierGatingTests
{
    [Fact]
    public void TierOf_Is1_2_3_AndFallsWhenTheGateDies()
    {
        Simulation sim = NewSim();
        Assert.Equal(1, sim.TierOf(0));
        EntityId g2 = Place(sim, 0, "test_gate2", 16, 4);
        Assert.Equal(2, sim.TierOf(0));
        Assert.Equal(1, sim.TierOf(1));                       // per player
        EntityId g3 = Place(sim, 0, "test_gate3", 20, 4);
        Assert.Equal(3, sim.TierOf(0));
        Assert.Equal(3, sim.ViewFor(0).Tier);
        Run(sim, 2, Kill(sim, g3));
        Assert.Equal(2, sim.TierOf(0));
        Run(sim, 2, Kill(sim, g2));
        Assert.Equal(1, sim.TierOf(0));
        Assert.Equal(1, sim.TierOf(99));                      // unknown slot
    }

    [Fact]
    public void TierSugar_LocksT2Types_UntilTheGate_AndRelocksWhenItDies()
    {
        Simulation sim = NewSim(("test_barracks", 8, 9), ("test_lab", 16, 9));
        int elite = Inf("test_elite"), bunker = Bld("test_bunker2"), upT2 = Up("test_up_t2");
        Assert.Contains("test_t2", Base.Infantry[elite].Prerequisites);
        Assert.True(sim.IsUnlocked(0, ProductionCategory.Infantry, Inf("test_rifleman")));
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Infantry, elite));
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Structure, bunker));
        Assert.False(sim.CanResearch(0, upT2));
        Assert.DoesNotContain((ProductionCategory.Infantry, elite), sim.HudFor(0).Buildable);

        EntityId gate = Place(sim, 0, "test_gate2", 20, 4);
        Assert.True(sim.IsUnlocked(0, ProductionCategory.Infantry, elite));
        Assert.True(sim.IsUnlocked(0, ProductionCategory.Structure, bunker));
        Assert.True(sim.CanResearch(0, upT2));
        Assert.Contains((ProductionCategory.Infantry, elite), sim.HudFor(0).Buildable);
        Assert.Contains(upT2, sim.HudFor(0).BuildableUpgrades);
        Assert.False(sim.IsUnlocked(1, ProductionCategory.Infantry, elite));   // the other player has no gate

        Run(sim, 2, Kill(sim, gate));
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Infantry, elite));
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Structure, bunker));
        Assert.False(sim.CanResearch(0, upT2));
    }

    [Fact]
    public void UpgradeIdPrerequisite_UnlocksAUnitAfterResearch()
    {
        Simulation sim = NewSim(("test_factory", 4, 9), ("test_lab", 16, 4));
        int secret = Veh("test_secret"), unlock = Up("test_up_unlock");
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Vehicle, secret));
        Run(sim, 1, Build(sim, ProductionCategory.Vehicle, secret));
        Assert.Equal(0, sim.Production.QueueLength(0, ProductionCategory.Vehicle));
        Run(sim, 1, Build(sim, ProductionCategory.Upgrade, unlock));
        for (int i = 0; i < 200 && !sim.HasUpgrade(0, unlock); i++) sim.Step(None);
        Assert.True(sim.HasUpgrade(0, unlock));
        Assert.True(sim.IsUnlocked(0, ProductionCategory.Vehicle, secret));
        Assert.False(sim.IsUnlocked(1, ProductionCategory.Vehicle, secret));
        Run(sim, 1, Build(sim, ProductionCategory.Vehicle, secret));
        Assert.Equal(1, sim.Production.QueueLength(0, ProductionCategory.Vehicle));
    }
}
