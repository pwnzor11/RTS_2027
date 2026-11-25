// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit;

namespace AI.Tests.S44;

/// <summary>s44 A-02 (focus_fire for every faction, supply_margin as data, malformed file = clear error) and A-03 (repair hysteresis).</summary>
public sealed class AiFixesS44Tests
{
    private sealed class Log : ICommandSink
    {
        public readonly List<(CommandKind Kind, EntityId Subject)> Orders = new();
        public void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default, ActorRef target = default, int arg2 = 0) => Orders.Add((kind, subject));
        public void Build(ProductionCategory category, int typeIndex) { }
        public void Place(int buildingType, CellCoord topLeft) { }
        public void Rally(EntityId factory, CellCoord cell) { }
    }

    private static SimRules Rules => TestWorld.Rules.Sim;

    private static ViewActor Building(int health, ushort index = 7)
    {
        int power = SimRules.IndexOf(Rules.Buildings, "auth_power", b => b.Id);
        return new ViewActor(new EntityId(index, 1), ActorKind.Building, 0, Relation.Self, power, new CellCoord(10, 10), new LeptonCoord(2560, 2560),
            health, 1000, MissionKind.None, true, EntityId.None, false, 2, 2);
    }

    private static int Decide(RepairManager repair, Log sink, int health)
    {
        var k = new RecruitKnowledge { Rules = TestWorld.Rules.Sim };
        k.Buildings.Add(Building(health));
        int before = sink.Orders.Count;
        repair.Think(k, sink);
        return sink.Orders.Count - before;
    }

    [Fact]
    public void Repair_StaysOn_WhileUnderFire_ForManyDecisions()
    {
        var repair = new RepairManager();
        var sink = new Log();
        for (int d = 0, h = 700; d < 40; d++, h = Math.Max(50, h - 10)) Decide(repair, sink, h);
        Assert.Single(sink.Orders);
    }

    [Fact]
    public void Repair_StaysOn_WhileHealing_ThenForgetsWhenWhole()
    {
        var repair = new RepairManager();
        var sink = new Log();
        Assert.Equal(1, Decide(repair, sink, 600));
        for (int h = 620; h < 1000; h += 20) Assert.Equal(0, Decide(repair, sink, h));
        Assert.Equal(0, Decide(repair, sink, 1000));   // whole: forgotten
        Assert.Equal(1, Decide(repair, sink, 500));    // damaged again later: a fresh order
    }

    [Fact]
    public void Repair_OrderedAgain_OnlyWhenHealthIsFrozen_NotHealingNotHit()
    {
        var repair = new RepairManager();
        var sink = new Log();
        Assert.Equal(1, Decide(repair, sink, 600));
        for (int d = 1; d < RepairManager.StuckDecisions; d++) Assert.Equal(0, Decide(repair, sink, 600));
        Assert.Equal(1, Decide(repair, sink, 600));   // the sim stopped it (broke): switch it back on once
    }

    [Fact]
    public void FocusFire_FlagReachesTheMilitaryOptions_ForEveryTier()
    {
        foreach (DifficultyProfile p in TestWorld.Tiers)
            Assert.Equal(p.FocusFire, MilitaryOptions.From(p).FocusFire);
        Assert.False(MilitaryOptions.From(TestWorld.Tiers[0]).FocusFire);
        Assert.True(MilitaryOptions.From(TestWorld.Tiers[2]).FocusFire);
        Assert.True(MilitaryOptions.From(TestWorld.Tiers[4]).FocusFire);
    }

    [Fact]
    public void PickWeakestInRange_TakesLowestHealthEnemyInReach()
    {
        int foot = SimRules.IndexOf(Rules.Infantry, "auth_rifleman", v => v.Id);
        ViewActor Unit(ushort idx, Relation rel, int hp, int x) => new(new EntityId(idx, 1), ActorKind.Infantry, rel == Relation.Self ? (byte)0 : (byte)1, rel, foot,
            new CellCoord(x / 256, 10), new LeptonCoord(x, 2560), hp, 100, MissionKind.None, true, EntityId.None, false, 1, 1);
        ViewActor me = Unit(1, Relation.Self, 100, 2560);
        var seen = new List<ViewActor> { Unit(2, Relation.Enemy, 80, 3000), Unit(3, Relation.Enemy, 30, 3200), Unit(4, Relation.Enemy, 10, 90000), Unit(5, Relation.Ally, 5, 2800) };
        ViewActor? pick = MilitaryManager.PickWeakestInRange(seen, Rules, me, 1500);
        Assert.Equal(3, pick!.Value.Id.Index);          // 4 is the weakest but out of reach; 5 is not an enemy
        Assert.Null(MilitaryManager.PickWeakestInRange(seen, Rules, me, 0));   // no gun
    }

    [Fact]
    public void SupplyMargin_ShippedPerTierMatchesTheOldTable_AndMissingFallsBack()
    {
        int[] old = [4, 5, 8, 10, 12];
        for (int t = 0; t < 5; t++)
        {
            Assert.Equal(old[t], TestWorld.Tiers[t].SupplyMargin);
            Assert.Equal(BuildOrderManager.MarginForTier(t + 1), BuildOrderManager.MarginFor(TestWorld.Tiers[t]));
        }
        Assert.Equal(BuildOrderManager.MarginForTier(2), BuildOrderManager.MarginFor(DifficultyProfile.Default));
        Assert.Equal(20, BuildOrderManager.MarginFor(DifficultyProfile.Default with { SupplyMargin = 20 }));
    }

    private static string RealText() => File.ReadAllText(DifficultyLoader.PathBesideRules(TestWorld.RulesDir));

    private static RulesException LoadBad(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), $"diff44_{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, text);
        try { return Assert.Throws<RulesException>(() => DifficultyLoader.Load(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MalformedDifficultyFile_RaisesErrorNamingFileAndKey()
    {
        RulesException typo = LoadBad(RealText().Replace("supply_margin = 8", "supply_margn = 8"));
        Assert.Contains(typo.Problems, p => p.File == DifficultyLoader.FileName && p.Key == "veteran.supply_margn");
        Assert.Contains(DifficultyLoader.FileName, typo.Message);
        Assert.Contains("veteran.supply_margn", typo.Message);

        RulesException range = LoadBad(RealText().Replace("supply_margin = 8", "supply_margin = 500"));
        Assert.Contains(range.Problems, p => p.Key == "veteran.supply_margin" && p.Message.Contains("between"));

        RulesException syntax = LoadBad("[recruit\ntier = 1");   // not TOML at all
        Assert.Contains(syntax.Problems, p => p.File == DifficultyLoader.FileName && p.Key == "<parse>");
    }
}
