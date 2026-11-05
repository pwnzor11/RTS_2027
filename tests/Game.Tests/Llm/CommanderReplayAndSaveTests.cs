// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Game.Tests.Saves;
using Rts.Llm;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Llm;

/// <summary>M5-10: a game led by an LLM commander replays exactly with no provider at all, and a save brings its commanders back.</summary>
public sealed class CommanderReplayAndSaveTests
{
    private const string AllIn =
        "{\"directive\":{\"posture\":\"allin\",\"focus\":\"army\",\"target\":0,\"mix\":\"vehicles\",\"expand\":false},\"say\":\"Forward.\"}";

    private static SkirmishSettings WithCommander()
    {
        SkirmishSettings d = SkirmishSettings.Default;
        return d with { Slots = d.Slots.Select(s => s.Kind == SlotKind.Ai ? s with { Commander = true } : s).ToList() };
    }

    [Fact]
    public async Task A_commander_game_replays_to_the_same_state_and_directive_without_any_provider()
    {
        using var t = new SaveTestSupport();
        using SkirmishGame game = SkirmishGame.Create(t.RulesDir, SkirmishSettings.Default);
        var fake = new FakeChatProvider();
        fake.EnqueueText(AllIn);
        using var commander = new Commander(1, fake, CommanderPersona.Plain("Authority"), "Authority", 0, true, new CommanderChatLog());
        game.Ai.SetDirectiveSource(1, commander);

        string path = Path.Combine(t.Folder, "llm.rpl");
        var rec = new ReplayRecording(game.Sim, RulesBundle.ReadDirectory(t.RulesDir), path, Path.Combine(t.Folder, "prev.rpl"), null);
        var host = new SimHost(game.Sim, rec) { Ai = game.Ai };
        host.StepTicks(Commander.CadenceTicks + 1);
        // The request runs on the thread pool: wait for that task itself (not a wall-clock budget), so the reply is always read within
        // its own cadence window (a late poll would drop it as stale). Then a few ticks carry the directive command into the sim.
        Assert.NotNull(commander.LastCall);
        await commander.LastCall!.WaitAsync(TimeSpan.FromSeconds(60));
        for (int i = 0; i < 50 && !game.Sim.ViewFor(1).Directive.Set; i++) host.StepTicks(1);
        Directive live = game.Sim.ViewFor(1).Directive;
        Assert.True(live.Set);
        Assert.Equal(DirectiveCodec.PostureAllIn, live.Posture);
        host.StepTicks(100);
        Assert.True(rec.Finish());
        ulong finalHash = game.Sim.ComputeStateHash();
        int finalTick = game.Sim.Tick;

        ReplayPlayback play = ReplayPlaybackLoader.Open(path, t.RulesDir);   // no commander, no provider: only the recorded command
        play.Clock.SetSpeed(8);
        for (int i = 0; i < 5000 && !play.Clock.Ended; i++) play.Advance(SimHost.TickMicroseconds);
        Assert.True(play.Clock.Ended);
        Assert.Equal(finalTick, play.Sim.Tick);
        Assert.Equal(finalHash, play.Sim.ComputeStateHash());
        Assert.Equal(-1, play.DesyncTick);
        Assert.Equal(live, play.Sim.ViewFor(1).Directive);
        // The reply arrives in wall time: on a slow runner the wait loop can pass another cadence boundary and the commander asks
        // again (CI failure, session 9). The replay still needs no provider; at least one request must have been made.
        Assert.NotEmpty(fake.Requests);
    }

    [Fact]
    public void A_loaded_save_gets_its_commanders_back_and_without_app_settings_it_has_none()
    {
        using var t = new SaveTestSupport();
        SkirmishSettings settings = WithCommander();
        using SkirmishGame game = SkirmishGame.Create(t.RulesDir, settings, new Settings());   // provider none: offline commander
        var host = new SimHost(game.Sim) { Ai = game.Ai };
        host.StepTicks(40);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(settings), host, t.RulesDir, "llm");

        LoadedGame loaded = GameLoader.Load(path, t.RulesDir);
        Assert.Contains(loaded.Header.Skirmish!.Slots, s => s.Commander);
        var source = new SimSnapshotSource(loaded, new Settings());
        ICommanderChannel c = Assert.Single(source.Commanders!.Commanders);
        Assert.Equal((byte)1, c.Player);
        Assert.False(c.Online);
        source.Commanders.Dispose();

        LoadedGame again = GameLoader.Load(path, t.RulesDir);
        Assert.Null(new SimSnapshotSource(again).Commanders);
    }
}
