// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;

namespace AI.Tests;

/// <summary>s29: the per-faction <c>siege_commit_ratio</c> key (default 0 = off, today's behaviour).</summary>
public class SiegeCommitKeyTests
{
    private static string Write(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), $"diff_{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void ShippedFileLeavesTheKeyOffForEveryFaction()
    {
        var keys = TestWorld.Tiers[2].FactionKeys;
        foreach (Faction f in new[] { Faction.Authority, Faction.Directorate, Faction.LeftBehind, Faction.Expedition })
            Assert.Equal(0, keys.CommitRatio((int)f));
    }

    [Fact]
    public void KeyIsReadPerFactionAndRangeChecked()
    {
        string text = File.ReadAllText(DifficultyLoader.PathBesideRules(TestWorld.RulesDir));
        string path = Write(text.Replace("\n[faction.expedition]\n", "\n[faction.expedition]\nsiege_commit_ratio = 300\n"));
        try
        {
            var keys = DifficultyLoader.Load(path)[2].FactionKeys;
            Assert.Equal(300, keys.CommitRatio((int)Faction.Expedition));
            Assert.Equal(0, keys.CommitRatio((int)Faction.Directorate));
            Assert.Equal(30, keys.HarassMinArmy((int)Faction.Expedition));
        }
        finally { File.Delete(path); }
        string bad = Write(text.Replace("\n[faction.expedition]\n", "\n[faction.expedition]\nsiege_commit_ratio = 20000\n"));
        try
        {
            var ex = Assert.Throws<RulesException>(() => DifficultyLoader.Load(bad));
            Assert.Contains(ex.Problems, p => p.Key == "faction.expedition.siege_commit_ratio");
        }
        finally { File.Delete(bad); }
    }

    [Fact]
    public void AaNeedsAirKeyIsOffByDefaultAndReadPerFaction()
    {
        Assert.False(TestWorld.Tiers[2].FactionKeys.AaNeedsAir((int)Faction.Expedition));
        string text = File.ReadAllText(DifficultyLoader.PathBesideRules(TestWorld.RulesDir));
        string path = Write(text.Replace("\n[faction.expedition]\n", "\n[faction.expedition]\naa_needs_air = 1\n"));
        try
        {
            var keys = DifficultyLoader.Load(path)[2].FactionKeys;
            Assert.True(keys.AaNeedsAir((int)Faction.Expedition));
            Assert.False(keys.AaNeedsAir((int)Faction.Directorate));
        }
        finally { File.Delete(path); }
    }
}
