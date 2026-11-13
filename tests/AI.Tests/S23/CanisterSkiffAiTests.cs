// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.M7;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests.S23;

/// <summary>s23 (Q-80): the Expedition's tier-2 anti-infantry hover, the Canister Skiff (working id exp_canister_skiff), and the AI that builds it.</summary>
public sealed class CanisterSkiffAiTests(ITestOutputHelper log)
{
    private const string Id = "exp_canister_skiff";

    [Fact]
    public void TheSkiffExistsAsATierTwoHoverWithSplashAgainstInfantry()
    {
        SimRules rules = TestWorld.Rules.Sim;
        VehicleType v = rules.Vehicles.Single(x => x.Id == Id);
        Assert.Equal(Faction.Expedition, v.Faction);
        Assert.Equal(2, v.Tier);
        Assert.Equal(Locomotion.Hover, v.Locomotion);
        Assert.Equal(rules.Vehicles.Single(x => x.Id == "exp_glide_tank").Prerequisites, v.Prerequisites);
        WeaponType w = rules.Weapons[v.PrimaryWeapon];
        WarheadType wh = rules.Warheads[w.Warhead];
        Assert.True(wh.Splash > 0 && !wh.HitsAllies);
        Assert.Equal(100, wh.VersusPercent[(int)ArmorClass.Unarmored]);
        Assert.True(wh.VersusPercent[(int)ArmorClass.Heavy] <= 15 && wh.VersusPercent[(int)ArmorClass.Structure] <= 10);
    }

    [Fact]
    public void OnlyTheHigherTiersWantSkiffs()
    {
        Assert.Equal(0, ExpeditionPlan.For(1).Skiffs);
        for (int t = 2; t <= 5; t++) Assert.True(ExpeditionPlan.For(t).Skiffs > 0);
    }

    [Fact]
    public void TheExpeditionAiBuildsSkiffsAgainstAnInfantryHeavyEnemy()
    {
        // Left Behind opens with a Scavenger swarm: the enemy mix seen is mostly infantry, so the special order for a skiff comes up.
        var rig = new ExpeditionRig(TestWorld.Tiers[2], Faction.LeftBehind, enemyProfile: TestWorld.Tiers[1]);
        int skiffType = SimRules.IndexOf(TestWorld.Rules.Sim.Vehicles, Id, v => v.Id);
        int peak = 0;
        rig.Run(30000, () =>
        {
            if (rig.Sim.Tick % 100 != 0) return false;
            int n = 0;
            for (int i = 1; i < rig.Sim.Units.Capacity; i++)
                if (rig.Sim.Units.Slots.IsIndexAlive(i) && rig.Sim.Units.Player[i] == 0 && rig.Sim.Units.TypeIndex[i] == skiffType) n++;
            peak = Math.Max(peak, n);
            return peak >= 1;
        });
        log.WriteLine($"tick {rig.Sim.Tick}: peak skiffs {peak}");
        Assert.True(peak >= 1, "the Expedition AI built a Canister Skiff against infantry");
    }
}
