// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Campaign;

namespace Rts.Game.Tests.S45Sys;

/// <summary>U-08 (GAMESYS-04): one bad value skips only that entry, and the player gets a warning instead of a silent reset.</summary>
public sealed class CampaignPartialLoadTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_s45cp_" + Guid.NewGuid().ToString("N"));

    public CampaignPartialLoadTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string Write(string text)
    {
        string file = Path.Combine(_dir, "campaign_authority.toml");
        File.WriteAllText(file, text);
        return file;
    }

    [Fact]
    public void A_bad_difficulty_entry_is_skipped_and_the_rest_of_the_progress_is_kept()
    {
        string file = Write("""
            version = 1
            faction = "authority"
            completed = ["m1", "m2"]
            techSet = ["tech_a"]

            [difficulty]
            m1 = 3
            m2 = 9
            """);

        (CampaignProgress p, IReadOnlyList<string> warnings) = CampaignProgress.LoadWithWarnings("authority", file);

        Assert.Equal(new[] { "m1", "m2" }, p.Completed);
        Assert.Equal(new[] { "tech_a" }, p.TechSet);
        Assert.Equal(3, p.Difficulty["m1"]);
        Assert.False(p.Difficulty.ContainsKey("m2"));
        Assert.Single(warnings);
        Assert.Contains("m2", warnings[0]);
        Assert.True(File.Exists(file), "a partly bad file is not set aside");
        Assert.False(File.Exists(file + ".bad"));
    }

    [Fact]
    public void A_list_of_the_wrong_type_is_skipped_with_a_warning_and_the_other_lists_load()
    {
        string file = Write("""
            version = 1
            faction = "authority"
            completed = ["m1"]
            techSet = "oops"
            """);

        (CampaignProgress p, IReadOnlyList<string> warnings) = CampaignProgress.LoadWithWarnings("authority", file);

        Assert.Equal(new[] { "m1" }, p.Completed);
        Assert.Empty(p.TechSet);
        Assert.Single(warnings);
        Assert.Contains("techSet", warnings[0]);
    }

    [Fact]
    public void A_file_that_cannot_be_parsed_is_set_aside_and_the_player_is_told()
    {
        string file = Write("version = 1\nfaction = \"authority\"\ncompleted = [");

        (CampaignProgress p, IReadOnlyList<string> warnings) = CampaignProgress.LoadWithWarnings("authority", file);

        Assert.Empty(p.Completed);
        Assert.False(File.Exists(file));
        Assert.True(File.Exists(file + ".bad"));
        Assert.Single(warnings);
        Assert.Contains(".bad", warnings[0]);
    }

    [Fact]
    public void A_clean_file_gives_no_warnings()
    {
        string file = Write("version = 1\nfaction = \"authority\"\ncompleted = [\"m1\"]\n");

        (CampaignProgress p, IReadOnlyList<string> warnings) = CampaignProgress.LoadWithWarnings("authority", file);

        Assert.Equal(new[] { "m1" }, p.Completed);
        Assert.Empty(warnings);
    }

    [Fact]
    public void The_strict_parser_still_rejects_a_bad_value()
    {
        Assert.Throws<InvalidDataException>(() => CampaignProgress.Parse("version = 1\nfaction = \"authority\"\n[difficulty]\nm1 = 9\n"));
    }
}
