// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Campaign;

namespace Rts.Game.Tests.Campaign;

/// <summary>M8-02: the campaign progress file, the campaign list and the ending file read and write (plan sections 3.5, 3.8, 4).</summary>
public sealed class CampaignFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_campaign_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static string DataDir =>
        Path.GetFullPath(Path.Combine(RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found"), ".."));

    private static void AssertSame(CampaignProgress a, CampaignProgress b)
    {
        Assert.Equal(a.Version, b.Version);
        Assert.Equal(a.Faction, b.Faction);
        Assert.Equal(a.Completed, b.Completed);
        Assert.Equal(a.TechSet, b.TechSet);
        Assert.Equal(a.Difficulty.OrderBy(k => k.Key), b.Difficulty.OrderBy(k => k.Key));
    }

    [Fact]
    public void Progress_round_trips_through_toml_text_and_a_file()
    {
        CampaignProgress p = CampaignProgress.New("authority")
            .WithWin("auth_01", 2, new[] { "auth_tank", "auth_barracks" })
            .WithWin("auth_02", 4, new[] { "auth_tank", "auth_wall" });
        Assert.Equal(new[] { "auth_01", "auth_02" }, p.Completed);
        Assert.Equal(new[] { "auth_barracks", "auth_tank", "auth_wall" }, p.TechSet);

        AssertSame(p, CampaignProgress.Parse(p.ToToml()));
        string file = Path.Combine(_dir, "sub", "campaign_authority.toml");
        Assert.True(p.Save(file));
        AssertSame(p, CampaignProgress.Load("authority", file));
        Assert.False(File.Exists(file + ".tmp"));
        Assert.Equal(p.ToToml(), CampaignProgress.Load("authority", file).ToToml());
    }

    [Fact]
    public void An_empty_record_round_trips_and_a_win_keeps_the_highest_tier_once()
    {
        CampaignProgress e = CampaignProgress.New("directorate");
        AssertSame(e, CampaignProgress.Parse(e.ToToml()));
        CampaignProgress p = e.WithWin("dir_01", 4, Array.Empty<string>()).WithWin("dir_01", 2, Array.Empty<string>());
        Assert.Single(p.Completed);
        Assert.Equal(4, p.Difficulty["dir_01"]);
        Assert.Empty(e.Completed);   // immutable
    }

    [Fact]
    public void Load_never_throws_and_ignores_files_that_are_missing_corrupt_newer_or_for_another_faction()
    {
        Directory.CreateDirectory(_dir);
        Assert.Empty(CampaignProgress.Load("authority", Path.Combine(_dir, "none.toml")).Completed);

        string bad = Path.Combine(_dir, "bad.toml");
        File.WriteAllText(bad, "this is [not toml");
        Assert.Equal("authority", CampaignProgress.Load("authority", bad).Faction);

        File.WriteAllText(bad, "version = 99\nfaction = \"authority\"\ncompleted = [\"a\"]\n");
        Assert.Empty(CampaignProgress.Load("authority", bad).Completed);

        var other = CampaignProgress.New("expedition").WithWin("exp_01", 3, Array.Empty<string>());
        other.Save(bad);
        Assert.Empty(CampaignProgress.Load("authority", bad).Completed);
    }

    [Theory]
    [InlineData("faction = \"a\"\n")]                                                    // no version
    [InlineData("version = 1\n")]                                                          // no faction
    [InlineData("version = 1\nfaction = \"a\"\ncompleted = [1]\n")]
    [InlineData("version = 1\nfaction = \"a\"\n[difficulty]\nm = 9\n")]
    public void Parse_rejects_malformed_progress(string text) => Assert.Throws<InvalidDataException>(() => CampaignProgress.Parse(text));

    [Fact]
    public void Progress_with_awkward_ids_survives_quoting()
    {
        CampaignProgress p = CampaignProgress.New("authority").WithWin("we\"ird\\id", 1, new[] { "a b" });
        AssertSame(p, CampaignProgress.Parse(p.ToToml()));
    }

    [Fact]
    public void Sample_authority_campaign_lists_auth_01_and_points_at_real_files()
    {
        CampaignDef def = CampaignDef.Load(CampaignDef.PathOf(DataDir, "authority"));
        Assert.Equal("authority", def.Faction);
        Assert.Equal(new[] { "auth_01", "auth_02", "auth_03", "auth_04" }, def.Missions.Select(m => m.Id));   // s12: acts 1-4
        Assert.True(Directory.Exists(Path.Combine(DataDir, "missions", "auth_01")));
        Assert.True(File.Exists(EndingDef.PathOf(DataDir, "authority")));
        Assert.Equal("authority_ending", def.Ending);
        Assert.True(def.IsOpen(Array.Empty<string>()));
        Assert.True(def.IsMissionAvailable("auth_01", CampaignProgress.New("authority")));
        Assert.False(def.IsMissionAvailable("auth_09", CampaignProgress.New("authority")));
    }

    [Fact]
    public void Campaign_rules_gate_missions_and_factions()
    {
        CampaignDef def = CampaignDef.Parse("""
            [campaign]
            faction = "left_behind"
            open_after = ["auth_02", "dir_02"]
            [[missions]]
            id = "lb_01"
            [[missions]]
            id = "lb_02"
            requires = ["lb_01"]
            """);
        Assert.False(def.IsOpen(new[] { "auth_01" }));
        Assert.True(def.IsOpen(new[] { "auth_01", "dir_02" }));
        CampaignProgress p = CampaignProgress.New("left_behind");
        Assert.False(def.IsMissionAvailable("lb_02", p));
        Assert.True(def.IsMissionAvailable("lb_02", p.WithWin("lb_01", 1, Array.Empty<string>())));
    }

    [Theory]
    [InlineData("[campaign]\nfaction = \"authority\"\n")]                                                                   // no missions
    [InlineData("[campaign]\nfaction = \"orcs\"\n[[missions]]\nid = \"a\"\n")]
    [InlineData("[campaign]\nfaction = \"authority\"\n[[missions]]\nid = \"a\"\n[[missions]]\nid = \"a\"\n")]
    [InlineData("[campaign]\nfaction = \"authority\"\n[[missions]]\nid = \"a\"\nrequires = [\"zz\"]\n")]
    public void Bad_campaign_files_are_rejected(string text) => Assert.Throws<InvalidDataException>(() => CampaignDef.Parse(text));

    [Fact]
    public void Sample_ending_has_two_placeholder_slides_and_one_is_conditional()
    {
        EndingDef e = EndingDef.Load(EndingDef.PathOf(DataDir, "authority"));
        Assert.Equal("authority", e.Faction);
        Assert.Equal(2, e.Slides.Count);
        Assert.All(e.Slides, s => Assert.Equal("PENDING owner text", e.Text[s.Key]));
        Assert.Equal(new[] { "ending_authority_01", "ending_authority_02" }, e.Slides.Select(s => s.Image));
        Assert.Single(e.SlidesFor(CampaignProgress.New("authority")));
        Assert.Equal(2, e.SlidesFor(CampaignProgress.New("authority").WithWin("auth_01", 1, Array.Empty<string>())).Count);
    }

    [Fact]
    public void Ending_conditions_read_difficulty()
    {
        EndingDef e = EndingDef.Parse("""
            [ending]
            faction = "expedition"
            [[slides]]
            image = "i1"
            key = "k1"
            condition = { kind = "difficulty_min", mission = "exp_04", value = 3 }
            [text]
            k1 = "PENDING owner text"
            """);
        CampaignProgress p = CampaignProgress.New("expedition");
        Assert.Empty(e.SlidesFor(p));
        Assert.Empty(e.SlidesFor(p.WithWin("exp_04", 2, Array.Empty<string>())));
        Assert.Single(e.SlidesFor(p.WithWin("exp_04", 3, Array.Empty<string>())));
    }

    [Theory]
    [InlineData("[ending]\nfaction = \"authority\"\n")]                                                                      // no slides
    [InlineData("[ending]\nfaction = \"authority\"\n[[slides]]\nimage = \"i\"\nkey = \"k\"\n")]                                // key without text
    [InlineData("[ending]\nfaction = \"authority\"\n[[slides]]\nimage = \"i\"\nkey = \"k\"\ncondition = { kind = \"x\", mission = \"m\" }\n[text]\nk = \"t\"\n")]
    [InlineData("[ending]\nfaction = \"authority\"\n[[slides]]\nimage = \"i\"\nkey = \"k\"\ncondition = { kind = \"difficulty_min\", mission = \"m\" }\n[text]\nk = \"t\"\n")]
    public void Bad_ending_files_are_rejected(string text) => Assert.Throws<InvalidDataException>(() => EndingDef.Parse(text));
}
