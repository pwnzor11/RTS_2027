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
}
