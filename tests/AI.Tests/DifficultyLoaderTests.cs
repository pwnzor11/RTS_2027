// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;

namespace AI.Tests;

public class DifficultyLoaderTests
{
    [Fact]
    public void LoadsRealFileWithFiveTiers()
    {
        var tiers = TestWorld.Tiers;
        Assert.Equal(["recruit", "regular", "veteran", "elite", "commissar"], tiers.Select(t => t.Id));
        Assert.Equal([1, 2, 3, 4, 5], tiers.Select(t => t.Tier));
        Assert.False(tiers[0].FocusFire);
        Assert.True(tiers[2].FocusFire);
        Assert.True(tiers[4].DecisionIntervalTicks < tiers[0].DecisionIntervalTicks);
        Assert.All(tiers.Skip(1), t => Assert.Empty(t.BuildRepertoire));
        Assert.Equal(7, tiers[0].BuildRepertoire.Count);
    }

    private static string Write(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), $"diff_{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void ReportsEveryProblem()
    {
        string real = File.ReadAllText(DifficultyLoader.PathBesideRules(TestWorld.RulesDir));
        string bad = real.Replace("decision_interval_ticks = 60", "decision_interval_ticks = 6.5")   // float
            .Replace("[elite]", "[elite]\nbogus = 1")                                                  // unknown key
            .Replace("[commissar]", "[extra]\ntier = 9\n\n[commissar_gone]");                          // unknown tier tables, commissar missing
        string path = Write(bad);
        try
        {
            var ex = Assert.Throws<RulesException>(() => DifficultyLoader.Load(path));
            Assert.Contains(ex.Problems, p => p.Key == "recruit.decision_interval_ticks");
            Assert.Contains(ex.Problems, p => p.Key == "elite.bogus");
            Assert.Contains(ex.Problems, p => p.Key == "extra");
            Assert.Contains(ex.Problems, p => p.Key == "commissar" && p.Message.Contains("required"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MissingFileIsAProblem()
    {
        var ex = Assert.Throws<RulesException>(() => DifficultyLoader.Load("/nonexistent/difficulty.toml"));
        Assert.Single(ex.Problems);
    }

    [Fact]
    public void FactionHarassKeyIsReadAndFallsBackTo30()
    {
        var real = TestWorld.Tiers[3].FactionKeys;
        Assert.Equal(14, real.HarassMinArmy((int)Rts.Sim.Rules.Faction.Directorate));
        Assert.Equal(30, real.HarassMinArmy((int)Rts.Sim.Rules.Faction.Authority));
        string text = File.ReadAllText(DifficultyLoader.PathBesideRules(TestWorld.RulesDir));
        string edited = text.Replace("harass_min_army = 14", "harass_min_army = 9");
        int cut = edited.IndexOf("\n[faction.left_behind]", StringComparison.Ordinal);
        int end = edited.IndexOf("\n[recruit]", StringComparison.Ordinal);
        edited = edited.Remove(cut, end - cut);   // left_behind and expedition tables missing
        string path = Write(edited);
        try
        {
            var keys = DifficultyLoader.Load(path)[4].FactionKeys;
            Assert.Equal(9, keys.HarassMinArmy((int)Rts.Sim.Rules.Faction.Directorate));
            Assert.Equal(30, keys.HarassMinArmy((int)Rts.Sim.Rules.Faction.LeftBehind));
            Assert.Equal(30, keys.HarassMinArmy((int)Rts.Sim.Rules.Faction.Expedition));
        }
        finally { File.Delete(path); }
        string bad = Write(text.Replace("harass_min_army = 14", "harass_min_army = 0\nbogus = 1"));
        try
        {
            var ex = Assert.Throws<RulesException>(() => DifficultyLoader.Load(bad));
            Assert.Contains(ex.Problems, p => p.Key == "faction.directorate.harass_min_army");
            Assert.Contains(ex.Problems, p => p.Key == "faction.directorate.bogus");
        }
        finally { File.Delete(bad); }
    }

    [Fact]
    public void AdaptRichKeysAreOffByDefaultAndRead()   // s39 M13-38
    {
        var d = Rts.Data.FactionTuning.Default;
        Assert.Equal((0, 3), (d.AdaptRichCash, d.AdaptRichMax));
        string text = File.ReadAllText(DifficultyLoader.PathBesideRules(TestWorld.RulesDir)).Replace("\r\n", "\n");
        var shipped = DifficultyLoader.Load(DifficultyLoader.PathBesideRules(TestWorld.RulesDir))[3].FactionKeys;
        Assert.Equal(2000, shipped.TuningOf(1).AdaptRichCash);   // s39: on for the Directorate (applies only with a handicap above 100)
        Assert.Equal(0, shipped.TuningOf(0).AdaptRichCash);
        string path = Write(text.Replace("[faction.authority]\n", "[faction.authority]\nadapt_rich_cash = 2000\nadapt_rich_max = 4\n"));
        try
        {
            var t = DifficultyLoader.Load(path)[3].FactionKeys.TuningOf(0);
            Assert.Equal((2000, 4), (t.AdaptRichCash, t.AdaptRichMax));
        }
        finally { File.Delete(path); }
        string bad = Write(text.Replace("[faction.directorate]\n", "[faction.directorate]\nadapt_rich_max = 1\n"));
        try { Assert.ThrowsAny<Exception>(() => DifficultyLoader.Load(bad)); }
        finally { File.Delete(bad); }
    }

    [Fact]
    public void BoreKeysAreReadAndDefaultToTheFormerConstants()   // s39 M13-37
    {
        var d = Rts.Data.FactionTuning.Default;
        Assert.Equal((5, 4, 1500, 15, 0, 200), (d.BoreMax, d.BoreArmy, d.BoreGapTicks, d.BoreDigShort, d.BoreSites, d.BoreMinCash));
        Assert.Equal(Rts.AI.Recruit.DirectorateTactics.MaxBoresBuilt, d.BoreMax);
        Assert.Equal(Rts.AI.Recruit.DirectorateTactics.ArmyBeforeBore, d.BoreArmy);
        Assert.Equal(Rts.AI.Recruit.DirectorateTactics.BoreGapTicks, d.BoreGapTicks);
        Assert.Equal(Rts.AI.Recruit.DirectorateTactics.DigShortCells, d.BoreDigShort);
        Assert.Equal(Rts.AI.Recruit.DirectorateTactics.BoreCushion, d.BoreMinCash);
        string text = File.ReadAllText(DifficultyLoader.PathBesideRules(TestWorld.RulesDir)).Replace("\r\n", "\n");
        Assert.Equal(0, DifficultyLoader.Load(DifficultyLoader.PathBesideRules(TestWorld.RulesDir))[3].FactionKeys.TuningOf(1).BoreMax);   // D-290
        string edited = text.Replace("[faction.authority]\n", "[faction.authority]\nbore_max = 2\nbore_army = 9\nbore_gap_ticks = 3000\nbore_dig_short = 25\nbore_sites = 1\nbore_min_cash = 900\n");
        string path = Write(edited);
        try
        {
            var t = DifficultyLoader.Load(path)[3].FactionKeys.TuningOf(0);
            Assert.Equal((2, 9, 3000, 25, 1, 900), (t.BoreMax, t.BoreArmy, t.BoreGapTicks, t.BoreDigShort, t.BoreSites, t.BoreMinCash));
        }
        finally { File.Delete(path); }
        string bad = Write(text.Replace("[faction.authority]\n", "[faction.authority]\nbore_sites = 3\n"));
        try { Assert.ThrowsAny<Exception>(() => DifficultyLoader.Load(bad)); }
        finally { File.Delete(bad); }
    }

    [Fact]
    public void FactionTuningKeysAreReadValidatedAndDefaultToTheConstants()
    {
        var real = TestWorld.Tiers[3].FactionKeys;
        Assert.Equal(new Rts.Data.FactionTuning(30, 30, 1500, 2, 3600, -1), real.TuningOf((int)Rts.Sim.Rules.Faction.Authority));
        string text = File.ReadAllText(DifficultyLoader.PathBesideRules(TestWorld.RulesDir));
        string edited = text.Replace("[faction.authority]\n", "[faction.authority]\narmy_before_tech = 18\narmy_before_adapt = 0\nadapt_cash = 1200\nadapt_max_production = 4\ntier2_gate_tick = 2400\nadapt_build = 1\n");
        string path = Write(edited);
        try
        {
            var keys = DifficultyLoader.Load(path)[4].FactionKeys;
            Assert.Equal(new Rts.Data.FactionTuning(18, 0, 1200, 4, 2400, 1), keys.TuningOf(Authority));
            Assert.Equal(Rts.Data.FactionTuning.Default, keys.TuningOf(LeftBehind));
            Assert.Equal(30, keys.HarassMinArmy(Authority));
        }
        finally { File.Delete(path); }
        string bad = Write(text.Replace("[faction.authority]\n", "[faction.authority]\narmy_before_tech = 0\nadapt_max_production = 7\ntier2_gate_tick = -1\n"));
        try
        {
            var ex = Assert.Throws<RulesException>(() => DifficultyLoader.Load(bad));
            Assert.Contains(ex.Problems, p => p.Key == "faction.authority.army_before_tech");
            Assert.Contains(ex.Problems, p => p.Key == "faction.authority.adapt_max_production");
            Assert.Contains(ex.Problems, p => p.Key == "faction.authority.tier2_gate_tick");
        }
        finally { File.Delete(bad); }
    }

    private const int LeftBehind = (int)Rts.Sim.Rules.Faction.LeftBehind;
    private const int Authority = (int)Rts.Sim.Rules.Faction.Authority;

    [Fact]
    public void LeftBehindTierThreeOverrideAppliesToThatFactionAndTierOnly()
    {
        var tiers = TestWorld.LoadedTiers;
        var shared = tiers[2];
        var lb = shared.ForFaction(LeftBehind);
        Assert.NotSame(shared, lb);
        Assert.True(lb.ArmyCap > 0);
        Assert.Equal(0, shared.ArmyCap);                              // the shared tier 3 table is untouched
        Assert.Same(shared, shared.ForFaction(4));                    // the Expedition plays the shared profile
        Assert.Same(lb, shared.ForFaction(LeftBehind));               // same instance every call (the AI compares by reference)
        Assert.Equal(shared.AttackThreshold, lb.AttackThreshold);     // keys the table does not name keep the tier value
        Assert.Equal(shared.Tier, lb.Tier);
        foreach (int t in new[] { 0, 1, 3, 4 }) Assert.Same(tiers[t], tiers[t].ForFaction(LeftBehind));   // other tiers have no override
    }

    [Fact]
    public void AuthorityTiersTwoToFivePlayWithoutRespondToAttacks()
    {
        // M4-16 s38: the Authority ladder fix; the shared tiers and the other factions keep the flag, the Authority keeps every other number of the tier
        for (int t = 1; t < 5; t++)
        {
            var shared = TestWorld.LoadedTiers[t];
            Assert.True(shared.RespondToAttacks);
            Assert.False(shared.ForFaction(Authority).RespondToAttacks);
            Assert.True(shared.ForFaction(1).RespondToAttacks);
            if (t < 4) Assert.Equal(shared.AttackThreshold, shared.ForFaction(Authority).AttackThreshold);   // s39 D-292: tier 5 sets its own
            Assert.Equal(shared.Repair, shared.ForFaction(Authority).Repair);
            Assert.Equal(t < 3, shared.ForFaction(4).RespondToAttacks);   // s39 D-291: Expedition tiers 4-5 without the flag
        }
        Assert.Equal((4, 14), (TestWorld.LoadedTiers[4].ForFaction(Authority).HarvesterTarget, TestWorld.LoadedTiers[4].ForFaction(Authority).AttackThreshold));
        Assert.Same(TestWorld.LoadedTiers[0], TestWorld.LoadedTiers[0].ForFaction(Authority));
    }

    [Fact]
    public void TierOverrideTableIsValidated()
    {
        string text = File.ReadAllText(DifficultyLoader.PathBesideRules(TestWorld.RulesDir)).Replace("\r\n", "\n");
        string plain = text.Replace("[faction.left_behind.tier3]\narmy_cap", "[faction.left_behind.tier3]\nattack_threshold = 5\nunit_reserve = 120\narmy_cap");
        string path = Write(plain);
        try
        {
            var lb = DifficultyLoader.Load(path)[2].ForFaction(LeftBehind);
            Assert.Equal(5, lb.AttackThreshold);
            Assert.Equal(120, lb.UnitReserve);
        }
        finally { File.Delete(path); }
        string bad = Write(text.Replace("[faction.left_behind.tier3]\narmy_cap", "[faction.left_behind.tier3]\nfocus_fire = 1\nattack_threshold = 0.5\narmy_cap"));
        try
        {
            var ex = Assert.Throws<RulesException>(() => DifficultyLoader.Load(bad));
            Assert.Contains(ex.Problems, p => p.Key == "faction.left_behind.tier3.focus_fire");        // not an overridable key
            Assert.Contains(ex.Problems, p => p.Key == "faction.left_behind.tier3.attack_threshold");  // a float
        }
        finally { File.Delete(bad); }
        string noTier = Write(text.Replace("[faction.left_behind.tier3]", "[faction.left_behind.tier9]"));
        try
        {
            var ex = Assert.Throws<RulesException>(() => DifficultyLoader.Load(noTier));
            Assert.Contains(ex.Problems, p => p.Key == "faction.left_behind.tier9");
        }
        finally { File.Delete(noTier); }
    }
}
