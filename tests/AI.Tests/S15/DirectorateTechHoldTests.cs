// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.AI.Recruit;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit;

namespace AI.Tests.S15;

/// <summary>S16 (Q-67): the Directorate at tiers 2-3 holds the Isotope Works and Rad Mortar until it has an early army.</summary>
public sealed class DirectorateTechHoldTests
{
    [Fact]
    public void OnlyTiersTwoThreeAndFiveHoldTheTech()
    {
        Assert.False(TierPlanner.HoldsDirectorateTech(1));
        Assert.True(TierPlanner.HoldsDirectorateTech(2));
        Assert.True(TierPlanner.HoldsDirectorateTech(3));
        Assert.False(TierPlanner.HoldsDirectorateTech(4));
        Assert.True(TierPlanner.HoldsDirectorateTech(5));   // S16 dir5
        Assert.Equal(TierPlanner.DirectorateTechArmy, TierPlanner.DirectorateTechArmyFor(3));
        Assert.Equal(TierPlanner.DirectorateTechArmyTier5, TierPlanner.DirectorateTechArmyFor(5));
        Assert.True(TierPlanner.DirectorateTechArmyTier5 > TierPlanner.DirectorateTechArmy);
        Assert.True(TierPlanner.IsDirectorateEarlyTech("dir_isotope_works"));
        Assert.True(TierPlanner.IsDirectorateEarlyTech("dir_rad_mortar"));
        Assert.False(TierPlanner.IsDirectorateEarlyTech("dir_pillbox"));
    }

    [Fact]
    public void ADirectorateTierThreeAiBuysNoTechBeforeItsArmyExists()
    {
        var rig = new TierRig(Faction.Directorate, TestWorld.Tiers[2]);
        int works = SimRules.IndexOf(rig.World.Sim.Buildings, "dir_isotope_works", b => b.Id);
        Assert.True(works >= 0);
        var own = new List<ViewActor>();
        int peak = 0;
        for (int i = 0; i < 60; i++)
        {
            rig.Run(100);
            rig.Sim.ViewFor(0).CopyOwn(own, false);
            int army = 0; bool tech = false;
            foreach (ViewActor a in own)
            {
                if (a.Kind == ActorKind.Building) { if (a.TypeIndex == works) tech = true; continue; }
                if (a.IsMobile && !(a.Kind == ActorKind.Vehicle && rig.World.Sim.Vehicles[a.TypeIndex].HarvestCapacity > 0)) army++;
            }
            peak = Math.Max(peak, army);
            if (tech)
            {
                Assert.True(peak >= TierPlanner.DirectorateTechArmy - 4, $"Isotope Works stands at tick {(i + 1) * 100} with a peak army of {peak}");
                return;
            }
        }
    }

    [Fact]
    public void ADirectorateTierFiveAiBuysNoTechBeforeItsArmyExists()
    {
        var rig = new TierRig(Faction.Directorate, TestWorld.Tiers[4]);
        int works = SimRules.IndexOf(rig.World.Sim.Buildings, "dir_isotope_works", b => b.Id);
        Assert.True(works >= 0);
        var own = new List<ViewActor>();
        int peak = 0;
        for (int i = 0; i < 60; i++)
        {
            rig.Run(100);
            rig.Sim.ViewFor(0).CopyOwn(own, false);
            int army = 0; bool tech = false;
            foreach (ViewActor a in own)
            {
                if (a.Kind == ActorKind.Building) { if (a.TypeIndex == works) tech = true; continue; }
                if (a.IsMobile && !(a.Kind == ActorKind.Vehicle && rig.World.Sim.Vehicles[a.TypeIndex].HarvestCapacity > 0)) army++;
            }
            peak = Math.Max(peak, army);
            if (tech)
            {
                Assert.True(peak >= TierPlanner.DirectorateTechArmyTier5 - 4, $"Isotope Works stands at tick {(i + 1) * 100} with a peak army of {peak}");
                return;
            }
        }
    }
}
