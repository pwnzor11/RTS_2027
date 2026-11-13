// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit;

namespace AI.Tests.S22;

/// <summary>S22 (M13-20): the mop-up decisions (beaten trigger, sweep target order), the runtime option and the save.</summary>
public sealed class MopupTests
{
    private static SimRules Rules => TestWorld.Rules.Sim;
    private static int B(string id) => SimRules.IndexOf(Rules.Buildings, id, b => b.Id);

    private static ViewActor Bld(int slot, string id, int x, int y) =>
        new(new EntityId((ushort)slot, 1), ActorKind.Building, 1, Relation.Enemy, B(id), new CellCoord(x, y), new LeptonCoord(x * 256, y * 256),
            100, 100, MissionKind.Guard, false, EntityId.None, true, 1, 1);

    [Fact]
    public void TheEnemyIsBeatenOnlyWhenOurArmyIsBigAndFarAheadOfWhatWasSeen()
    {
        Assert.True(MopupPlan.Beaten(5000, 0, 10));                       // nothing seen in the long window
        Assert.True(MopupPlan.Beaten(5000, 2000, 10));                    // exactly two and a half to one (s23)
        Assert.False(MopupPlan.Beaten(5000, 2001, 10));                   // just short of it
        Assert.False(MopupPlan.Beaten(5000, 1000, MopupPlan.MinArmy - 1));   // too few units while the enemy still has an army
        Assert.False(MopupPlan.Beaten(MopupPlan.MinValue - 1, 100, 20));  // an army of cheap units is not a force against an army
        Assert.True(MopupPlan.Beaten(600, 0, MopupPlan.MinArmyNoEnemy));  // s23: no enemy army left, a few units are enough
        Assert.False(MopupPlan.Beaten(600, 0, MopupPlan.MinArmyNoEnemy - 1));
        Assert.False(MopupPlan.Beaten(0, 0, 0));
        Assert.False(MopupPlan.Beaten(int.MaxValue, int.MaxValue, 50));   // no overflow: equal values are not four to one
    }

    [Fact]
    public void StaleBlocksAreThoseNeverSeenOrNotInViewForALongTime()
    {
        Assert.True(MopupPlan.Stale(0, 100));
        Assert.False(MopupPlan.Stale(1001, 1000));
        Assert.False(MopupPlan.Stale(1001, 1000 + MopupPlan.RevisitTicks));
        Assert.True(MopupPlan.Stale(1001, 1001 + MopupPlan.RevisitTicks));
    }

    [Fact]
    public void TheSweepTakesNeverSeenBlocksBeforeOldFogAndTheDefendedCoreLast()
    {
        // 6 x 1 blocks of a 48 x 8 map; the army stands at block 0
        int[] stamp = { 19999, 19999, 0, 0, 0, 0 };
        CellCoord? g = MopupPlan.PickSweep(Rules, stamp, 6, 48, 8, 20000, new CellCoord(4, 4), new List<ViewActor>(), _ => true, _ => false);
        Assert.Equal(new CellCoord(2 * 8 + 4, 4), g);   // block 2: stale and never seen, nearest

        // old fog under the army (seen long ago, +24) beats a never-seen block three blocks away (24 cells x 4)
        stamp = new[] { 100, 100, 100, 0, 100, 100 };
        g = MopupPlan.PickSweep(Rules, stamp, 6, 48, 8, 20000, new CellCoord(4, 4), new List<ViewActor>(), _ => true, _ => false);
        Assert.Equal(new CellCoord(4, 4), g);

        // two stale blocks, the nearer one under the guns of a known pillbox: the open one goes first; without the pillbox the near one does
        stamp = new[] { 20000, 20000, 0, 20000, 20000, 0 };
        var known = new List<ViewActor> { Bld(1, "dir_pillbox", 20, 4) };
        Assert.Equal(1, SiegePlan.Cover(Rules, known, new CellCoord(20, 4)));
        Assert.Equal(0, SiegePlan.Cover(Rules, known, new CellCoord(44, 4)));
        g = MopupPlan.PickSweep(Rules, stamp, 6, 48, 8, 20000, new CellCoord(28, 4), known, _ => true, _ => false);
        Assert.Equal(new CellCoord(44, 4), g);
        g = MopupPlan.PickSweep(Rules, stamp, 6, 48, 8, 20000, new CellCoord(28, 4), new List<ViewActor>(), _ => true, _ => false);
        Assert.Equal(new CellCoord(20, 4), g);
    }

