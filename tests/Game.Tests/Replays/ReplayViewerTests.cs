// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Game.Tests.Saves;
using Rts.Sim.Replay;

namespace Rts.Game.Tests.Replays;

public sealed class ReplayViewerTests
{
    private const int Ticks = 160;

    private static string Record(SaveTestSupport t, Rts.Sim.Simulation sim, Action<SimHost> play, Rts.Sim.Replay.ReplayMission? mission = null)
    {
        string path = Path.Combine(t.Folder, "r.rpl");
        var rec = new ReplayRecording(sim, RulesBundle.ReadDirectory(t.RulesDir), path, Path.Combine(t.Folder, "prev.rpl"), mission);
        var host = new SimHost(sim, rec);
        play(host);
        Assert.True(rec.Finish());
        return path;
    }

    [Fact]
    public void A_skirmish_replay_plays_to_the_recorded_final_hash_with_no_desync()
    {
        using var t = new SaveTestSupport();
        var game = SkirmishGame.Create(t.RulesDir, 0, 2);
        string path = Record(t, game.Sim, h => { SaveTestSupport.GiveOrders(h); h.StepTicks(Ticks); });
        ulong finalHash = game.Sim.ComputeStateHash();

        ReplayPlayback play = ReplayPlaybackLoader.Open(path, t.RulesDir);
        Assert.Equal(Ticks, play.Clock.FinalTick);
        play.Clock.SetSpeed(8);
        for (int i = 0; i < 1000 && !play.Clock.Ended; i++) play.Advance(SimHost.TickMicroseconds);

        Assert.True(play.Clock.Ended);
        Assert.Equal(Ticks, play.Sim.Tick);
        Assert.Equal(finalHash, play.Sim.ComputeStateHash());
        Assert.Equal(-1, play.DesyncTick);
    }

    [Fact]
    public void Stepping_and_scrubbing_reach_the_same_state_as_running()
    {
        using var t = new SaveTestSupport();
        var game = SkirmishGame.Create(t.RulesDir, 0, 2);
        string path = Record(t, game.Sim, h => { SaveTestSupport.GiveOrders(h); h.StepTicks(Ticks); });

        ReplayPlayback stepped = ReplayPlaybackLoader.Open(path, t.RulesDir);
        stepped.Clock.Paused = true;
        for (int i = 0; i < 25; i++) stepped.StepOne();
        Assert.Equal(25, stepped.Sim.Tick);
        Assert.Equal(0, stepped.Advance(100 * SimHost.TickMicroseconds));   // paused: the clock does nothing
        stepped.ScrubForward();                                             // 10 s = 200 ticks, but only 135 are left
        Assert.True(stepped.Clock.Ended);
        Assert.Equal(game.Sim.ComputeStateHash(), stepped.Sim.ComputeStateHash());
        Assert.Equal(-1, stepped.DesyncTick);
    }

    [Fact]
    public void A_generated_map_replay_finds_its_map_by_hash()
    {
        using var t = new SaveTestSupport();
        var game = SkirmishGame.Create(t.RulesDir, 99, 2);
        string path = Record(t, game.Sim, h => h.StepTicks(40));
        ReplayPlayback play = ReplayPlaybackLoader.Open(path, t.RulesDir);
        play.ScrubForward();
        Assert.Equal(game.Sim.ComputeStateHash(), play.Sim.ComputeStateHash());
    }

    [Fact]
    public void A_mission_replay_reruns_the_script()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "auth_01");
        string path = Record(t, game.Sim, h => h.StepTicks(Ticks), game.ReplayRef);

        ReplayPlayback play = ReplayPlaybackLoader.Open(path, t.RulesDir);
        Assert.NotNull(play.Host);
        play.ScrubForward();
        Assert.True(play.Clock.Ended);
        Assert.Equal(game.Sim.ComputeStateHash(), play.Sim.ComputeStateHash());
        Assert.Equal(-1, play.DesyncTick);
    }

    [Fact]
    public void A_mission_replay_with_a_changed_script_hash_is_refused_with_a_message()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "auth_01");
        string path = Record(t, game.Sim, h => h.StepTicks(20), game.ReplayRef with { ScriptHash = game.ReplayRef.ScriptHash + 1 });
        var e = Assert.Throws<SaveLoadException>(() => ReplayPlaybackLoader.Open(path, t.RulesDir));
        Assert.Contains("script", e.Message);
    }

    [Fact]
    public void A_damaged_replay_is_refused_with_a_message()
    {
        using var t = new SaveTestSupport();
        string path = Path.Combine(t.Folder, "bad.rpl");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        Assert.Throws<SaveLoadException>(() => ReplayPlaybackLoader.Open(path, t.RulesDir));
    }

    [Fact]
    public void The_viewer_shows_a_view_per_player_then_everything()
    {
        using var t = new SaveTestSupport();
        var game = SkirmishGame.Create(t.RulesDir, 0, 2);
        string path = Record(t, game.Sim, h => h.StepTicks(10));
        ReplayPlayback play = ReplayPlaybackLoader.Open(path, t.RulesDir);
        Assert.Equal(-1, play.Viewer);
        play.CycleViewer();
        Assert.Equal(0, play.Viewer);
        play.CycleViewer();
        Assert.Equal(1, play.Viewer);
        play.CycleViewer();
        Assert.Equal(-1, play.Viewer);
    }
}
