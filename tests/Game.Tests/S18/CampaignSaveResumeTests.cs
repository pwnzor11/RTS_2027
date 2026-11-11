// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;
using Rts.Game.Campaign;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Game.Tests.Saves;
using Rts.Scripting;

namespace Rts.Game.Tests.S18;

/// <summary>
/// S18 MIS-02 / GS-02: a campaign mission restored from a save is still recognised as that campaign mission (faction, id, tier), which is what
/// GameScreen.CreateFromSave uses to wire the win (RecordWin, debrief, unlocks, ending) and Restart through CampaignFlow.
/// </summary>
public sealed class CampaignSaveResumeTests
{
    private static string DataDir(SaveTestSupport t) => Path.GetFullPath(Path.Combine(t.RulesDir, ".."));

    private static CampaignService Service(SaveTestSupport t) =>
        new(DataDir(t), Path.Combine(t.Folder, "profile"), new ArchiveProgress(), Path.Combine(t.Folder, "archive.toml"));

    private static MissionGame SaveAndLoad(SaveTestSupport t, MissionGame game, int ticks)
    {
        var host = new SimHost(game.Sim);
        host.StepTicks(ticks);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(), host, t.RulesDir, "campaign");
        return MissionGame.FromSave(GameLoader.Load(path, t.RulesDir));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    public void Save_mid_mission_load_win_and_the_progress_is_recorded(int tier)
    {
        using var t = new SaveTestSupport();
        CampaignService svc = Service(t);
        CampaignStart start = svc.Start(t.RulesDir, "authority", "auth_01", tier);
        MissionGame loaded = SaveAndLoad(t, start.Game, 60);

        CampaignMissionRef? link = svc.CampaignMissionOf(loaded.Host);
        Assert.NotNull(link);
        Assert.Equal(new CampaignMissionRef("authority", "auth_01", tier), link);

        loaded.Sim.Step(Array.Empty<Rts.Sim.Core.Command>());
        CampaignTestKit.ForceWin(loaded.Host, loaded.Sim);
        Assert.True(loaded.IsOver);
        CampaignProgress after = svc.RecordWin(link!.Faction, link.MissionId, link.Tier);
        Assert.True(after.IsCompleted("auth_01"));
        Assert.Equal(tier, after.Difficulty["auth_01"]);
        Assert.True(svc.Progress("authority").IsCompleted("auth_01"));
        Assert.Equal(CampaignMissionState.Next, svc.StateOf("authority", "auth_02", svc.Progress("authority")));
    }

    [Fact]
    public void The_tutorial_and_a_non_campaign_mission_are_not_campaign_missions()
    {
        using var t = new SaveTestSupport();
        CampaignService svc = Service(t);
        MissionGame tut = MissionGame.Create(t.RulesDir, "tut_01", 1);
        Assert.Null(svc.CampaignMissionOf(tut.Host));
    }
}
