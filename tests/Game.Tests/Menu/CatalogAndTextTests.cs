// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Menu;

namespace Rts.Game.Tests.Menu;

public sealed class CatalogAndTextTests
{
    private static string Temp()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts2027-menu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void No_save_folder_means_no_saves_and_continue_stays_off()
    {
        string root = Path.Combine(Path.GetTempPath(), "rts2027-nosaves-" + Guid.NewGuid().ToString("N"));
        var catalog = new SaveCatalog(root);

        Assert.Empty(catalog.List(SaveKind.Campaign));
        Assert.Empty(catalog.List(SaveKind.Skirmish));
        Assert.Null(catalog.Newest());
        Assert.False(MainMenuModel.Build(catalog.Newest() is not null)[0].Enabled);
    }

    [Fact]
    public void A_file_that_is_not_a_save_is_listed_as_unreadable_and_never_offered_to_continue()
    {
        string root = Temp();
        try
        {
            var catalog = new SaveCatalog(root);
            Directory.CreateDirectory(catalog.FolderOf(SaveKind.Skirmish));
            File.WriteAllText(Path.Combine(catalog.FolderOf(SaveKind.Skirmish), "junk" + SaveCatalog.Extension), "not a save");

            SaveEntry entry = Assert.Single(catalog.List(SaveKind.Skirmish));
            Assert.False(entry.Readable);
            Assert.Equal("junk", entry.Name);
            Assert.Null(catalog.Newest());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Replays_are_listed_newest_first_and_a_broken_one_says_so()
    {
        string dir = Temp();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "a.rpl"), new byte[] { 1, 2, 3 });
            File.SetLastWriteTimeUtc(Path.Combine(dir, "a.rpl"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.WriteAllBytes(Path.Combine(dir, "b.rpl"), new byte[] { 4, 5, 6 });
            File.SetLastWriteTimeUtc(Path.Combine(dir, "b.rpl"), new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            File.WriteAllText(Path.Combine(dir, "ignored.txt"), "x");

            IReadOnlyList<ReplayEntry> list = new ReplayCatalog(dir).List();

            Assert.Equal(new[] { "b.rpl", "a.rpl" }, list.Select(r => r.Name));
            Assert.All(list, r => Assert.False(r.Readable));
            Assert.Contains("replay verify", list[0].VerifyCommand);
            Assert.Empty(new ReplayCatalog(Path.Combine(dir, "missing")).List());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Wrap_breaks_at_spaces_keeps_blank_lines_and_never_loses_words()
    {
        const string text = "alpha beta gamma\n\ndelta epsilon";
        List<string> lines = TextWrap.Wrap(text, 10, s => s.Length);

        Assert.Equal(new[] { "alpha beta", "gamma", "", "delta", "epsilon" }, lines);
        Assert.Equal(new[] { "supercalifragilistic" }, TextWrap.Wrap("supercalifragilistic", 5, s => s.Length));
        Assert.Equal(text.Replace("\n", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries), lines.SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public void About_page_has_the_gpl_notice_and_reads_the_credits_table()
    {
        Assert.Contains("GNU General Public License", AboutContent.Notice);
        Assert.Contains("version 3", AboutContent.Notice);

        const string md = "| Library | Licence | Use |\n|---|---|---|\n| Tomlyn 2.10.1 | BSD-2-Clause | TOML |\n| `NVorbis 0.10.4` | MIT | Ogg |\n";
        Assert.Equal(new[] { "Tomlyn 2.10.1 - BSD-2-Clause", "NVorbis 0.10.4 - MIT" }, AboutContent.ParseCredits(md));
        Assert.Same(AboutContent.FallbackCredits, AboutContent.ParseCredits("no tables here"));
        Assert.Same(AboutContent.FallbackCredits, AboutContent.ParseCredits(null));
    }

    [Fact]
    public void The_real_credits_file_gives_a_credit_per_library()
    {
        string? root = SceneSetTests.DataDir is { } d ? Path.GetDirectoryName(d) : null;
        string file = Path.Combine(root!, "docs", "CREDITS.md");
        if (!File.Exists(file)) return;

        IReadOnlyList<string> credits = AboutContent.ParseCredits(File.ReadAllText(file));

        Assert.Contains(credits, c => c.StartsWith("MonoGame", StringComparison.Ordinal));
        Assert.Contains(credits, c => c.StartsWith("Tomlyn", StringComparison.Ordinal));
        Assert.Contains(credits, c => c.StartsWith("Noto Sans", StringComparison.Ordinal));
    }

    [Fact]
    public void How_to_play_covers_the_required_topics_in_a_few_pages()
    {
        string all = string.Join(' ', HowToPlayContent.Pages.SelectMany(p => p.Lines.Append(p.Title))).ToLowerInvariant();
        foreach (string topic in new[] { "select", "move", "attack", "build", "harvest", "tunnel", "upgrade", "hotkey" })
            Assert.Contains(topic, all);
        Assert.InRange(HowToPlayContent.Pages.Count, 6, 10);
        Assert.All(HowToPlayContent.Pages, p => Assert.NotEmpty(p.Lines));
        Assert.Equal(HowToPlayContent.Pages.Count, HowToPlayContent.Pages.Select(p => p.Title).Distinct().Count());
    }
}
