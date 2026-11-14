// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.M4;
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace AI.Tests.S24;

/// <summary>S24 (M5-12, D-241): scripted games (fixed seed) where player 0 plays the tiered AI and gets one economy commander order at tick 2;
/// players 1 and 2 only hold their bases. Each order kind changes what the AI does; with no order the AI is the plain one.</summary>
public sealed class EconomyOrdersTests(ITestOutputHelper log)
{
    private sealed record Outcome(int Tier2Tick, int Tier3Tick, int Refineries, CellCoord[] RefineryCells, int MinCash, int Units, CellCoord Yard);

    private Outcome Play(int tier, CommanderOrderKind? kind, int amount, CellCoord? cell, int ticks, int cash = 0, int minFrom = 400, bool relative = false)
    {
        DifficultyProfile profile = TestWorld.Tiers[tier - 1];
        MapData map = M4AiTests.QuadMap(48, 3);
        var sim = new Simulation(new SimSetup(3, map, 3, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash) { Factions = [Faction.Authority, Faction.Authority, Faction.Authority] });
        SkirmishStart.Apply(sim);
        if (cash > 0) sim.Players.Cash[0] = cash;
        var driver = new AiDriver(3);
        var ai = new RecruitAi(profile);
        driver.Add(0, ai, profile);
        driver.Add(1, new IdleAi(), profile);
        driver.Add(2, new IdleAi(), profile);
        var rules = TestWorld.Rules.Sim;
        int refinery = SimRules.IndexOf(rules.Buildings, "auth_refinery", b => b.Id);
        var pending = new List<Command>();
        int minCash = int.MaxValue;
        CellCoord yard = default;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == 0) { yard = sim.Buildings.Cell[i]; break; }
        for (int t = 0; t < ticks; t++)
        {
            if (t == 30 && kind is CommanderOrderKind k)
            {
                CellCoord c = cell ?? Directive.NoCell;
                if (relative) c = new CellCoord(Math.Clamp(yard.X + c.X, 1, 46), Math.Clamp(yard.Y + c.Y, 1, 46));
                pending.Add(CommanderOrderCodec.ToCommand(sim.Tick + 1, 0, 1, k, 0, c, amount, 0));
            }
            driver.Collect(sim, pending);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
            if (t % 100 == 0 && t >= 2500 && kind is not null) { var h = sim.ViewFor(0).Hud; log.WriteLine($"t{t} cash {sim.Players.Cash[0]} " + string.Join(" | ", h.Queues.Select(q => q.Category + ":" + string.Join(",", q.Items.Select(i => rules.Buildings.Count > i.TypeIndex ? i.TypeIndex + "/" + i.ProgressPermille + (i.Stalled ? "S" : "") : "?")) + " ready " + q.ReadyTypeIndex))); }
            if (t >= minFrom) minCash = Math.Min(minCash, sim.Players.Cash[0]);
        }
        log.WriteLine("orders: " + string.Join(" ", sim.ViewFor(0).CommanderOrders.Select(o => o.Kind + "@" + o.Cell + "#" + o.IssuedTick)));
        var owned = new List<string>();
        for (int i = 1; i < sim.Buildings.Capacity; i++) if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == 0) owned.Add(rules.Buildings[sim.Buildings.TypeIndex[i]].Id);
        log.WriteLine("t10=" + rules.Buildings[10].Id + " ref=" + refinery); log.WriteLine("owned: " + string.Join(" ", owned) + " cash " + sim.Players.Cash[0]);
        var cells = new List<CellCoord>();
        int units = 0;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == 0 && sim.Buildings.TypeIndex[i] == refinery) cells.Add(sim.Buildings.Cell[i]);
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == 0) units++;
        log.WriteLine($"tier{tier} order={(kind is null ? "none" : kind.ToString())} amount={amount} tier2={ai.Tier2Tick} tier3={ai.Tier3Tick} refineries=[{string.Join(' ', cells)}] minCash={minCash} units={units} yard={yard}");
        return new Outcome(ai.Tier2Tick, ai.Tier3Tick, cells.Count, cells.ToArray(), minCash, units, yard);
    }

    private static int Dist(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    [Fact]
    public void TechToBringsTheTierGatesForward()
    {
        Outcome plain = Play(2, null, 0, null, 4000);
        Outcome ordered = Play(2, CommanderOrderKind.TechTo, 3, null, 4000);
        int p2 = plain.Tier2Tick < 0 ? int.MaxValue : plain.Tier2Tick;
        Assert.True(ordered.Tier2Tick >= 0, "the tier-2 gate is built under a TechTo order");
        Assert.True(ordered.Tier2Tick < p2, $"tier 2 earlier: {ordered.Tier2Tick} vs plain {plain.Tier2Tick}");
    }

    [Fact]
    public void ReserveKeepsTheCreditsUntouched()
    {
        Outcome plain = Play(2, null, 0, null, 3000, cash: 6000);
        Outcome ordered = Play(2, CommanderOrderKind.Reserve, 2500, null, 3000, cash: 6000);
        Assert.True(plain.MinCash < 2000, $"the plain AI spends below 2000 (min {plain.MinCash})");
        Assert.True(ordered.MinCash >= 2500 - 150, $"the ordered AI keeps about 2500 (min {ordered.MinCash})");
    }

    [Fact]
    public void ExpandToPlacesTheNewRefineryNearTheOrderCell()
    {
        var aim = new CellCoord(5, 5);   // relative to the yard
        Outcome plain = Play(2, null, 0, null, 4000, cash: 4000);
        Outcome ordered = Play(2, CommanderOrderKind.ExpandTo, 0, aim, 4000, cash: 4000, relative: true);
        Assert.True(ordered.Refineries >= 2, "an expansion refinery stands");
        var target = new CellCoord(Math.Clamp(ordered.Yard.X + aim.X, 1, 46), Math.Clamp(ordered.Yard.Y + aim.Y, 1, 46));
        int best = int.MaxValue;
        foreach (CellCoord c in ordered.RefineryCells) best = Math.Min(best, Dist(c, target));
        int plainBest = int.MaxValue;
        foreach (CellCoord c in plain.RefineryCells) plainBest = Math.Min(plainBest, Dist(c, target));
        Assert.True(best <= plainBest, $"nearest refinery to the order cell: {best} vs plain {plainBest}");
        Assert.True(ordered.Refineries >= plain.Refineries);
    }
}
