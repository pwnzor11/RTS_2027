// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Profile;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M10;

public sealed class ProfileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_m10_profile_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private string File1 => Path.Combine(_dir, "profile.toml");

    [Fact]
    public void Win_always_earns_completed_and_no_losses_only_without_unit_losses()
    {
        MissionRecord clean = MedalRules.ForWin(20 * 90, 0);
        MissionRecord costly = MedalRules.ForWin(20 * 90, 3);
        Assert.True(clean.Completed);
        Assert.True(clean.NoLosses);
        Assert.Equal(90, clean.BestSeconds);
        Assert.True(costly.Completed);
        Assert.False(costly.NoLosses);
        Assert.Equal(new[] { "Completed", "No losses" }, MedalRules.Names(clean));
        Assert.Equal(new[] { "Completed" }, MedalRules.Names(costly));
    }

    [Fact]
    public void Merge_never_loses_a_medal_and_keeps_the_faster_time()
    {
        var a = new MissionRecord(true, true, 300);
        var b = new MissionRecord(true, false, 200);
        MissionRecord m = a.Merge(b);
        Assert.True(m.NoLosses);
        Assert.Equal(200, m.BestSeconds);
        Assert.Equal(120, default(MissionRecord).Merge(new MissionRecord(true, false, 120)).BestSeconds);
    }

    [Fact]
    public void New_medals_are_only_the_ones_not_held_before()
    {
        MissionRecord before = MedalRules.ForWin(2000, 5);
        Assert.Equal(new[] { "No losses" }, MedalRules.NewNames(before, MedalRules.ForWin(2000, 0)));
        Assert.Empty(MedalRules.NewNames(before, MedalRules.ForWin(2000, 5)));
        Assert.Equal(new[] { "Completed" }, MedalRules.NewNames(default, MedalRules.ForWin(2000, 1)));
    }

    [Fact]
    public void Round_trips_through_a_file()
    {
        PlayerProfile p = PlayerProfile.Empty
            .WithMission("auth_01", MedalRules.ForWin(20 * 100, 0))
            .WithMission("dir_02", MedalRules.ForWin(20 * 200, 4))
            .WithSkirmish(Faction.Authority, 2, true, 600, 4000)
            .WithSkirmish(Faction.Authority, 2, false, 300, 2500)
            .WithSkirmish(Faction.Directorate, 3, true, 450, 9000);
        Assert.True(p.Save(File1));

        PlayerProfile q = PlayerProfile.Load(File1);
        Assert.Equal(p.ToToml(), q.ToToml());
        Assert.True(q.Missions["auth_01"].NoLosses);
        Assert.False(q.Missions["dir_02"].NoLosses);
        Assert.Equal(new SkirmishTally(1, 1), q.ByFaction[Faction.Authority]);
        Assert.Equal(new SkirmishTally(1, 0), q.ByFaction[Faction.Directorate]);
        Assert.Equal(new SkirmishTally(1, 1), q.ByAiTier[2]);
        Assert.Equal(450, q.FastestWinSeconds);
        Assert.Equal(9000, q.BiggestArmy);
    }

    [Fact]
    public void Fastest_win_ignores_losses_and_slower_wins()
    {
        PlayerProfile p = PlayerProfile.Empty.WithSkirmish(Faction.Authority, 1, false, 100, 10)
            .WithSkirmish(Faction.Authority, 1, true, 500, 10).WithSkirmish(Faction.Authority, 1, true, 700, 10);
        Assert.Equal(500, p.FastestWinSeconds);
    }

    [Fact]
    public void Missing_file_gives_an_empty_profile()
    {
        PlayerProfile p = PlayerProfile.Load(File1);
        Assert.Empty(p.Missions);
        Assert.Equal(0, p.BiggestArmy);
    }

    [Fact]
    public void Corrupt_file_gives_an_empty_profile_and_is_kept_as_bad()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(File1, "this is [[ not toml ===");
        PlayerProfile p = PlayerProfile.Load(File1);
        Assert.Empty(p.Missions);
        Assert.False(File.Exists(File1));
        Assert.Equal("this is [[ not toml ===", File.ReadAllText(File1 + ".bad"));
        Assert.True(p.Save(File1));   // the next save works
    }

    [Fact]
    public void Newer_version_is_treated_as_unreadable()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(File1, "version = 99\n[missions.a]\ncompleted = true\n");
        Assert.Empty(PlayerProfile.Load(File1).Missions);
        Assert.True(File.Exists(File1 + ".bad"));
    }

    [Fact]
    public void Odd_values_are_skipped_or_clamped()
    {
        PlayerProfile p = PlayerProfile.Parse("""
            version = 1
            [skirmish]
            fastest_win_seconds = -5
            biggest_army = "lots"
            [skirmish_factions.Authority]
            wins = 99999999999
            losses = "x"
            [skirmish_factions.Nonsense]
            wins = 1
            [skirmish_tiers.9]
            wins = 1
            [skirmish_tiers.2]
            wins = 3
            [missions.good_1]
            completed = true
            no_losses = "yes"
            best_seconds = 61
            [missions."bad id!"]
            completed = true
            """);
        Assert.Equal(0, p.FastestWinSeconds);
        Assert.Equal(0, p.BiggestArmy);
        Assert.Equal(PlayerProfile.MaxCount, p.ByFaction[Faction.Authority].Wins);
        Assert.Equal(0, p.ByFaction[Faction.Authority].Losses);
        Assert.Single(p.ByFaction);
        Assert.Equal(new[] { 2 }, p.ByAiTier.Keys);
        Assert.Equal(new[] { "good_1" }, p.Missions.Keys);
        Assert.True(p.Missions["good_1"].Completed);
        Assert.False(p.Missions["good_1"].NoLosses);
    }

    [Fact]
    public void Unsafe_mission_ids_are_not_stored()
    {
        PlayerProfile p = PlayerProfile.Empty.WithMission("a]\nb", new MissionRecord(true, true, 1));
        Assert.Empty(p.Missions);
    }

    [Fact]
    public void Save_to_an_unwritable_place_returns_false_without_throwing()
    {
        Directory.CreateDirectory(_dir);
        string blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");
        Assert.False(PlayerProfile.Empty.Save(Path.Combine(blocker, "profile.toml")));
    }

    [Fact]
    public void Recorder_saves_the_win_and_reports_new_medals()
    {
        IReadOnlyList<string> first = ProfileRecorder.RecordMissionWin(File1, "auth_01", 20 * 60, 2);
        IReadOnlyList<string> second = ProfileRecorder.RecordMissionWin(File1, "auth_01", 20 * 50, 0);
        IReadOnlyList<string> third = ProfileRecorder.RecordMissionWin(File1, "auth_01", 20 * 70, 0);
        Assert.Equal(new[] { "Completed" }, first);
        Assert.Equal(new[] { "No losses" }, second);
        Assert.Empty(third);
        Assert.Equal(50, PlayerProfile.Load(File1).Missions["auth_01"].BestSeconds);
    }

    [Fact]
    public void Recorder_counts_a_skirmish()
    {
        ProfileRecorder.RecordSkirmish(File1, Faction.LeftBehind, 3, true, 20 * 400, 1234);
        PlayerProfile p = PlayerProfile.Load(File1);
        Assert.Equal(new SkirmishTally(1, 0), p.ByFaction[Faction.LeftBehind]);
        Assert.Equal(400, p.FastestWinSeconds);
        Assert.Equal(1234, p.BiggestArmy);
    }

    [Fact]
    public void Page_lines_show_medals_and_records()
    {
        IReadOnlyList<string> empty = ProfileModel.Lines(PlayerProfile.Empty);
        Assert.Contains(empty, l => l.Contains("None yet"));
        PlayerProfile p = PlayerProfile.Empty.WithMission("auth_01", MedalRules.ForWin(20 * 65, 0)).WithSkirmish(Faction.Authority, 2, true, 125, 800);
        IReadOnlyList<string> lines = ProfileModel.Lines(p);
        Assert.Contains("  auth_01: Completed, No losses   best 1:05", lines);
        Assert.Contains("  Fastest win: 2:05", lines);
        Assert.Contains("    Authority: 1 / 0", lines);
        Assert.Contains("    Tier 2: 1 / 0", lines);
    }
}
