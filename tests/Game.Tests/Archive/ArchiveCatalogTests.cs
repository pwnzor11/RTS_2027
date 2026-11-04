// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;

namespace Rts.Game.Tests.Archive;

public sealed class ArchiveCatalogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_archive_" + Guid.NewGuid().ToString("N"));

    public ArchiveCatalogTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void The_shipped_archive_loads_with_every_category()
    {
        ArchiveCatalog catalog = ArchiveTestData.Catalog();

        Assert.NotEmpty(catalog.Entries);
        foreach (ArchiveCategory category in Enum.GetValues<ArchiveCategory>())
        {
            Assert.True(catalog.InCategory(category).Any(), $"no entries in {category}");
        }
        Assert.All(catalog.Entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Title)));
    }

    [Fact]
    public void Every_entry_id_is_unique()
    {
        ArchiveCatalog catalog = ArchiveTestData.Catalog();

        Assert.Equal(catalog.Entries.Count, catalog.Entries.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_unit_structure_and_upgrade_in_the_rules_has_an_entry_of_the_right_category()
    {
        ArchiveCatalog catalog = ArchiveTestData.Catalog();
        var rules = ArchiveTestData.Rules();

        void Check(string id, ArchiveCategory category)
        {
            ArchiveEntry? e = catalog.Find(id);
            Assert.True(e is not null, $"no archive entry for rules id {id}");
            Assert.Equal(category, e!.Category);
        }

        foreach (var v in rules.Vehicles) Check(v.Id, ArchiveCategory.Unit);
        foreach (var i in rules.Infantry) Check(i.Id, ArchiveCategory.Unit);
        foreach (var b in rules.Buildings) Check(b.Id, ArchiveCategory.Structure);
        foreach (var u in rules.Upgrades) Check(u.Id, ArchiveCategory.Upgrade);
    }

    [Fact]
    public void Every_unit_structure_and_upgrade_entry_names_a_real_rules_id_and_every_unlock_target_exists()
    {
        ArchiveCatalog catalog = ArchiveTestData.Catalog();
        var rules = ArchiveTestData.Rules();

        foreach (ArchiveEntry e in catalog.Entries)
        {
            if (e.HasStats) Assert.True(ArchiveStats.Exists(rules, e.Id), $"{e.Id} is not in the rules");
            if (e.Unlock.Kind == ArchiveUnlockKind.Encounter) Assert.True(ArchiveStats.Exists(rules, e.Unlock.Arg), $"{e.Id}: bad encounter {e.Unlock.Arg}");
            if (e.Unlock.Kind == ArchiveUnlockKind.Research) Assert.Contains(rules.Upgrades, u => u.Id == e.Unlock.Arg);
        }
    }

    [Fact]
    public void The_writers_unlock_suggestions_are_applied()
    {
        ArchiveCatalog catalog = ArchiveTestData.Catalog();

        Assert.Equal(ArchiveUnlockKind.Open, catalog.Find("auth_tank")!.Unlock.Kind);
        Assert.Equal(ArchiveUnlockKind.Open, catalog.Find("history_timeline")!.Unlock.Kind);
        Assert.Equal(new ArchiveUnlock(ArchiveUnlockKind.Encounter, "dir_sunrise"), catalog.Find("dir_sunrise")!.Unlock);
        Assert.Equal(ArchiveUnlockKind.Campaign, catalog.Find("dir_reactor_crawler")!.Unlock.Kind);
    }

    [Fact]
    public void Every_leader_secret_is_a_separate_campaign_entry_and_the_open_entry_no_longer_holds_it()
    {
        ArchiveCatalog catalog = ArchiveTestData.Catalog();
        ArchiveEntry[] secrets = catalog.InCategory(ArchiveCategory.Leader).Where(e => e.Id.EndsWith("_secret", StringComparison.Ordinal)).ToArray();

        Assert.Equal(8, secrets.Length);
        foreach (ArchiveEntry secret in secrets)
        {
            ArchiveEntry open = catalog.Find(secret.Id[..^"_secret".Length])!;
            Assert.Equal(ArchiveUnlockKind.Campaign, secret.Unlock.Kind);
            Assert.EndsWith(": Classified", secret.Title, StringComparison.Ordinal);
            Assert.StartsWith("Secret:", secret.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Secret:", open.Body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void No_entry_is_still_a_draft_stub_except_technology_which_has_no_approved_text_yet()
    {
        // ASSUMED (M3-17): Technology entries have no lore file, so they stay stubs. Directorate upgrades do not exist in
        // the rules yet, so they have no entries at all.
        ArchiveCatalog catalog = ArchiveTestData.Catalog();

        Assert.All(catalog.Entries.Where(e => e.Category != ArchiveCategory.Technology),
            e => Assert.DoesNotContain("[draft pending]", e.Body, StringComparison.Ordinal));
    }

    [Fact]
    public void Units_structures_and_upgrades_carry_a_field_note_and_the_timeline_exists()
    {
        ArchiveCatalog catalog = ArchiveTestData.Catalog();

        Assert.All(catalog.Entries.Where(e => e.HasStats), e => Assert.False(string.IsNullOrWhiteSpace(e.FieldNote), e.Id));
        Assert.Contains("*Oct 1962.*", catalog.Find("history_timeline")!.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void A_duplicate_id_across_files_is_rejected()
    {
        File.WriteAllText(Path.Combine(_dir, "a.toml"), Entry("x"));
        File.WriteAllText(Path.Combine(_dir, "b.toml"), Entry("x"));

        var ex = Assert.Throws<InvalidDataException>(() => ArchiveCatalog.Load(_dir));
        Assert.Contains("'x'", ex.Message);
    }

    [Theory]
    [InlineData("category = \"Nonsense\"")]
    [InlineData("unlock = \"campaign:\"")]
    [InlineData("unlock = \"whenever\"")]
    [InlineData("faction = \"martians\"")]
    public void A_bad_value_is_rejected_with_the_entry_named(string line)
    {
        File.WriteAllText(Path.Combine(_dir, "a.toml"), Entry("x", line));

        var ex = Assert.Throws<InvalidDataException>(() => ArchiveCatalog.Load(_dir));
        Assert.Contains("'x'", ex.Message);
    }

    [Fact]
    public void Malformed_toml_is_rejected_and_a_missing_folder_throws()
    {
        File.WriteAllText(Path.Combine(_dir, "a.toml"), "[[entry]\nid = ");

        Assert.Throws<InvalidDataException>(() => ArchiveCatalog.Load(_dir));
        Assert.Throws<DirectoryNotFoundException>(() => ArchiveCatalog.Load(Path.Combine(_dir, "nope")));
    }

    private static string Entry(string id, string? overrideLine = null)
    {
        var lines = new List<string>
        {
            "category = \"History\"", "faction = \"neutral\"", "title = \"T\"", "body = \"b\"", "unlock = \"open\"",
        };
        if (overrideLine is not null)
        {
            string key = overrideLine.Split('=')[0].Trim();
            lines.RemoveAll(l => l.StartsWith(key + " ", StringComparison.Ordinal));
            lines.Add(overrideLine);
        }
        return "[[entry]]\nid = \"" + id + "\"\n" + string.Join("\n", lines) + "\n";
    }
}
