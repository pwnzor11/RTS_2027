// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.M3.Systems.SystemsFixture;

namespace Rts.Sim.Tests.M3.Systems;

/// <summary>M3-06 upgrade research: cost, time, cancel refund, prerequisites, faction gating, HUD list.</summary>
public sealed class UpgradeTests
{
    private const int A = 0, B = 1, X = 2;

    private static Simulation WithLab(IReadOnlyList<Faction>? factions = null, int cash = 2000, bool lab = true)
    {
        Simulation sim = NewSim(factions: factions);
        sim.PlaceBuilding(0, Hq, new CellCoord(4, 4));
        if (lab) sim.PlaceBuilding(0, Lab, new CellCoord(8, 4));
        sim.Players.Cash[0] = cash;
        return sim;
    }

    [Fact]
    public void Research_ChargesCostAndSetsTheBitOnCompletion()
    {
        Simulation sim = WithLab();
        Run(sim, 1, BuildUpgrade(sim, A));
        Assert.Equal(1, sim.Production.QueueLength(0, ProductionCategory.Upgrade));
        int steps = 1;
        while (!sim.HasUpgrade(0, A) && steps < 300) { sim.Step(None); steps++; }
        Assert.True(sim.HasUpgrade(0, A));
        Assert.InRange(steps, 99, 102);                       // build_time 100 at full power
        Assert.Equal(1500, sim.Players.Cash[0]);              // exactly the cost, once
        Assert.Equal(0, sim.Production.QueueLength(0, ProductionCategory.Upgrade));
        Assert.Equal(1UL << A, sim.HudFor(0).Upgrades);
        Run(sim, 20);
        Assert.Equal(1500, sim.Players.Cash[0]);
    }

    [Fact]
    public void Cancel_RefundsWhatWasDrained()
    {
        Simulation sim = WithLab();
        Run(sim, 40, BuildUpgrade(sim, A));
        Assert.InRange(sim.Players.Cash[0], 1, 1999);         // some, not all, drained
        Run(sim, 1, CancelUpgrade(sim, 0));
        Assert.Equal(2000, sim.Players.Cash[0]);
        Assert.Equal(0, sim.Production.QueueLength(0, ProductionCategory.Upgrade));
        Run(sim, 200);
        Assert.False(sim.HasUpgrade(0, A));
    }

    [Fact]
    public void Prerequisite_UpgradeMustBeOwnedFirst()
    {
        Simulation sim = WithLab();
        Assert.False(sim.CanResearch(0, B));
        Run(sim, 1, BuildUpgrade(sim, B));
        Assert.Equal(0, sim.Production.QueueLength(0, ProductionCategory.Upgrade));
        Run(sim, 1, BuildUpgrade(sim, A));
        while (!sim.HasUpgrade(0, A)) sim.Step(None);
        Assert.True(sim.CanResearch(0, B));
        Run(sim, 1, BuildUpgrade(sim, B));
        while (!sim.HasUpgrade(0, B)) sim.Step(None);
        Assert.Equal(2000 - 500 - 300, sim.Players.Cash[0]);
    }

    [Fact]
    public void Faction_GatesWhoMayResearch()
    {
        Simulation authority = WithLab();
        Assert.False(authority.CanResearch(0, X));
        Run(authority, 1, BuildUpgrade(authority, X));
        Assert.Equal(0, authority.Production.QueueLength(0, ProductionCategory.Upgrade));

        Simulation directorate = WithLab(new[] { Faction.Directorate, Faction.Authority });
        Assert.True(directorate.CanResearch(0, X));
        Assert.False(directorate.CanResearch(0, A));
    }

    [Fact]
    public void NeedsAResearchBuilding_NotOwnedTwice_NotQueuedTwice()
    {
        Simulation none = WithLab(lab: false);
        Assert.False(none.CanResearch(0, A));
        Simulation sim = WithLab();
        Run(sim, 1, BuildUpgrade(sim, A), BuildUpgrade(sim, A));
        Assert.Equal(1, sim.Production.QueueLength(0, ProductionCategory.Upgrade));
        while (!sim.HasUpgrade(0, A)) sim.Step(None);
        Assert.False(sim.CanResearch(0, A));
        Assert.False(sim.CanResearch(1, A));                  // player 1 has no lab
        Assert.False(sim.CanResearch(0, 99));
    }

    [Fact]
    public void Hud_BuildableUpgrades_ReflectsCanResearch()
    {
        Simulation sim = WithLab();
        Assert.Equal(new[] { A }, sim.HudFor(0).BuildableUpgrades);
        Assert.Empty(sim.HudFor(1).BuildableUpgrades);
        Run(sim, 1, BuildUpgrade(sim, A));
        Assert.Empty(sim.HudFor(0).BuildableUpgrades);        // queued: not offered again, B still locked
        while (!sim.HasUpgrade(0, A)) sim.Step(None);
        Assert.Equal(new[] { B }, sim.HudFor(0).BuildableUpgrades);
    }

    [Fact]
    public void Research_PausesWithoutALab_AndStallsWithoutCash()
    {
        Simulation sim = WithLab(cash: 100);
        Run(sim, 60, BuildUpgrade(sim, A));
        Assert.True(sim.Production.IsStalled(0, ProductionCategory.Upgrade));
        Assert.False(sim.HasUpgrade(0, A));
        Assert.Equal(0, sim.Players.Cash[0]);
        sim.Players.Cash[0] = 5000;
        while (!sim.HasUpgrade(0, A)) sim.Step(None);
        Assert.Equal(4600, sim.Players.Cash[0]);              // 100 already drained + 400 more = 500 in total
    }
}
