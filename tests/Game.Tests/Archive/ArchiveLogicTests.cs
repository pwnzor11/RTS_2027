// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Archive;

public sealed class ArchiveLogicTests
{
    [Fact]
    public void Live_stats_match_the_rules()
    {
        SimRules rules = ArchiveTestData.Rules();
        VehicleType tank = rules.Vehicles.First(v => v.Id == "auth_tank");
        BuildingType refinery = rules.Buildings.First(b => b.Id == "auth_refinery");
        UpgradeType grouting = rules.Upgrades.First(u => u.Id == "auth_grouting");

        IReadOnlyList<ArchiveStatLine> t = ArchiveStats.For(rules, "auth_tank");
        Assert.Equal(tank.Cost.ToString(), Value(t, "Cost"));
        Assert.Equal(tank.MaxHealth.ToString(), Value(t, "Health"));
        WeaponType w = rules.Weapons[tank.PrimaryWeapon];
        Assert.Contains(w.Id, Value(t, "Weapon"), StringComparison.Ordinal);
        Assert.Contains(w.Damage + " damage", Value(t, "Weapon"), StringComparison.Ordinal);
        Assert.Contains(string.Join(", ", tank.Prerequisites), Value(t, "Requires"), StringComparison.Ordinal);

        IReadOnlyList<ArchiveStatLine> r = ArchiveStats.For(rules, "auth_refinery");
        Assert.Equal(refinery.Cost.ToString(), Value(r, "Cost"));
        Assert.Equal($"{refinery.FootprintW}x{refinery.FootprintH} cells", Value(r, "Footprint"));

        IReadOnlyList<ArchiveStatLine> u = ArchiveStats.For(rules, "auth_grouting");
        Assert.Equal(grouting.Cost.ToString(), Value(u, "Cost"));
        Assert.Equal(grouting.Effect, Value(u, "Effect"));
    }

    [Fact]
    public void A_changed_rule_changes_the_stats_so_they_cannot_go_stale()
    {
        SimRules rules = ArchiveTestData.Rules();
        VehicleType tank = rules.Vehicles.First(v => v.Id == "auth_tank");
        SimRules edited = rules with { Vehicles = rules.Vehicles.Select(v => v.Id == "auth_tank" ? tank with { Cost = 4321 } : v).ToList() };

        Assert.Equal("4321", Value(ArchiveStats.For(edited, "auth_tank"), "Cost"));
    }

    [Fact]
    public void Every_stat_entry_in_the_archive_has_stats_and_unknown_ids_have_none()
    {
        SimRules rules = ArchiveTestData.Rules();
        foreach (ArchiveEntry e in ArchiveTestData.Catalog().Entries.Where(e => e.HasStats))
        {
            Assert.NotEmpty(ArchiveStats.For(rules, e.Id));
        }
        Assert.Empty(ArchiveStats.For(rules, "nonsense"));
    }

    [Fact]
    public void Browser_navigates_tabs_and_entries_with_wrapping_and_counts_locked_entries()
    {
        var progress = new ArchiveProgress();
        var browser = new ArchiveBrowser(ArchiveTestData.Catalog(), progress);

        Assert.Equal(ArchiveCategory.History, browser.Tab);
        browser.MoveTab(-1);
        Assert.Equal(ArchiveCategory.Technology, browser.Tab);
        browser.MoveTab(1);
        Assert.Equal(ArchiveCategory.History, browser.Tab);
        browser.MoveEntry(-1);
        Assert.Equal(browser.Entries.Count - 1, browser.Selected);

        browser.Tab = ArchiveCategory.Leader;
        (int open, int total) = browser.Count(ArchiveCategory.Leader);
        Assert.Equal(16, total);
        Assert.Equal(browser.Catalog.InCategory(ArchiveCategory.Leader).Count(e => e.Unlock.Kind == ArchiveUnlockKind.Open), open);
        Assert.True(open is > 0 and < 16);

        progress.MarkCampaignMission("TBD");
        Assert.Equal(16, browser.Count(ArchiveCategory.Leader).Unlocked);
    }

    [Fact]
    public void The_tracker_records_what_the_viewer_sees_and_ignores_the_omniscient_view()
    {
        SimRules rules = ArchiveTestData.Rules();
        string path = Path.Combine(Path.GetTempPath(), "rts_tracker_" + Guid.NewGuid().ToString("N") + ".toml");
        try
        {
            int tank = rules.Vehicles.Select((v, i) => (v, i)).First(x => x.v.Id == "auth_tank").i;
            int sunrise = rules.Buildings.Select((b, i) => (b, i)).First(x => x.b.Id == "dir_sunrise").i;
            UnitView unit = new(default, UnitKind.Vehicle, 1, tank, default, 0, 100);
            BuildingView ghost = new(default, 1, sunrise, default, 1, 1, 100, true);
            var seen = new SimSnapshot(0, 1, 1, new[] { unit }, new[] { LandType.Clear }, 0, new[] { ghost }, 0,
                Array.Empty<byte>());

            var tracker = new ArchiveTracker(new ArchiveProgress(), path);
            Assert.False(tracker.Observe(seen with { Viewer = -1 }, rules));
            Assert.Empty(tracker.Progress.Encounters);

            Assert.True(tracker.Observe(seen, rules));
            Assert.Contains("auth_tank", tracker.Progress.Encounters);
            Assert.Contains("dir_sunrise", tracker.Progress.Encounters);
            Assert.False(tracker.Observe(seen, rules));
            Assert.Contains("dir_sunrise", ArchiveProgress.Load(path).Encounters);   // saved

            Assert.True(tracker.ObserveResearch(rules, 1UL));
            Assert.Contains("auth_grouting", ArchiveProgress.Load(path).Research);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Markup_parses_headings_paragraphs_and_emphasis_and_wraps_by_width()
    {
        var blocks = ArchiveMarkup.Parse("# Head\n\nOne *two* three\nfour.\n\nSecond.");

        Assert.Equal(3, blocks.Count);
        Assert.True(blocks[0].Heading);
        Assert.Equal(new[] { "One ", "two", " three four." }, blocks[1].Spans.Select(s => s.Text));
        Assert.True(blocks[1].Spans[1].Emphasis);

        var lines = ArchiveMarkup.Wrap(blocks, 80, (t, b) => t.Length * 10);   // 8 chars per line
        Assert.All(lines, l => Assert.True(l.Words.Count == 0 || l.Words[^1].X + l.Words[^1].Text.Length * 10 <= 80 || l.Words.Count == 1));
        Assert.True(lines.Count > 3);
        Assert.True(lines.First(l => !l.Heading && l.GapBefore).GapBefore);
        Assert.Equal("a * b", Assert.Single(ArchiveMarkup.Spans("a * b")).Text);
    }

    [Theory]
    [InlineData("open", ArchiveUnlockKind.Open, "")]
    [InlineData("campaign:auth_01", ArchiveUnlockKind.Campaign, "auth_01")]
    [InlineData("encounter:dir_sunrise", ArchiveUnlockKind.Encounter, "dir_sunrise")]
    [InlineData("research:auth_grouting", ArchiveUnlockKind.Research, "auth_grouting")]
    public void Unlock_rules_parse_and_print_back(string text, ArchiveUnlockKind kind, string arg)
    {
        Assert.True(ArchiveUnlock.TryParse(text, out ArchiveUnlock rule));
        Assert.Equal(new ArchiveUnlock(kind, arg), rule);
        Assert.Equal(text, rule.ToString());
    }

    private static string Value(IReadOnlyList<ArchiveStatLine> lines, string label) => lines.First(l => l.Label == label).Value;
}
