// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;

namespace Rts.Game.Tests.Archive;

public sealed class ArchiveProgressTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_progress_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static ArchiveEntry Entry(string unlock)
    {
        Assert.True(ArchiveUnlock.TryParse(unlock, out ArchiveUnlock rule));
        return new ArchiveEntry("e", ArchiveCategory.History, ArchiveFaction.Neutral, "E", "", "", "", "", rule, "test");
    }

    [Fact]
    public void Open_is_always_unlocked_and_each_other_rule_needs_its_own_record()
    {
        var p = new ArchiveProgress();
        ArchiveEntry open = Entry("open"), camp = Entry("campaign:auth_01"), enc = Entry("encounter:dir_sunrise"), res = Entry("research:auth_grouting");

        Assert.True(p.IsUnlocked(open));
        Assert.False(p.IsUnlocked(camp));
        Assert.False(p.IsUnlocked(enc));
        Assert.False(p.IsUnlocked(res));

        // Records of the wrong kind or id do not unlock.
        p.RecordEncounter("auth_grouting");
        p.RecordResearch("dir_sunrise");
        p.MarkCampaignMission("dir_sunrise");
        Assert.False(p.IsUnlocked(camp));
        Assert.False(p.IsUnlocked(enc));
        Assert.False(p.IsUnlocked(res));

        p.MarkCampaignMission("auth_01");
        p.RecordEncounter("dir_sunrise");
        p.RecordResearch("auth_grouting");
        Assert.True(p.IsUnlocked(camp));
        Assert.True(p.IsUnlocked(enc));
        Assert.True(p.IsUnlocked(res));
    }

    [Fact]
    public void Recording_twice_reports_new_only_once_and_sets_dirty()
    {
        var p = new ArchiveProgress();

        Assert.False(p.Dirty);
        Assert.True(p.RecordEncounter("dir_sunrise"));
        Assert.False(p.RecordEncounter("dir_sunrise"));
        Assert.False(p.RecordEncounter(" "));
        Assert.True(p.Dirty);
    }

    [Fact]
    public void A_saved_record_round_trips()
    {
        string path = Path.Combine(_dir, "sub", "archive_progress.toml");
        var p = new ArchiveProgress();
        p.RecordEncounter("dir_sunrise");
        p.RecordEncounter("dir_commissar");
        p.RecordResearch("auth_grouting");
        p.MarkCampaignMission("auth_01");

        Assert.True(p.Save(path));
        Assert.False(p.Dirty);
        ArchiveProgress back = ArchiveProgress.Load(path);

        Assert.Equal(p.Encounters, back.Encounters);
        Assert.Equal(p.Research, back.Research);
        Assert.Equal(p.Missions, back.Missions);
        Assert.False(back.Dirty);
    }

    [Fact]
    public void Odd_characters_in_ids_survive_the_round_trip()
    {
        var p = new ArchiveProgress();
        p.RecordEncounter("we\"ird\\id");

        ArchiveProgress back = ArchiveProgress.Parse(p.ToToml());

        Assert.Contains("we\"ird\\id", back.Encounters);
    }

    [Fact]
    public void A_missing_file_gives_an_empty_record()
    {
        ArchiveProgress p = ArchiveProgress.Load(Path.Combine(_dir, "nothing.toml"));

        Assert.Empty(p.Encounters);
        Assert.Empty(p.Research);
        Assert.Empty(p.Missions);
    }

    [Theory]
    [InlineData("this is { not toml")]
    [InlineData("encounters = 5\nresearch = \"x\"\nmissions = [1, 2]")]
    [InlineData("version = 99\nencounters = [\"a\"]")]
    [InlineData("\0\0\0")]
    [InlineData("")]
    public void A_corrupt_file_gives_an_empty_record_and_never_throws(string text)
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "bad.toml");
        File.WriteAllText(path, text);

        ArchiveProgress p = ArchiveProgress.Load(path);

        Assert.Empty(p.Encounters);
        Assert.Empty(p.Missions);
        Assert.True(p.RecordEncounter("ok"));   // and it is still usable and can overwrite the bad file
        Assert.True(p.Save(path));
        Assert.Contains("ok", ArchiveProgress.Load(path).Encounters);
    }

    [Fact]
    public void Wrong_typed_items_are_skipped_but_good_ones_kept()
    {
        ArchiveProgress p = ArchiveProgress.Parse("encounters = [\"a\", 7, \"b\"]\nmissions = [\"auth_01\"]");

        Assert.Equal(new[] { "a", "b" }, p.Encounters);
        Assert.Contains("auth_01", p.Missions);
    }

    [Fact]
    public void The_default_path_sits_beside_the_settings_file()
    {
        Assert.Equal(Path.GetDirectoryName(Settings.DefaultPath), Path.GetDirectoryName(ArchiveProgress.DefaultPath));
        Assert.EndsWith("archive_progress.toml", ArchiveProgress.DefaultPath, StringComparison.Ordinal);
    }
}
