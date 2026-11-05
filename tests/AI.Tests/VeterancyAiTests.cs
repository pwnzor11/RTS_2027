// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.AI;
using Rts.Data;
using Rts.AI.Recruit;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit;

namespace AI.Tests;

/// <summary>VET-6 (D-127, design 14 section 6): the Recruit AI values, retreats from and targets ranked units.</summary>
public sealed class VeterancyAiTests
{
    [Theory]
    [InlineData(800, 0, 800)]
    [InlineData(800, 1, 1000)]
    [InlineData(800, 2, 1200)]
    [InlineData(800, 3, 1400)]
    [InlineData(1, 3, 1)]      // integer division rounds down
    [InlineData(7, 1, 8)]
    [InlineData(0, 3, 0)]
    public void ValueFollowsTheFormula(int cost, int rank, int expected)
        => Assert.Equal(expected, VeterancyValue.Of(cost, rank));

    [Fact]
    public void RankIsClamped()
    {
        Assert.Equal(VeterancyValue.Of(500, 0), VeterancyValue.Of(500, -4));
        Assert.Equal(VeterancyValue.Of(500, 3), VeterancyValue.Of(500, 9));
    }

    [Fact]
    public void LargeCostsDoNotOverflow()
        => Assert.Equal(1_750_000_000, VeterancyValue.Of(1_000_000_000, 3));

    // ---- helpers ---------------------------------------------------------------------------------

    private static DifficultyProfile Tier(int tier) => TestWorld.Tiers[tier - 1];

    private static ViewActor Unit(int slot, int rank, int health, int max = 100, int x = 10, int y = 10, Relation rel = Relation.Self, ActorKind kind = ActorKind.Infantry)
        => new(new EntityId((ushort)slot, 1), kind, (byte)(rel == Relation.Self ? 0 : 1), rel, 0, new CellCoord(x, y), new LeptonCoord(x * 256, y * 256),
            health, max, MissionKind.Guard, true, EntityId.None, false, 1, 1) { Rank = rank };

