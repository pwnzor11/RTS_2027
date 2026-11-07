// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Archive;
using Rts.Game.Campaign;
using Rts.Game.Screens;
using Rts.Scripting;

namespace Rts.Game.Tests.M8_08;

/// <summary>M8-08 / M8-16: the campaign screens through their view-models (no drawing).</summary>
public sealed class CampaignScreensTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_m808_" + Guid.NewGuid().ToString("N"));

    public CampaignScreensTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static string RulesDir =>
        RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");

    private static string DataDir => Path.GetFullPath(Path.Combine(RulesDir, ".."));

    private CampaignService Service() => new(DataDir, Path.Combine(_dir, "profile"), new ArchiveProgress(), Path.Combine(_dir, "archive.toml"));

    private static FactionPickRow Row(IReadOnlyList<FactionPickRow> rows, string f) => rows.Single(r => r.Faction == f);

    [Fact]
    public void Faction_pick_shows_four_factions_with_the_reason_for_each_lock()
    {
        CampaignService s = Service();
        IReadOnlyList<FactionPickRow> rows = FactionPickModel.Build(s);
        Assert.Equal(4, rows.Count);
        Assert.True(Row(rows, "authority").Open);
        Assert.True(Row(rows, "directorate").Open);
        FactionPickRow lb = Row(rows, "left_behind");
        Assert.False(lb.Open);
        Assert.Equal("opens after act 2 of the Authority or Directorate campaign", lb.Reason);
        Assert.Equal("opens after act 3 of the Authority or Directorate campaign", Row(rows, "expedition").Reason);
        Assert.Contains("locked", lb.Label);
    }

    [Fact]
    public void Finishing_act_two_of_either_campaign_opens_left_behind()
    {
        CampaignService s = Service();
        Assert.True(CampaignProgress.New("directorate").WithWin("dir_02", 3, Array.Empty<string>()).Save(s.ProgressPath("directorate")));
        FactionPickRow lb = Row(FactionPickModel.Build(s), "left_behind");
        Assert.True(lb.Open);
        Assert.Equal("", lb.Reason);
    }

    /// <summary>A scratch data folder with every campaign file but only auth_01 written, so the "not written yet" rows stay testable as missions land.</summary>
    private string DataWithOnlyAuth01()
    {
        string data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(Path.Combine(data, "campaigns"));
        Directory.CreateDirectory(Path.Combine(data, "missions", "auth_01"));
        foreach (string f in Directory.GetFiles(Path.Combine(DataDir, "campaigns"))) File.Copy(f, Path.Combine(data, "campaigns", Path.GetFileName(f)));
        foreach (string f in Directory.GetFiles(Path.Combine(DataDir, "missions", "auth_01"))) File.Copy(f, Path.Combine(data, "missions", "auth_01", Path.GetFileName(f)));
        return data;
    }

    [Fact]
    public void Mission_select_shows_next_completed_locked_and_unwritten_missions()
    {
        string data = DataWithOnlyAuth01();
        CampaignService Svc() => new(data, Path.Combine(_dir, "profile"), new ArchiveProgress(), Path.Combine(_dir, "archive.toml"));
        CampaignService s = Svc();
        MissionSelectRow auth = MissionSelectModel.Build(s, data, "authority").Single(r => r.Id == "auth_01");
        Assert.Equal(CampaignMissionState.Next, auth.State);
        Assert.True(auth.CanStart);
        Assert.Equal("Survey Convoy", auth.Title);

        Assert.True(CampaignProgress.New("authority").WithWin("auth_01", 2, Array.Empty<string>()).Save(s.ProgressPath("authority")));
        Assert.Equal(CampaignMissionState.Completed, MissionSelectModel.Build(Svc(), data, "authority").Single(r => r.Id == "auth_01").State);

        IReadOnlyList<MissionSelectRow> dir = MissionSelectModel.Build(s, data, "directorate");
        MissionSelectRow first = dir[0];
        Assert.False(first.Exists);
        Assert.False(first.CanStart);
        Assert.Equal("not written yet", first.StateText);
        MissionSelectRow second = dir[1];
        Assert.Equal(CampaignMissionState.Locked, second.State);
        Assert.False(second.CanStart);
        Assert.Contains("not written yet", second.Hint);
    }

    [Fact]
    public void Briefing_difficulty_choice_has_five_named_tiers_and_stops_at_the_ends()
    {
        IReadOnlyList<string> names = BriefingModel.LoadTierNames(RulesDir);
        Assert.Equal(5, names.Count);
        var m = new BriefingModel("T", "authority", "text", Array.Empty<string>(), names, 3);
        Assert.Equal(3, m.Tier);
        Assert.Equal(names[2], m.TierName);
        m.AdjustTier(10);
        Assert.Equal(5, m.Tier);
        m.AdjustTier(-10);
        Assert.Equal(1, m.Tier);
        Assert.Equal(names[0], m.TierName);
    }

    [Fact]
    public void Briefing_of_a_real_mission_loads_text_theme_and_objectives_preview()
    {
        var catalog = ArchiveCatalog.Empty;
        BriefingModel m = BriefingModel.Load(DataDir, RulesDir, "auth_01", "authority", 4, catalog, new ArchiveProgress());
        Assert.Equal("Survey Convoy", m.Title);
        Assert.Contains("SURVEY CONVOY", m.Text);
        Assert.Equal(4, m.Tier);
        Assert.NotEmpty(m.Objectives);
    }

    [Fact]
    public void Archive_links_show_the_title_and_mark_locked_entries()
    {
        string archive = Path.Combine(_dir, "archive");
        Directory.CreateDirectory(archive);
        File.WriteAllText(Path.Combine(archive, "a.toml"), """
            [[entry]]
            id = "leader_a"
            category = "Leader"
            faction = "authority"
            title = "Open Person"
            subtitle = "s"
            body = "b"
            unlock = "open"

            [[entry]]
            id = "leader_b"
            category = "Leader"
            faction = "authority"
            title = "Hidden Person"
            subtitle = "s"
            body = "b"
            unlock = "campaign:auth_01"
            """);
        ArchiveCatalog cat = ArchiveCatalog.Load(archive);
        string text = BriefingModel.ResolveLinks("See [[leader_a]], [[leader_b]] and [[nobody]].", cat, new ArchiveProgress());
        Assert.Equal("See Open Person, Hidden Person (locked) and nobody.", text);
        var done = new ArchiveProgress();
        done.MarkCampaignMission("auth_01");
        Assert.Equal("Hidden Person", BriefingModel.ResolveLinks("[[leader_b]]", cat, done));
    }

    [Fact]
    public void Debrief_after_a_win_lists_time_objectives_new_tech_and_new_archive_entries()
    {
        string archive = Path.Combine(_dir, "archive2");
        Directory.CreateDirectory(archive);
        File.WriteAllText(Path.Combine(archive, "a.toml"), """
            [[entry]]
            id = "leader_b"
            category = "Leader"
            faction = "authority"
            title = "Hidden Person"
            subtitle = "s"
            body = "b"
            unlock = "campaign:auth_01"
            """);
        ArchiveCatalog cat = ArchiveCatalog.Load(archive);
        CampaignProgress before = CampaignProgress.New("authority");
        CampaignProgress after = before.WithWin("auth_01", 3, new[] { "auth_tank" });
        var objectives = new[]
        {
            new MissionObjective("a", "Escort", true, ObjectiveStatus.Completed),
            new MissionObjective("b", "Plant refinery", true, ObjectiveStatus.Completed),
            new MissionObjective("c", "Save civilians", false, ObjectiveStatus.Active),
        };
        DebriefModel d = DebriefModel.Build("Survey Convoy", "auth_01", 20 * 125, objectives, before, after, 3, true, cat);
        Assert.Equal("2:05", d.TimeText);
        Assert.Equal((2, 2), (d.PrimaryDone, d.PrimaryTotal));
        Assert.Equal((0, 1), (d.OptionalDone, d.OptionalTotal));
        Assert.Equal(new[] { "auth_tank" }, d.NewTech);
        Assert.Equal(new[] { "Hidden Person" }, d.NewArchive);
        Assert.True(d.FinalMission);
        Assert.Contains("New tech: auth_tank", d.Lines());

        // A replay of the same mission gives no new Archive entries and no new tech.
        DebriefModel again = DebriefModel.Build("Survey Convoy", "auth_01", 100, objectives, after, after.WithWin("auth_01", 3, new[] { "auth_tank" }), 3, true, cat);
        Assert.Empty(again.NewArchive);
        Assert.Empty(again.NewTech);
    }

    [Fact]
    public void Record_win_through_the_service_feeds_the_debrief()
    {
        CampaignService s = Service();
        CampaignProgress before = s.Progress("authority");
        CampaignProgress after = s.RecordWin("authority", "auth_01", 2);
        DebriefModel d = DebriefModel.Build("Survey Convoy", "auth_01", 600, Array.Empty<MissionObjective>(), before, after, 2, MissionSelectModel.IsFinal(s, "authority", "auth_01"), ArchiveCatalog.Empty);
        Assert.False(d.FinalMission);   // s12: auth_01 is act 1 of 4
        Assert.True(MissionSelectModel.IsFinal(s, "authority", "auth_04"));
        Assert.Equal(CampaignMissionState.Completed, MissionSelectModel.Build(s, DataDir, "authority").Single(r => r.Id == "auth_01").State);
    }

    [Fact]
    public void Ending_slides_follow_file_order_and_the_progress_conditions()
    {
        EndingDef def = EndingDef.Parse("""
            [ending]
            faction = "authority"
            [[slides]]
            image = "ending_authority_01"
            key = "k1"
            [[slides]]
            image = "ending_authority_02"
            key = "k2"
            condition = { kind = "completed", mission = "auth_01" }
            [text]
            "k1" = "first"
            "k2" = "second"
            """);
        var empty = new EndingModel(def, CampaignProgress.New("authority"));
        Assert.Single(empty.Frames);
        var full = new EndingModel(def, CampaignProgress.New("authority").WithWin("auth_01", 3, Array.Empty<string>()));
        Assert.Equal(new[] { "ending_authority_01", "ending_authority_02" }, full.Frames.Select(f => f.Image));
        Assert.Equal("first", full.Current!.Caption);
        Assert.Equal(0, full.Index);
        full.Next();
        Assert.Equal("ending_authority_02", full.Current!.Image);
        Assert.False(full.Finished);
        full.Next();
        Assert.True(full.Finished);
        Assert.Null(full.Current);
    }

    [Fact]
    public void Ending_difficulty_condition_filters_and_skip_ends_at_once()
    {
        EndingDef def = EndingDef.Parse("""
            [ending]
            faction = "authority"
            [[slides]]
            image = "i1"
            key = "k1"
            [[slides]]
            image = "i2"
            key = "k2"
            condition = { kind = "difficulty_min", mission = "auth_01", value = 4 }
            [text]
            "k1" = "one"
            "k2" = "two"
            """);
        CampaignProgress easy = CampaignProgress.New("authority").WithWin("auth_01", 2, Array.Empty<string>());
        CampaignProgress hard = CampaignProgress.New("authority").WithWin("auth_01", 5, Array.Empty<string>());
        Assert.Equal(new[] { "one" }, new EndingModel(def, easy).Frames.Select(f => f.Caption));
        var m = new EndingModel(def, hard);
        Assert.Equal(new[] { "one", "two" }, m.Frames.Select(f => f.Caption));
        m.Skip();
        Assert.True(m.Finished);
        Assert.Null(m.Current);
    }
}
