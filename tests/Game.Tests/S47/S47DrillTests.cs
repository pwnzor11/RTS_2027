// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Drills;
using Rts.Game.Menu;
using Rts.Game.Profile;
using Rts.Scripting;

namespace Rts.Game.Tests.S47;

/// <summary>S47 U-12: the drills list, the medal text, and the profile's drill records (missing and old files are fine).</summary>
public sealed class S47DrillTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_s47_drill_" + Guid.NewGuid().ToString("N"));

    private static string DataDir =>
        Path.GetFullPath(Path.Combine(RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found"), ".."));

    private string ProfilePath => Path.Combine(_dir, "profile.toml");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void A_missing_profile_file_loads_empty_and_the_list_shows_every_drill_as_not_played()
    {
        PlayerProfile p = PlayerProfile.Load(ProfilePath);
        Assert.Empty(p.Drills);
        IReadOnlyList<DrillEntry> list = DrillCatalog.Load(DataDir, p);
        Assert.Equal(new[] { "drill_build", "drill_hold", "drill_raid" }, list.Select(e => e.Id));
        Assert.All(list, e => Assert.Equal("Not played", DrillText.Best(e.Record)));
    }

    [Fact]
    public void An_old_profile_without_drills_still_loads_and_keeps_its_other_data()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ProfilePath, "version = 1\n[skirmish]\nfastest_win_seconds = 300\nbiggest_army = 900\n\n[missions.auth_01]\ncompleted = true\nno_losses = false\nbest_seconds = 400\n");
        PlayerProfile p = PlayerProfile.Load(ProfilePath);
        Assert.Empty(p.Drills);
        Assert.Equal(300, p.FastestWinSeconds);
        Assert.True(p.Missions["auth_01"].Completed);
    }

    [Fact]
    public void Best_medal_and_time_are_saved_and_survive_a_reload()
    {
        var run = new DrillRun(150, 0, 6);
        ProfileRecorder.RecordDrill(ProfilePath, "drill_build", run, DrillMedal.Silver);
        PlayerProfile p = PlayerProfile.Load(ProfilePath);
        Assert.Equal(new DrillRecord(2, 150, 0), p.Drills["drill_build"]);
        Assert.Equal("Silver  2:30", DrillText.Best(p.Drills["drill_build"]));
        Assert.False(File.Exists(ProfilePath + ".tmp"));
    }

    [Fact]
    public void A_worse_run_never_lowers_the_best_medal_and_a_faster_one_lowers_the_time()
    {
        ProfileRecorder.RecordDrill(ProfilePath, "drill_raid", new DrillRun(100, 1, 5), DrillMedal.Gold);
        ProfileRecorder.RecordDrill(ProfilePath, "drill_raid", new DrillRun(200, 3, 3), DrillMedal.Bronze);
        DrillRecord r = PlayerProfile.Load(ProfilePath).Drills["drill_raid"];
        Assert.Equal(3, r.Medal);
        Assert.Equal(100, r.BestSeconds);
        Assert.Equal(1, r.BestLost);
        ProfileRecorder.RecordDrill(ProfilePath, "drill_raid", new DrillRun(80, 0, 6), DrillMedal.Gold);
        r = PlayerProfile.Load(ProfilePath).Drills["drill_raid"];
        Assert.Equal(80, r.BestSeconds);
        Assert.Equal(0, r.BestLost);
    }

    [Fact]
    public void Drill_records_do_not_disturb_campaign_records_and_the_list_reads_them()
    {
        ProfileRecorder.RecordMissionWin(ProfilePath, "auth_01", 20 * 300, 0, 480);
        ProfileRecorder.RecordDrill(ProfilePath, "drill_hold", new DrillRun(120, 2, 6), DrillMedal.Silver);
        PlayerProfile p = PlayerProfile.Load(ProfilePath);
        Assert.True(p.Missions["auth_01"].Completed);
        DrillEntry hold = DrillCatalog.Load(DataDir, p).Single(e => e.Id == "drill_hold");
        Assert.Equal("Silver  2:00", DrillText.Best(hold.Record));
        Assert.Contains("Silver", hold.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hand_edited_drill_record_is_clamped_and_bad_ids_are_skipped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ProfilePath, "version = 1\n[drills.drill_build]\nmedal = 99\nbest_seconds = -5\n\n[drills.\"bad id\"]\nmedal = 1\nbest_seconds = 10\n");
        PlayerProfile p = PlayerProfile.Load(ProfilePath);
        Assert.Equal(new DrillRecord(3, 0, -1), p.Drills["drill_build"]);
        Assert.DoesNotContain("bad id", p.Drills.Keys);
    }

    [Fact]
    public void The_end_screen_model_shows_the_medal_the_run_and_all_three_thresholds()
    {
        DrillDef def = MissionLoader.Load(MissionLoader.FolderOf(DataDir, "drill_raid")).Drill!;
        var run = new DrillRun(def.Silver, 1, def.KeepSilver);
        DrillEndModel model = DrillEndModel.Build("Drill: Raid", def, run, default, new DrillRecord(2, run.Seconds, 1));
        Assert.Equal(DrillMedal.Silver, model.Medal);
        Assert.Equal("Medal: Silver", model.Lines[0]);
        Assert.Contains(model.Lines, l => l.StartsWith("Gold: ", StringComparison.Ordinal));
        Assert.Contains(model.Lines, l => l.StartsWith("Silver: ", StringComparison.Ordinal));
        Assert.Contains(model.Lines, l => l.StartsWith("Bronze: ", StringComparison.Ordinal));
        Assert.Contains("New best medal", model.Lines);
    }

    [Fact]
    public void Threshold_text_reads_plainly_for_time_loss_and_keep_rules()
    {
        Assert.Equal("Gold: 0:50 or less, keep 6 units", DrillText.Threshold(new DrillDef(1, "seconds", 50, 70, 100, 6, 5, 3), DrillMedal.Gold));
        Assert.Equal("Gold: none lost", DrillText.Threshold(new DrillDef(2, "lost", 0, 2, 5), DrillMedal.Gold));
        Assert.Equal("Bronze: 5 or fewer lost", DrillText.Threshold(new DrillDef(2, "lost", 0, 2, 5), DrillMedal.Bronze));
    }

    [Fact]
    public void The_main_menu_has_a_Drills_entry_after_Campaign()
    {
        IReadOnlyList<MainMenuEntry> entries = MainMenuModel.Build(hasSave: false);
        int campaign = entries.ToList().FindIndex(e => e.Action == MenuAction.Campaign);
        Assert.Equal(MenuAction.Drills, entries[campaign + 1].Action);
        Assert.Equal("Drills", entries[campaign + 1].Label);
        Assert.True(entries[campaign + 1].Enabled);
    }
}
