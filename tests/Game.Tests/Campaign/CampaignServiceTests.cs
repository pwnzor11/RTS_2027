// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Archive;
using Rts.Game.Campaign;
using Rts.Game.Host;
using Rts.Game.Saves;
using Rts.Game.Sandbox;
using Rts.Game.Tests.Saves;
using Rts.Sim;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Campaign;

/// <summary>M8-07: campaign progress, mission states, faction unlocks, the tech allow-list at start, wins and mid-mission saves.</summary>
public sealed class CampaignServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_campsvc_" + Guid.NewGuid().ToString("N"));

    public CampaignServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static string RulesDir =>
        RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");

    private static string DataDir => Path.GetFullPath(Path.Combine(RulesDir, ".."));

    private string Profile => Path.Combine(_dir, "profile");

    private string ArchivePath => Path.Combine(_dir, "archive.toml");

    private CampaignService Service(string? dataDir = null) =>
        new(dataDir ?? DataDir, Profile, new ArchiveProgress(), ArchivePath);

    private void Write(CampaignService s, string faction, CampaignProgress p) => Assert.True(p.Save(s.ProgressPath(faction)));

    private static bool Allowed(Simulation sim, string vehicleId)
    {
        int idx = sim.Setup.Rules.Vehicles.ToList().FindIndex(v => v.Id == vehicleId);
        Assert.True(idx >= 0, vehicleId + " is not a vehicle type");
        return sim.UnlockFilter == null || sim.UnlockFilter(0, ProductionCategory.Vehicle, idx);
    }

    /// <summary>A scratch data folder whose auth_01 is a schema-2 mission that unlocks auth_tank on win (the real one has no unlocks).</summary>
    private string TempDataWithUnlockingMission()
    {
        string data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(Path.Combine(data, "missions", "auth_01"));
        Directory.CreateDirectory(Path.Combine(data, "campaigns"));
        foreach (string f in Directory.GetFiles(Path.Combine(DataDir, "campaigns"))) File.Copy(f, Path.Combine(data, "campaigns", Path.GetFileName(f)));
        foreach (string f in Directory.GetFiles(Path.Combine(DataDir, "missions", "auth_01"))) File.Copy(f, Path.Combine(data, "missions", "auth_01", Path.GetFileName(f)));
        string toml = Path.Combine(data, "missions", "auth_01", "mission.toml");
        string text = File.ReadAllText(toml).Replace("[mission]\n", "[mission]\nschema = 2\n", StringComparison.Ordinal).Replace("[mission]\r\n", "[mission]\r\nschema = 2\r\n", StringComparison.Ordinal);
        File.WriteAllText(toml, text + "\n[unlocks_on_win]\ntech = [\"auth_tank\", \"auth_wall\"]\n");
        return data;
    }

    [Fact]
    public void Progress_is_empty_at_first_and_round_trips_through_the_profile_file()
    {
        CampaignService s = Service();
        Assert.Empty(s.Progress("authority").Completed);
        CampaignProgress p = CampaignProgress.New("authority").WithWin("auth_01", 3, new[] { "auth_tank" });
        Write(s, "authority", p);
        Assert.StartsWith(Profile, s.ProgressPath("authority"));
        CampaignProgress back = s.Progress("authority");
        Assert.Equal(p.ToToml(), back.ToToml());
        Assert.Empty(s.Progress("directorate").Completed);   // one file per faction
    }

    [Fact]
    public void Mission_states_are_next_then_completed_then_unlock_the_following_one()
    {
        CampaignService s = Service();
        Assert.Equal(new[] { "dir_01", "dir_02", "dir_03", "dir_04" }, s.Campaign("directorate").Missions.Select(m => m.Id));
        Assert.Equal(new[] { CampaignMissionState.Next, CampaignMissionState.Locked, CampaignMissionState.Locked, CampaignMissionState.Locked },
            s.Missions("directorate").Select(m => m.State));
        Assert.All(s.Missions("directorate"), m => Assert.Equal(Directory.Exists(Path.Combine(DataDir, "missions", m.Id)), m.Exists));   // written or placeholder
        Assert.True(s.Missions("authority").First(m => m.Id == "auth_01").Exists);

        Write(s, "directorate", CampaignProgress.New("directorate").WithWin("dir_01", 1, Array.Empty<string>()));
        Assert.Equal(new[] { CampaignMissionState.Completed, CampaignMissionState.Next, CampaignMissionState.Locked, CampaignMissionState.Locked },
            s.Missions("directorate").Select(m => m.State));
        Assert.Equal(CampaignMissionState.Locked, s.StateOf("directorate", "dir_09", CampaignProgress.New("directorate")));
    }

    [Fact]
    public void Faction_rule_authority_and_directorate_open_left_behind_after_act2_expedition_after_act3()
    {
        CampaignService s = Service();
        Assert.True(s.IsFactionOpen("authority"));
        Assert.True(s.IsFactionOpen("directorate"));
        Assert.False(s.IsFactionOpen("left_behind"));
        Assert.False(s.IsFactionOpen("expedition"));

        Write(s, "authority", CampaignProgress.New("authority").WithWin("auth_01", 1, Array.Empty<string>()));
        Assert.False(s.IsFactionOpen("left_behind"));   // act 1 is not enough

        Write(s, "directorate", CampaignProgress.New("directorate").WithWin("dir_01", 1, Array.Empty<string>()).WithWin("dir_02", 1, Array.Empty<string>()));
        Assert.True(s.IsFactionOpen("left_behind"));    // act 2 of either
        Assert.False(s.IsFactionOpen("expedition"));

        Write(s, "directorate", CampaignProgress.New("directorate").WithWin("dir_03", 1, Array.Empty<string>()));
        Assert.True(s.IsFactionOpen("expedition"));     // act 3 of either

        var fresh = new CampaignService(DataDir, Path.Combine(_dir, "other"), new ArchiveProgress(), ArchivePath);
        Write(fresh, "authority", CampaignProgress.New("authority").WithWin("auth_03", 2, Array.Empty<string>()));
        Assert.True(fresh.IsFactionOpen("expedition"));
        Assert.Contains("left_behind", fresh.Factions);
        Assert.Contains("expedition", fresh.Factions);
    }

    [Fact]
    public void A_locked_faction_or_mission_cannot_start()
    {
        CampaignService s = Service();
        Assert.Throws<InvalidOperationException>(() => s.Start(RulesDir, "left_behind", "lb_01", 1));
        Assert.Throws<InvalidOperationException>(() => s.Start(RulesDir, "authority", "auth_02", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Start(RulesDir, "authority", "auth_01", 6));
    }

    [Fact]
    public void Allow_list_is_starts_with_plus_player_tech_plus_the_progress_tech_set()
    {
        CampaignService s = Service();
        Write(s, "authority", CampaignProgress.New("authority").WithWin("x", 1, new[] { "auth_tank" }));
        CampaignStart start = s.Start(RulesDir, "authority", "auth_01", 2);
        Assert.Equal(new[] { "auth_barracks", "auth_factory", "auth_jeep", "auth_power", "auth_refinery", "auth_rifleman", "auth_tank", "auth_wall" }, start.AllowList);
        Assert.Equal(2, start.Difficulty);
        Assert.True(Allowed(start.Game.Sim, "auth_tank"));
        Assert.True(Allowed(start.Game.Sim, "auth_jeep"));

    }

    [Fact]
    public void Without_the_tech_set_a_type_the_mission_does_not_list_stays_locked()
    {
        CampaignStart start = Service().Start(RulesDir, "authority", "auth_01", 1);
        Assert.DoesNotContain("auth_tank", start.AllowList);
        Assert.False(Allowed(start.Game.Sim, "auth_tank"));
        Assert.True(Allowed(start.Game.Sim, "auth_jeep"));
    }

    [Fact]
    public void A_win_records_completion_difficulty_unlocks_and_the_archive_entry()
    {
        string data = TempDataWithUnlockingMission();
        var archive = new ArchiveProgress();
        var s = new CampaignService(data, Profile, archive, ArchivePath);
        CampaignProgress p = s.RecordWin("authority", "auth_01", 4);
        Assert.True(p.IsCompleted("auth_01"));
        Assert.Equal(4, p.Difficulty["auth_01"]);
        Assert.Equal(new[] { "auth_tank", "auth_wall" }, p.TechSet);
        Assert.Contains("auth_01", archive.Missions);

        // saved to disk: a new service (new process) sees both
        CampaignProgress disk = new CampaignService(data, Profile, null, ArchivePath).Progress("authority");
        Assert.Equal(p.ToToml(), disk.ToToml());
        Assert.Contains("auth_01", ArchiveProgress.Load(ArchivePath).Missions);

        // a replay on a lower tier keeps the best tier and adds nothing twice
        CampaignProgress again = s.RecordWin("authority", "auth_01", 2);
        Assert.Equal(4, again.Difficulty["auth_01"]);
        Assert.Single(again.Completed);
        Assert.Throws<InvalidOperationException>(() => s.RecordWin("authority", "dir_01", 1));
    }

    [Fact]
    public void Campaign_tech_never_reaches_skirmish()
    {
        CampaignService s = Service();
        Write(s, "authority", CampaignProgress.New("authority").WithWin("x", 1, new[] { "auth_tank" }));
        _ = s.Start(RulesDir, "authority", "auth_01", 1);

        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = t.NewSkirmish();
        Assert.Null(host.Sim.UnlockFilter);   // skirmish has no allow-list at all, before or after a campaign start
        Assert.Null(game.Sim.UnlockFilter);
    }

    [Fact]
    public void A_mid_mission_save_reloads_the_exact_allow_list_and_state()
    {
        CampaignService s = Service();
        Write(s, "authority", CampaignProgress.New("authority").WithWin("x", 1, new[] { "auth_tank" }));
        CampaignStart start = s.Start(RulesDir, "authority", "auth_01", 3);

        // The profile changes after the save was made (a later win adds tech, or the file is deleted): the save must not care.
        using var t = new SaveTestSupport();
        var host = new SimHost(start.Game.Sim);
        host.StepTicks(40);
        string path = GameSaver.Save(t.Catalog, start.Game.SaveTemplate(), host, t.RulesDir, "campaign");
        File.Delete(s.ProgressPath("authority"));
        Write(s, "authority", CampaignProgress.New("authority").WithWin("y", 1, new[] { "auth_wall" }));

        LoadedGame loaded = GameLoader.Load(path, t.RulesDir);
        Assert.Equal("auth_01", loaded.Header.MissionId);
        Assert.Equal(host.Sim.ComputeStateHash(), loaded.Sim.ComputeStateHash());
        Assert.NotNull(loaded.Sim.UnlockFilter);
        var rules = loaded.Sim.Setup.Rules;
        for (int i = 0; i < rules.Vehicles.Count; i++)
            Assert.Equal(host.Sim.UnlockFilter!(0, ProductionCategory.Vehicle, i), loaded.Sim.UnlockFilter!(0, ProductionCategory.Vehicle, i));
        for (int i = 0; i < (rules.Infantry?.Count ?? 0); i++)
            Assert.Equal(host.Sim.UnlockFilter!(0, ProductionCategory.Infantry, i), loaded.Sim.UnlockFilter!(0, ProductionCategory.Infantry, i));
        for (int i = 0; i < (rules.Buildings?.Count ?? 0); i++)
            Assert.Equal(host.Sim.UnlockFilter!(0, ProductionCategory.Structure, i), loaded.Sim.UnlockFilter!(0, ProductionCategory.Structure, i));
        Assert.True(Allowed(loaded.Sim, "auth_tank"));   // the tech at start, not the changed profile
    }
}
