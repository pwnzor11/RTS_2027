// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests.Directorate;

/// <summary>M3-10: the Recruit AI plays the Directorate (build order, scraper economy, Bores into Hatches, tunnel transit, radiation care).</summary>
public class DirectorateRecruitTests(ITestOutputHelper log)
{
    private const int Ticks = 9000;
    private static MapData Map(ulong seed) => MapGenerator.Generate(48, seed);

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    public void DirectorateBuildsHarvestsDigsAndUsesTheNetwork(ulong seed)
    {
        DirectorateOutcome o = DirectorateMatch.Play(Map(seed), seed, Ticks);
        log.WriteLine($"seed {seed}: {o}");
        Assert.True(o.Structures >= 6, "a base: yard, vent, smelter, hatch, reactor, bore");
        Assert.True(o.Income > 0, "the scraper brought credits in");
        Assert.True(o.HatchesBuilt >= 1, "at least one Hatch was dug by a Bore");
        Assert.True(o.TransitUnits >= 1, "at least one unit rode the network");
        Assert.Equal(0, o.Unplaceable);
    }

    [Fact]
    public void SameSeedGivesTheSameGame()
    {
        DirectorateOutcome a = DirectorateMatch.Play(Map(1), 1, 5000);
        DirectorateOutcome b = DirectorateMatch.Play(Map(1), 1, 5000);
        Assert.Equal(a, b);
        Assert.NotEqual(0UL, a.Hash);
    }

    [Fact]
    public void RunnerAcceptsFactionsAndIsDeterministic()
    {
        MapData map = Map(2);
        var dir = new AiSide("recruit", TestWorld.Tiers[0], Faction.Directorate);
        var auth = new AiSide("recruit", TestWorld.Tiers[0]);
        AiGameResult a = AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 2, dir, auth, true, 4000);
        AiGameResult b = AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 2, dir, auth, true, 4000);
        Assert.Equal(a, b);
        Assert.Contains("/directorate", dir.Label);
    }

    /// <summary>Acceptance log: Directorate Recruit vs Authority Recruit, two seeds, both slot orders. No win-rate target.</summary>
    [Fact]
    public void WinRateLog()
    {
        int wins = 0, losses = 0, draws = 0;
        foreach (ulong seed in new ulong[] { 1, 2 })
            foreach (bool slot0 in new[] { true, false })
            {
                DirectorateOutcome o = DirectorateMatch.Play(Map(seed), seed, Ticks, dirInSlot0: slot0);
                log.WriteLine($"seed {seed} dirSlot0={slot0}: winner={(o.Winner == 0 ? "Directorate" : o.Winner == 1 ? "Authority" : "draw")} end={o.EndTick} hatches={o.HatchesBuilt} transit={o.TransitUnits} {o}");
                if (o.Winner == 0) wins++; else if (o.Winner == 1) losses++; else draws++;
            }
        log.WriteLine($"Directorate Recruit vs Authority Recruit: {wins} wins, {losses} losses, {draws} draws");
        Assert.Equal(4, wins + losses + draws);
    }

    [Fact]
    public void InfantryLeavesIrradiatedCells()
    {
        SimRules rules = TestWorld.Rules.Sim;
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(48, 48), 2, rules, TestWorld.Rules.RulesHash)
        { Factions = [Faction.Directorate, Faction.Authority] });
        SkirmishStart.Apply(sim);
        var driver = new AiDriver(1);
        var ai = new RecruitAi(TestWorld.Tiers[0]);
        driver.Add(0, ai, TestWorld.Tiers[0]);
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
        // The start soldiers first march to the rally cell (and the AI sends a wave at once while it owns no scraper), and only idle
        // infantry is moved off a hot cell. So let the AI settle: run until one of its soldiers stands idle, then contaminate its cell.
        int soldier = -1;
        Run(2500, () =>
        {
            if (sim.Tick < 600) return false;   // before this the soldiers have not yet been given their first order and only look idle
            var own = new List<Rts.Sim.View.ViewActor>();
            sim.ViewFor(0).CopyOwn(own, false);
            foreach (var a in own)
                if (a.Kind == ActorKind.Infantry && a.Idle) { soldier = a.Id.Index; return true; }
            return false;
        });
        Assert.True(soldier > 0, "a soldier of the AI stood idle at some point");
        int before = ai.RadiationMoves;
        CellCoord at = sim.Infantry.Position[soldier].ToCell();
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                var c = new CellCoord(at.X + dx, at.Y + dy);
                if (sim.Ground.Contains(c)) sim.Ground.Radiation[sim.Ground.IndexOf(c)] = 250;   // high enough to outlast the decay until the next decision
            }
        Run(300, () => ai.RadiationMoves > before);
        log.WriteLine($"radiation moves: {ai.RadiationMoves - before}");
        Assert.True(ai.RadiationMoves > before, "idle infantry on a hot cell was ordered to a clean one");
    }
}
