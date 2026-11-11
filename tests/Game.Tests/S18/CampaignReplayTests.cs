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

/// <summary>S18 GS-03: a campaign mission replay recorded at tier 5 stores only the mission id and script hash, so playback runs at tier 3.</summary>
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

    // S18 audit: pins current behaviour of GS-03; flip the assertion when fixed.
    [Fact]
    public void Audit_A_tier_5_campaign_replay_desyncs_on_playback()
    {
        using var t = new SaveTestSupport();
        (string path, ulong final) = RecordTier5(t);
        ReplayPlayback play = ReplayPlaybackLoader.Open(path, t.RulesDir);
        Assert.Equal(3, play.Host!.DifficultyTier);   // the replay carries no tier: playback runs at the default
        play.ScrubForward();
        Assert.NotEqual(final, play.Sim.ComputeStateHash());
        Assert.NotEqual(-1, play.DesyncTick);
    }

    [Fact(Skip = "S18 GS-03: awaiting owner decision")]
    public void A_tier_5_campaign_replay_plays_back_with_no_desync()
    {
        using var t = new SaveTestSupport();
        (string path, ulong final) = RecordTier5(t);
        ReplayPlayback play = ReplayPlaybackLoader.Open(path, t.RulesDir);
        play.ScrubForward();
        Assert.Equal(final, play.Sim.ComputeStateHash());
        Assert.Equal(-1, play.DesyncTick);
    }
}