    private sealed class Log : ICommandSink
    {
        public readonly List<(CommandKind Kind, EntityId Subject, CellCoord Cell, ActorRef Target)> Orders = new();
        public void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default, ActorRef target = default, int arg2 = 0)
            => Orders.Add((kind, subject, cell, target));
        public void Build(ProductionCategory category, int typeIndex) { }
        public void Place(int buildingType, CellCoord topLeft) { }
        public void Rally(EntityId factory, CellCoord cell) { }
    }

    private static RecruitKnowledge Know(params ViewActor[] army)
    {
        var rig = new TierRig(Faction.Authority, Tier(3));
        rig.Run(2);
        var k = new RecruitKnowledge { View = rig.Sim.ViewFor(0), Rules = TestWorld.Rules.Sim };
        k.Army.AddRange(army);
        k.Own.AddRange(army);
        return k;
    }

    // ---- (b) retreat -----------------------------------------------------------------------------

    [Fact]
    public void ARank3UnitRetreatsAtAHigherHealthThanARank0Unit()
    {
        // 55 % health with a 50 % profile: a recruit fights on, a rank-3 unit (threshold 80 %) leaves
        ViewActor recruit = Unit(1, 0, 55), hero = Unit(2, 3, 55);
        RecruitKnowledge k = Know(recruit, hero);
        var squads = new Squads(); var sink = new Log();
        new RetreatManager().Think(k, sink, squads, new CellCoord(5, 5), 50);
        Assert.Single(sink.Orders);
        Assert.Equal(hero.Id, sink.Orders[0].Subject);
    }

    [Theory]
    [InlineData(0, 49, true)]
    [InlineData(0, 50, false)]
    [InlineData(1, 59, true)]
    [InlineData(1, 60, false)]
    [InlineData(2, 69, true)]
    [InlineData(2, 70, false)]
    [InlineData(3, 79, true)]
    [InlineData(3, 80, false)]
    public void EachRankAddsTenPercentToTheRetreatLine(int rank, int health, bool leaves)
    {
        var sink = new Log();
        new RetreatManager().Think(Know(Unit(1, rank, health)), sink, new Squads(), new CellCoord(5, 5), 50);
        Assert.Equal(leaves, sink.Orders.Count == 1);
    }

    [Fact]
    public void ARetreatedRank3UnitGoesToRepairAndRank2GoesHome()
    {
        ViewActor r2 = Unit(1, 2, 20), r3 = Unit(2, 3, 20);
        RecruitKnowledge k = Know(r2, r3);
        var squads = new Squads();
        new RetreatManager().Think(k, new Log(), squads, new CellCoord(5, 5), 50);
        Assert.True(squads.IsHome(r2) && !squads.IsRepairing(r2));
        Assert.True(squads.IsRepairing(r3) && !squads.IsHome(r3));
        Assert.Contains(r3, k.Detached);
        Assert.DoesNotContain(r3, k.Home);
    }

    [Fact]
    public void ARepairedRank3UnitRejoinsTheArmy()
    {
        ViewActor weak = Unit(2, 3, 20);
        RecruitKnowledge k = Know(weak);
        var squads = new Squads(); var retreat = new RetreatManager();
        retreat.Think(k, new Log(), squads, new CellCoord(5, 5), 50);
        ViewActor healed = weak with { Health = 95 };
        k.Army.Clear(); k.Own.Clear(); k.Own.Add(healed); k.Army.Add(healed);
        squads.Apply(k);
        Assert.Contains(healed, k.Detached);                       // still repairing while the manager has not looked again
        retreat.Think(k, new Log(), squads, new CellCoord(5, 5), 50);
        k.Army.Clear(); k.Army.Add(healed);
        squads.Apply(k);
        Assert.Contains(healed, k.Army);
        Assert.False(squads.IsRepairing(healed));
    }

    [Fact]
    public void TheRepairSetSurvivesSaveAndLoad()
    {
        ViewActor hero = Unit(2, 3, 20);
        var squads = new Squads();
        squads.SendRepair(Know(hero), hero, 77);
        var ms = new MemoryStream();
        squads.Snapshot(new BinaryWriter(ms));
        var copy = new Squads();
        copy.Restore(new BinaryReader(new MemoryStream(ms.ToArray())));
        Assert.True(copy.IsRepairing(hero));
        Assert.Equal(77, copy.RepairSince(hero));
    }

    // ---- (c) focus fire --------------------------------------------------------------------------

    [Fact]
    public void TheAiPicksAVisibleRank3EnemyOverAnEqualRank0One()
    {
        // the rank-0 enemy is nearer; the rank-3 one wins anyway
        ViewActor near = Unit(10, 0, 100, x: 12, y: 10, rel: Relation.Enemy), hero = Unit(11, 3, 100, x: 16, y: 10, rel: Relation.Enemy);
        ViewActor? pick = MilitaryManager.PickFocus([near, hero], new CellCoord(10, 10), TestWorld.Rules.Sim);
        Assert.Equal(hero.Id, pick?.Id);
        Assert.Null(MilitaryManager.PickFocus([near], new CellCoord(10, 10), TestWorld.Rules.Sim));
        Assert.Null(MilitaryManager.PickFocus([hero with { Relation = Relation.Ally }], new CellCoord(10, 10), TestWorld.Rules.Sim));
        Assert.Null(MilitaryManager.PickFocus([hero], new CellCoord(40, 40), TestWorld.Rules.Sim));   // out of range
    }

    [Fact]
    public void TheDefenceSpotPrefersARank3AttackerButKeepsNearestOtherwise()
    {
        ViewActor victim = Unit(1, 0, 50, x: 10, y: 10, kind: ActorKind.Building);
        ViewActor near = Unit(10, 0, 100, x: 11, y: 10, rel: Relation.Enemy), far0 = Unit(11, 0, 100, x: 15, y: 10, rel: Relation.Enemy);
        ViewActor hero = Unit(12, 3, 100, x: 18, y: 10, rel: Relation.Enemy);
        RecruitKnowledge k = Know();
        k.Visible.AddRange([far0, near]);
        Assert.Equal(near.Cell, MilitaryManager.NearestEnemyTo(k, victim));   // no ranks: unchanged
        k.Visible.Add(hero);
        Assert.Equal(hero.Cell, MilitaryManager.NearestEnemyTo(k, victim));
    }

    // ---- (d) squads ------------------------------------------------------------------------------

    [Fact]
    public void RankedUnitsStayInOneProng()
    {
        var army = new List<ViewActor>();
        for (int i = 0; i < 8; i++) army.Add(Unit(i + 1, i % 3 == 0 ? 2 + i % 2 : 0, 100));
        int idx = 0;
        var inB = new List<ViewActor>();
        foreach (ViewActor a in army) if (MilitaryManager.InSquadB(a, ref idx)) inB.Add(a);
        Assert.All(inB, a => Assert.True(a.Rank < 2));
        Assert.All(army.FindAll(a => a.Rank >= 2), a => Assert.DoesNotContain(a, inB));

        int plain = 0, b = 0;
        for (int i = 0; i < 8; i++) if (MilitaryManager.InSquadB(Unit(i + 1, 0, 100), ref plain)) b++;
        Assert.Equal(4, b);   // veterancy off: the old alternation
    }

    // ---- determinism -----------------------------------------------------------------------------

    [Fact]
    public void AnAiSavedMidGameWithVeterancyOnContinuesLikeOneThatNeverSaved()
    {
        const int Before = 1500, After = 1500;
        var straight = new TierRig(Faction.Authority, Tier(4));
        straight.Run(Before + After);
        var r = new TierRig(Faction.Authority, Tier(4));
        r.Run(Before);
        byte[]? blob = r.CaptureAi();
        Assert.NotNull(blob);
        Assert.Equal(straight.Sim.ComputeStateHash(), r.ResumeWithBlob(blob!, After));
    }
}
