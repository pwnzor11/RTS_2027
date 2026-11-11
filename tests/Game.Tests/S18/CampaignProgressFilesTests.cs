// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Archive;
using Rts.Game.Campaign;
using Rts.Game.Screens;

namespace Rts.Game.Tests.S18;

/// <summary>S18 GS-01, GS-04 / UX-05, GS-05: campaign and Archive progress files and the Campaign screen rows.</summary>
public sealed class CampaignProgressFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_s18_cpf_" + Guid.NewGuid().ToString("N"));

    public CampaignProgressFilesTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private static string DataDir =>
        Path.GetFullPath(Path.Combine(RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found"), ".."));

    private string Profile => Path.Combine(_dir, "profile");
    private string ArchivePath => Path.Combine(_dir, "archive.toml");

    // ---- GS-01 ----

    [Fact]
    public void A_win_keeps_archive_unlocks_the_tracker_wrote_after_the_campaign_screen_opened()
    {
        var svc = new CampaignService(DataDir, Profile, null, ArchivePath);   // the Campaign screen opens: archive read now (empty)
        var tracker = new ArchiveTracker(ArchiveProgress.Load(ArchivePath), ArchivePath);   // the mission's tracker, as GameScreen.Enter makes it
        Assert.True(tracker.Progress.RecordEncounter("auth_tank"));
        Assert.True(tracker.Progress.Save(ArchivePath));

        svc.RecordWin("authority", "auth_01", 3);

        ArchiveProgress disk = ArchiveProgress.Load(ArchivePath);
        Assert.Contains("auth_tank", disk.Encounters);
        Assert.Contains("auth_01", disk.Missions);
    }

    // ---- GS-04 / UX-05 ----

    [Fact]
    public void The_lock_hint_skips_the_tutorial_row()
    {
        var svc = new CampaignService(DataDir, Profile, new ArchiveProgress(), ArchivePath);
        var screen = new CampaignScreen(null!, svc);   // no graphics needed: the menu actions are not run
        IReadOnlyList<FactionPickRow> rows = screen.Rows;
        Assert.Equal("", screen.HintAt(0));   // Tutorial
        for (int i = 0; i < rows.Count; i++) Assert.Equal(rows[i].Reason, screen.HintAt(i + 1));
        Assert.Equal("", screen.HintAt(rows.Count + 1));   // Load game
        int lb = rows.ToList().FindIndex(r => r.Faction == "left_behind");
        Assert.Equal("opens after act 2 of the Authority or Directorate campaign", screen.HintAt(lb + 1));
    }

    [Fact]
    public void The_campaign_screen_rows_refresh_after_a_win()
    {
        var svc = new CampaignService(DataDir, Profile, new ArchiveProgress(), ArchivePath);
        var screen = new CampaignScreen(null!, svc);
        Assert.False(screen.Rows.Single(r => r.Faction == "left_behind").Open);
        Assert.Equal(0, screen.Rows.Single(r => r.Faction == "authority").Done);

        svc.RecordWin("authority", "auth_01", 3);
        svc.RecordWin("authority", "auth_02", 3);
        screen.Refresh();

        Assert.True(screen.Rows.Single(r => r.Faction == "left_behind").Open);
        Assert.Equal(2, screen.Rows.Single(r => r.Faction == "authority").Done);
    }

    // ---- GS-05 ----

    [Theory]
    [InlineData("this is = not [ toml")]
    [InlineData("version = 99\nfaction = \"authority\"\ncompleted = [\"auth_01\"]\n")]
    public void An_unreadable_or_newer_campaign_progress_file_is_kept_as_bad(string text)
    {
        string path = Path.Combine(_dir, "campaign_authority.toml");
        File.WriteAllText(path, text);
        CampaignProgress p = CampaignProgress.Load("authority", path);
        Assert.Empty(p.Completed);
        Assert.Equal(text, File.ReadAllText(path + ".bad"));
        Assert.True(p.WithWin("auth_01", 3, Array.Empty<string>()).Save(path));
        Assert.Equal(text, File.ReadAllText(path + ".bad"));   // the next save does not destroy it
    }

    [Theory]
    [InlineData("encounters = [ broken")]
    [InlineData("version = 7\nencounters = [\"auth_tank\"]\n")]
    public void An_unreadable_or_newer_archive_progress_file_is_kept_as_bad(string text)
    {
        File.WriteAllText(ArchivePath, text);
        ArchiveProgress p = ArchiveProgress.Load(ArchivePath);
        Assert.Empty(p.Encounters);
        Assert.Equal(text, File.ReadAllText(ArchivePath + ".bad"));
        p.RecordEncounter("auth_jeep");
        Assert.True(p.Save(ArchivePath));
        Assert.Equal(text, File.ReadAllText(ArchivePath + ".bad"));
        Assert.Contains("auth_jeep", ArchiveProgress.Load(ArchivePath).Encounters);
        Assert.False(File.Exists(ArchivePath + ".tmp"));
    }

    [Fact]
    public void A_good_file_is_not_moved()
    {
        string path = Path.Combine(_dir, "campaign_authority.toml");
        Assert.True(CampaignProgress.New("authority").WithWin("auth_01", 2, Array.Empty<string>()).Save(path));
        Assert.True(CampaignProgress.Load("authority", path).IsCompleted("auth_01"));
        Assert.False(File.Exists(path + ".bad"));
        var a = new ArchiveProgress();
        a.RecordEncounter("auth_tank");
        Assert.True(a.Save(ArchivePath));
        Assert.Contains("auth_tank", ArchiveProgress.Load(ArchivePath).Encounters);
        Assert.False(File.Exists(ArchivePath + ".bad"));
    }

    [Fact]
    public void A_failed_progress_save_is_reported()
    {
        string blocker = Path.Combine(_dir, "blocked");
        File.WriteAllText(blocker, "a file where the profile folder should be");
        var svc = new CampaignService(DataDir, blocker, new ArchiveProgress(), ArchivePath);
        svc.RecordWin("authority", "auth_01", 3);
        Assert.False(svc.LastWinSaved);

        var ok = new CampaignService(DataDir, Profile, new ArchiveProgress(), ArchivePath);
        ok.RecordWin("authority", "auth_01", 3);
        Assert.True(ok.LastWinSaved);
    }

    [Fact]
    public void Settings_save_goes_through_a_temporary_file()
    {
        string path = Path.Combine(_dir, "settings.toml");
        var s = new Settings();
        Assert.True(s.Save(path));
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }
}
