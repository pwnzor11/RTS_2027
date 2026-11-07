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
}