    [Fact]
    public void TheSweepSkipsBlocksThatCannotBeWalkedOrWereAvoided()
    {
        int[] stamp = { 0, 0, 0 };
        CellCoord? g = MopupPlan.PickSweep(Rules, stamp, 3, 24, 8, 5000, new CellCoord(4, 4), new List<ViewActor>(), c => c.X != 4, c => c.X == 12);
        Assert.Equal(new CellCoord(20, 4), g);
        Assert.Null(MopupPlan.PickSweep(Rules, new[] { 4999, 4999 }, 2, 16, 8, 5000, new CellCoord(4, 4), new List<ViewActor>(), _ => true, _ => false));   // all fresh
        Assert.Null(MopupPlan.PickSweep(Rules, Array.Empty<int>(), 0, 0, 0, 5000, default, new List<ViewActor>(), _ => true, _ => false));
    }

    [Fact]
    public void ABigArmySplitsIntoPartiesWithSeparateGoals()
    {
        Assert.Equal(1, MopupPlan.Parties(3));
        Assert.Equal(2, MopupPlan.Parties(2 * MopupPlan.PartySize));
        Assert.Equal(MopupPlan.MaxParties, MopupPlan.Parties(500));
        int[] stamp = new int[12 * 12];   // a 96 x 96 map, nothing ever seen
        var goals = MopupPlan.PickSweeps(Rules, stamp, 12, 96, 96, 20000, new CellCoord(4, 4), new List<ViewActor>(), _ => true, _ => false, 5);
        Assert.Equal(5, goals.Count);
        Assert.Equal(new CellCoord(4, 4), goals[0]);   // the first is the single-goal pick
        for (int i = 0; i < goals.Count; i++)
            for (int j = i + 1; j < goals.Count; j++)
                Assert.True(Math.Max(Math.Abs(goals[i].X - goals[j].X), Math.Abs(goals[i].Y - goals[j].Y)) >= MopupPlan.PartySpacingCells);
        // fewer stale blocks than parties: only what is left
        var few = MopupPlan.PickSweeps(Rules, new[] { 0, 99999, 99999 }, 3, 24, 8, 100000, new CellCoord(4, 4), new List<ViewActor>(), _ => true, _ => false, 4);
        Assert.Single(few);
    }

    [Fact]
    public void TheOptionFollowsTheProfileAndIsOffWithoutRespondToAttacks()
    {
        DifficultyProfile t1 = TestWorld.Tiers[0], t3 = TestWorld.Tiers[2];
        Assert.False(t1.RespondToAttacks);
        Assert.False(MilitaryOptions.From(t1).Mopup);
        Assert.True(t3.RespondToAttacks);
        Assert.True(MilitaryOptions.From(t3).Mopup);
        Assert.False(MilitaryOptions.From(t3.WithMopup(false)).Mopup);
        Assert.False(MilitaryOptions.From(t1.WithMopup(true)).Mopup);   // tier 1 never mops up whatever the switch says
        Assert.False(MilitaryOptions.Recruit.Mopup);
    }

    [Fact]
    public void AMissionBotNeverMopsUp()
    {
        var m = new MilitaryManager(8) { Options = MilitaryOptions.From(TestWorld.Tiers[2]) };
        Assert.True(m.MopupOn);
        m.MissionBot = true;
        Assert.False(m.MopupOn);
        Assert.False(m.Sweeping);
    }

    [Fact]
    public void WithMopupAlsoSwitchesTheFactionVersions()
    {
        DifficultyProfile t3 = TestWorld.Tiers[2];
        DifficultyProfile over = t3 with { AttackThreshold = t3.AttackThreshold + 1 };
        DifficultyProfile p = t3.WithFactionOverride(1, over).WithMopup(false);
        Assert.False(p.Mopup);
        Assert.False(p.ForFaction(1).Mopup);
        Assert.True(t3.ForFaction(1).Mopup || ReferenceEquals(t3.ForFaction(1), t3));
    }

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void AnAiSavedAfterTheMopupMemoryFilledContinuesLikeOneThatNeverSaved(Faction faction)
    {
        const int Before = 9500, After = 1500;
        DifficultyProfile tier = TestWorld.Tiers[3];
        var straight = new TierRig(faction, tier);
        straight.Run(Before + After);
        var r = new TierRig(faction, tier);
        r.Run(Before);
        byte[]? blob = r.CaptureAi();
        Assert.NotNull(blob);
        Assert.Equal(straight.Sim.ComputeStateHash(), r.ResumeWithBlob(blob!, After));
    }
}
