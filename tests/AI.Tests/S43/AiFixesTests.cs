// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit;

namespace AI.Tests.S43;

/// <summary>s43 (D-311): the three owner-approved AI fixes (switches in <see cref="AiFixes"/>).</summary>
public sealed class AiFixesTests
{
    private static int Count(Simulation sim, byte player, int type)
    {
        int n = 0;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player && sim.Buildings.TypeIndex[i] == type) n++;
        return n;
    }

    private static int Harvesters(Simulation sim, byte player, int type)
    {
        int n = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player && sim.Units.TypeIndex[i] == type) n++;
        return n;
    }

    private static int Total(Simulation sim, byte player)
    {
        int n = 0;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player) n++;
        return n;
    }

    [Fact]
    public void ABrokeAiWhoseRefineryDiedWithALiveHarvesterRebuildsTheRefinery()
    {
        SimRules rules = TestWorld.Rules.Sim;
        var sim = new Simulation(new SimSetup(1, MapGenerator.Generate(64, 1), 2, rules, TestWorld.Rules.RulesHash) { AllowDebug = true });
        SkirmishStart.Apply(sim);
        DifficultyProfile profile = TestWorld.Tiers[2];
        var driver = new AiDriver(1);
        driver.Add(0, new RecruitAi(profile), profile);
        int refinery = SimRules.IndexOf(rules.Buildings, "auth_refinery", b => b.Id);
        int scraper = SimRules.IndexOf(rules.Vehicles, "auth_scraper", v => v.Id);
        ushort seq = 1;
        var pending = new List<Command>();
        void Run(int ticks, Func<bool>? until = null)
        {
            for (int t = 0; t < ticks; t++)
            {
                driver.Collect(sim, pending);
                sim.Step(pending);
                pending.RemoveAll(c => c.ExecTick < sim.Tick);
                if (until != null && until()) return;
            }
        }
        Run(14000, () => Count(sim, 0, refinery) > 0 && Harvesters(sim, 0, scraper) > 0 && Total(sim, 0) >= 7);
        Assert.True(Count(sim, 0, refinery) > 0 && Harvesters(sim, 0, scraper) > 0, "the AI built a refinery and owns a harvester");
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == 0 && sim.Buildings.TypeIndex[i] == refinery)
                pending.Add(new Command(sim.Tick, 0, seq++, CommandKind.Debug, sim.Buildings.Slots.IdOf(i), default, default, (int)DebugOp.Kill, (int)ActorKind.Building));
        pending.Add(new Command(sim.Tick, 0, seq++, CommandKind.Debug, EntityId.None, default, default, (int)DebugOp.SetCash, 1500));
        Run(2);
        Assert.Equal(0, Count(sim, 0, refinery));
        Assert.True(Harvesters(sim, 0, scraper) > 0, "the harvester survives");
        Run(12000, () => Count(sim, 0, refinery) > 0);
        Assert.True(Count(sim, 0, refinery) > 0, "the broke AI rebuilt its refinery");
    }

    [Fact]
    public void MopupDoesNotNeedRespondToAttacksFromTierTwo()
    {
        foreach (Faction f in new[] { Faction.Authority, Faction.Expedition })
            for (int tier = 1; tier <= 5; tier++)
            {
                DifficultyProfile p = TestWorld.LoadedTiers[tier - 1].ForFaction((int)f);
                MilitaryOptions o = MilitaryOptions.From(p);
                Assert.Equal(tier >= 2, o.Mopup);
                if (f == Faction.Authority && tier >= 2) Assert.False(o.Respond);   // respond_to_attacks itself is unchanged
                if (f == Faction.Expedition && tier >= 4) Assert.False(o.Respond);
                Assert.Equal(tier >= 2, new MilitaryManager(8) { Options = o }.MopupOn);
            }
        Assert.False(new MilitaryManager(8) { Options = MilitaryOptions.From(TestWorld.LoadedTiers[3].ForFaction((int)Faction.Authority).WithMopup(false)) }.MopupOn);
    }

    [Fact]
    public void TheDirectorateBuildsAnAntiAirStructure()
    {
        Assert.Equal("dir_rocket_nest", new DirectorateRecruitFaction().AntiAirId);
        Assert.True(SimRules.IndexOf(TestWorld.Rules.Sim.Buildings, "dir_rocket_nest", b => b.Id) >= 0);
    }
}
