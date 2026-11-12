// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens;

namespace Rts.Game.Tests.M10;

public sealed class CreditsTests
{
    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");

    private static string CreditsMarkdown()
    {
        for (DirectoryInfo? dir = new(RulesDir); dir is not null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "docs", "CREDITS.md");
            if (File.Exists(path)) return File.ReadAllText(path);
        }

        throw new FileNotFoundException("docs/CREDITS.md");
    }

    // The data rows of every table in docs/CREDITS.md (the header row and the |---| row are not data).
    private static List<string> MarkdownRows()
    {
        var rows = new List<string>();
        bool header = false;
        foreach (string raw in CreditsMarkdown().Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (!line.StartsWith('|')) { header = false; continue; }
            if (!header) { header = true; continue; }     // the first row of a table
            if (line.StartsWith("|---")) continue;
            rows.Add(line);
        }

        return rows;
    }

    [Fact]
    public void The_shipped_credits_parse_with_sections_and_entries()
    {
        CreditsDoc doc = CreditsDoc.Load(RulesDir);
        Assert.NotSame(CreditsDoc.Fallback, doc);
        Assert.Equal("RTS_2027", doc.Title);
        Assert.True(doc.Sections.Count >= 3);
        Assert.All(doc.Sections, s => { Assert.NotEmpty(s.Heading); Assert.NotEmpty(s.Entries); });
        Assert.All(doc.Sections.SelectMany(s => s.Entries), e => { Assert.NotEmpty(e.Name); Assert.NotEmpty(e.Credit); Assert.NotEmpty(e.Match); });
    }

    [Fact]
    public void Every_row_of_credits_md_is_credited_and_every_entry_matches_a_row()
    {
        List<string> rows = MarkdownRows();
        Assert.True(rows.Count >= 20);   // libraries, imported assets, audio and the 12 stills
        CreditsDoc doc = CreditsDoc.Load(RulesDir);
        var entries = doc.Sections.SelectMany(s => s.Entries).ToList();
        foreach (string row in rows)
            Assert.True(entries.Any(e => e.Match.Any(m => row.Contains(m, StringComparison.Ordinal))), "Not in data/ui/credits.toml: " + row[..Math.Min(80, row.Length)]);
        foreach (CreditsEntry entry in entries)
            Assert.True(rows.Any(r => entry.Match.Any(m => r.Contains(m, StringComparison.Ordinal))), "Matches no row of docs/CREDITS.md: " + entry.Name);
    }

    [Fact]
    public void Parse_reads_the_layout_and_rejects_bad_toml()
    {
        const string toml = "title = \"T\"\nclosing = \"Bye\"\n[[section]]\nheading = \"H\"\n[[section.entry]]\nname = \"N\"\ncredit = \"C\"\nmatch = [\"x\", \"y\"]\n[[section.entry]]\ncredit = \"nameless\"\n";
        CreditsDoc doc = CreditsDoc.Parse(toml)!;
        Assert.Equal("T", doc.Title);
        Assert.Equal("Credits", doc.Subtitle);
        CreditsSection section = Assert.Single(doc.Sections);
        CreditsEntry entry = Assert.Single(section.Entries);   // the entry without a name is skipped
        Assert.Equal(new[] { "x", "y" }, entry.Match);
        Assert.Null(CreditsDoc.Parse("[[section"));
    }

    [Fact]
    public void A_missing_file_gives_the_fallback_which_still_makes_a_roll()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts2027-credits-" + Guid.NewGuid().ToString("N"), "rules");
        CreditsDoc doc = CreditsDoc.Load(dir);
        Assert.Same(CreditsDoc.Fallback, doc);
        Assert.NotEmpty(doc.Lines(40, t => t.Length));
    }

    [Fact]
    public void Lines_wrap_to_the_width_and_run_title_to_closing()
    {
        CreditsDoc doc = new("Game", "Credits", "Thanks", new[]
        {
            new CreditsSection("Libs", new[] { new CreditsEntry("Lib", "one two three four five six seven", new[] { "m" }) }),
        });
        IReadOnlyList<CreditsLine> lines = doc.Lines(12, t => t.Length);
        Assert.Equal(CreditsLineKind.Title, lines[0].Kind);
        Assert.Equal("Game", lines[0].Text);
        Assert.Equal(CreditsLineKind.Heading, lines[^1].Kind);
        Assert.Equal("Thanks", lines[^1].Text);
        Assert.All(lines.Where(l => l.Kind == CreditsLineKind.Text), l => Assert.True(l.Text.Length <= 12));
        Assert.True(lines.Count(l => l.Kind == CreditsLineKind.Text) >= 4);   // the subtitle and at least three wrapped rows
        Assert.Contains(lines, l => l.Kind == CreditsLineKind.Name && l.Text == "Lib");
    }

    [Fact]
    public void The_roll_moves_up_and_ends_after_the_last_line_leaves()
    {
        var roll = new CreditsRoll();
        Assert.Equal(100, roll.LineY(0, 10, 100));          // the first line starts at the bottom edge
        Assert.False(roll.IsFinished(5, 10, 100));
        roll.Advance(2);
        Assert.Equal(CreditsRoll.QuartersPerUpdate * 2 / 4, roll.Offset);
        Assert.Equal(100 - roll.Offset, roll.LineY(0, 10, 100));
        int updates = 0;
        while (!roll.IsFinished(5, 10, 100) && updates < 10_000) { roll.Advance(); updates++; }
        Assert.InRange(updates, 1, 9_999);
        Assert.True(roll.LineY(5, 10, 100) < 0);
    }
}
