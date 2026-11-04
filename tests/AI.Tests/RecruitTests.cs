// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace AI.Tests;

public class RecruitTests
{
    private static readonly DifficultyProfile Recruit = TestWorld.Tiers[0];

    /// <summary>Wraps an AI and checks every Place command against the view at the moment it is issued.</summary>
    private sealed class CheckingAi(IAiPlayer inner) : IAiPlayer
    {
        public int Places, Illegal;
        public string Id => inner.Id;
        public void Start(AiContext ctx) => inner.Start(ctx);
        public void Think(PlayerView view, ICommandSink sink) => inner.Think(view, new Sink(this, view, sink));

        private sealed class Sink(CheckingAi owner, PlayerView view, ICommandSink real) : ICommandSink
        {
            public void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default,
                ActorRef target = default, int arg2 = 0) => real.Issue(kind, subject, subjectKind, cell, target, arg2);
            public void Build(ProductionCategory category, int typeIndex) => real.Build(category, typeIndex);
            public void Rally(EntityId factory, CellCoord cell) => real.Rally(factory, cell);
            public void Place(int buildingType, CellCoord topLeft)
            {
                owner.Places++;
                if (!view.CanPlace(buildingType, topLeft)) owner.Illegal++;
                real.Place(buildingType, topLeft);
            }
        }
    }

    private static Simulation EconomySim(MapData map, ulong seed = 1)
    {
        var sim = new Simulation(new SimSetup(seed, map, 2, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash));
        SkirmishStart.Apply(sim);
        return sim;
    }

    private static int Count(Simulation sim, byte player, string id)
    {
        int type = SimRules.IndexOf(sim.Setup.Rules.Buildings, id, b => b.Id), n = 0;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player && sim.Buildings.TypeIndex[i] == type) n++;
        return n;
    }

    private static void Run(Simulation sim, AiDriver driver, int ticks, Action<int>? each = null)
    {
        var pending = new List<Command>();
        for (int t = 0; t < ticks; t++)
        {
            driver.Collect(sim, pending);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
            each?.Invoke(t);
        }
    }

    [Fact]
    public void BuildOrderProgressesOnAFlatMap()
    {
        Simulation sim = EconomySim(MapData.CreateFlat(64, 64));
        var driver = new AiDriver(1);
        driver.Add(0, new RecruitAi(Recruit), Recruit);
        int power = -1, refinery = -1, barracks = -1;
        Run(sim, driver, 3000, t =>
        {
            if (power < 0 && Count(sim, 0, "auth_power") >= 1) power = t;
            if (refinery < 0 && Count(sim, 0, "auth_refinery") >= 1) refinery = t;
            if (barracks < 0 && Count(sim, 0, "auth_barracks") >= 1) barracks = t;
        });
        Assert.InRange(power, 0, 700);          // a power plant first, well within the opening
        Assert.True(refinery > power, "refinery comes after the first power plant");
        Assert.InRange(refinery, 0, 1800);
        Assert.True(barracks > refinery, "barracks follows the refinery");
        Assert.Equal(0, Count(sim, 1, "auth_power"));   // the other slot has no AI
    }

    [Fact]
    public void FullOrderIsBuiltAndLanesStayFree()
    {
        Simulation sim = EconomySim(Rts.Data.MapGenerator.Generate(64, 1));
        var driver = new AiDriver(1);
        driver.Add(0, new RecruitAi(Recruit), Recruit);
        Run(sim, driver, 6000);
        foreach (string id in new[] { "auth_refinery", "auth_barracks", "auth_factory", "auth_silo", "auth_turret" })
            Assert.True(Count(sim, 0, id) >= 1, id + " was not built");
        Assert.True(Count(sim, 0, "auth_power") >= 2);

        // the cell just outside every producer's exit and the refinery dock holds no building
        SimRules rules = sim.Setup.Rules;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
        {
            if (!sim.Buildings.Slots.IsIndexAlive(i) || sim.Buildings.Player[i] != 0) continue;
            BuildingType bt = rules.Buildings[sim.Buildings.TypeIndex[i]];
            CellCoord c = sim.Buildings.Cell[i];
            if (bt.Produces != null)
                Assert.True(sim.Map.BuildingAt(new CellCoord(c.X + bt.Exit.X, c.Y + bt.Exit.Y)).IsNone, bt.Id + " exit is blocked");
            if (bt.Refinery)
                Assert.True(sim.Map.BuildingAt(new CellCoord(c.X + bt.Dock.X, c.Y + bt.Dock.Y)).IsNone, bt.Id + " dock is blocked");
        }
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void PlacementNeverViolatesCanPlace(ulong seed)
    {
        Simulation sim = EconomySim(Rts.Data.MapGenerator.Generate(64, seed), seed);
        var ai = new RecruitAi(Recruit);
        var checking = new CheckingAi(ai);
        var driver = new AiDriver(seed);
        driver.Add(0, checking, Recruit);
        Run(sim, driver, 5000);
        Assert.True(checking.Places >= 7, $"only {checking.Places} Place commands");
        Assert.Equal(0, checking.Illegal);
        Assert.Equal(0, ai.Unplaceable);
    }

    [Fact]
    public void ScraperWhoseHomeFieldRunsOutIsSentToLookElsewhere()
    {
        // seed 8: the only field within 16 cells of the base is small and built over; the next one is 20+ cells away
        Simulation sim = EconomySim(Rts.Data.MapGenerator.Generate(64, 8), 8);
        var ai = new RecruitAi(Recruit);
        var driver = new AiDriver(8);
        driver.Add(0, ai, Recruit);
        Run(sim, driver, 6000);
        // M2-19: the sim's own map-wide field search now finds the next field, so the AI's probe orders are no longer
        // needed; what matters is that the economy does not stay dead.
        Assert.True(ai.UnitsQueued >= 10, $"only {ai.UnitsQueued} units were queued: the economy stayed dead");
    }

    [Fact]
    public void RecruitBeatsIdleOnAFixedSeed()
    {
        MapData map = Rts.Data.MapGenerator.Generate(64, 1);
        AiGameResult r = AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 1,
            new AiSide("recruit", Recruit), new AiSide("idle", TestWorld.Tiers[0]), true, 16000);
        Assert.Equal(0, r.Winner);
        Assert.True(r.EndTick < 16000);
        Assert.True(r.StatsA.Structures >= 7);
        Assert.Equal(1, r.StatsB.Structures);
        Assert.Equal(0, r.StatsB.Units);
    }

    [Fact]
    public void SameSeedGivesSameGameAndHash()
    {
        MapData map = Rts.Data.MapGenerator.Generate(64, 5);
        AiGameResult Play() => AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 5,
            new AiSide("recruit", Recruit), new AiSide("recruit", Recruit), true, 4000);
        AiGameResult a = Play(), b = Play();
        Assert.Equal(a, b);
        Assert.NotEqual(0UL, a.FinalHash);
    }

    [Fact]
    public void RecruitIsRegisteredAndKeepsTheSkirmisher()
    {
        Assert.Contains("recruit", AiRegistry.Ids);
        Assert.Contains("skirmisher", AiRegistry.Ids);
        Assert.True(AiRegistry.TryCreate("recruit", Recruit, out IAiPlayer? ai));
        Assert.Equal("recruit", ai!.Id);
    }
}
