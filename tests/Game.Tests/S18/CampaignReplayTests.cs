// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Archive;
using Rts.Game.Campaign;
using Rts.Game.Host;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Game.Tests.Saves;

namespace Rts.Game.Tests.S18;

/// <summary>S18 GS-03: a campaign mission replay recorded at tier 5 stores only the mission id and script hash, so playback runs at tier 3 and
/// drifts; by owner decision mission replays are "watchable only" and hide the desync warning, skirmish replays keep it.</summary>
public sealed class CampaignReplayTests
{
    private const int Ticks = 400;

    private static (string Path, ulong FinalHash) RecordTier5(SaveTestSupport t)
    {
        var svc = new CampaignService(Path.GetFullPath(Path.Combine(t.RulesDir, "..")), Path.Combine(t.Folder, "profile"), new ArchiveProgress(),
            Path.Combine(t.Folder, "archive.toml"));
        MissionGame game = svc.Start(t.RulesDir, "authority", "auth_01", 5).Game;
        string path = Path.Combine(t.Folder, "tier5.rpl");
        var rec = new ReplayRecording(game.Sim, RulesBundle.ReadDirectory(t.RulesDir), path, Path.Combine(t.Folder, "prev.rpl"), game.ReplayRef);
        new SimHost(game.Sim, rec).StepTicks(Ticks);
        Assert.True(rec.Finish());
        return (path, game.Sim.ComputeStateHash());
    }

    // S18 GS-03 (owner decision, "watchable only"): the replay still drifts (it carries no tier or campaign tech), but the viewer shows the
    // watchable-only note instead of the desync warning.
    [Fact]
    public void A_tier_5_campaign_replay_drifts_but_is_watchable_only()
    {
        using var t = new SaveTestSupport();
        (string path, ulong final) = RecordTier5(t);
        ReplayPlayback play = ReplayPlaybackLoader.Open(path, t.RulesDir);
        Assert.Equal(3, play.Host!.DifficultyTier);   // the replay carries no tier: playback runs at the default
        play.ScrubForward();
        Assert.NotEqual(final, play.Sim.ComputeStateHash());
        Assert.NotEqual(-1, play.DesyncTick);
        Assert.True(play.IsMissionReplay);
        Assert.False(play.ShowDesyncWarning);
        Assert.Equal("Mission replay: watchable only, it may drift from the original game", Rts.Scripting.MissionReplay.WatchableOnlyNote);
    }

    [Fact]
    public void A_skirmish_replay_that_desyncs_still_shows_the_warning()
    {
        using var t = new SaveTestSupport();
        SkirmishGame game = SkirmishGame.Create(t.RulesDir, 0, 2);
        string path = Path.Combine(t.Folder, "skirmish.rpl");
        var rec = new ReplayRecording(game.Sim, RulesBundle.ReadDirectory(t.RulesDir), path, Path.Combine(t.Folder, "prev.rpl"));
        new SimHost(game.Sim, rec).StepTicks(Ticks);
        Assert.True(rec.Finish());

        ReplayPlayback play = ReplayPlaybackLoader.Open(path, t.RulesDir);
        Assert.False(play.IsMissionReplay);
        play.Sim.Players.Cash[0] += 1;   // force a desync: the playback no longer matches the recorded hashes
        play.ScrubForward();
        Assert.NotEqual(-1, play.DesyncTick);
        Assert.True(play.ShowDesyncWarning);
    }
}
