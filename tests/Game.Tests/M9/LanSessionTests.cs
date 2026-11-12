// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Lan;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M9;

/// <summary>M9-07 / M9-08 / M9-10: the in-game LAN wiring over the loopback network with real sims (host, two clients, a spectator).</summary>
public sealed class LanSessionTests
{
    private static Command BuildInfantry(byte player) =>
        new(0, player, 0, CommandKind.Build, EntityId.None, EntityId.None, default, (int)ProductionCategory.Infantry, 0);

    [Fact]
    public void Two_thousand_ticks_stay_in_sync_and_client_input_and_chat_reach_every_peer()
    {
        using var h = new LanSessionHarness();
        Assert.Equal(0, h.Sessions[0].LocalPlayer);
        Assert.Equal(1, h.Sessions[1].LocalPlayer);
        Assert.Equal(2, h.Sessions[2].LocalPlayer);
        Assert.True(h.Sessions[3].IsSpectator);
        Assert.Equal(1, h.Sessions[1].Source.Local);
        Assert.Equal(1, h.Sessions[1].Source.Viewer);
        Assert.Equal(-1, h.Sessions[3].Source.Viewer);
        foreach (LanSession s in h.Sessions) Assert.Null(s.Source.Host.Ai);   // the host runs every AI through the scheduler

        h.RunToTick(100);
        h.Sessions[1].Source.Host.Issue(BuildInfantry(1));   // the client's own order leaves through the Outbox
        h.Sessions[3].Source.Host.Issue(BuildInfantry(0));   // a spectator cannot order anything
        h.Sessions[1].SendChat(team: false, "hello all");
        h.RunToTick(2000);

        // the client's command came back in a bundle and ran on the same tick in every sim
        var issued = h.Stepped[1].Where(e => e.Command.Player == 1 && e.Command.Kind == CommandKind.Build).ToList();
        Assert.Single(issued);
        for (int p = 0; p < LanSessionHarness.Peers; p++)
            Assert.Contains(h.Stepped[p], e => e.Tick == issued[0].Tick && e.Command == issued[0].Command);
        Assert.Contains(h.Stepped[0], e => e.Command.Player == 3);   // the host's AI played slot 3
        Assert.DoesNotContain(h.Stepped[0], e => e.Command.Player == 0 && e.Command.Kind == CommandKind.Build);

        ulong host = h.HashOf(0);
        for (int p = 1; p < LanSessionHarness.Peers; p++) Assert.Equal(host, h.HashOf(p));
        Assert.True(h.Host.HashesCompared > 100, $"only {h.Host.HashesCompared} hashes compared");
        Assert.Equal(0, h.Host.HashMismatches);
        Assert.Null(h.Host.DesyncTick);

        foreach (LanSession s in h.Sessions)
            Assert.Contains(s.Chat, l => l.FromPeer == 1 && l.Player == 1 && l.Sender == "Ivo" && l.Text == "hello all");
    }

    [Fact]
    public void A_dropped_player_is_taken_over_by_an_ai_and_everyone_is_told()
    {
        using var h = new LanSessionHarness(seed: 7);
        h.RunToTick(80);
        int before = h.Stepped[0].Count(e => e.Command.Player == 2);
        h.Frozen[2] = true;
        h.Net.Disconnect(2, "cable pulled");
        h.RunToTick(900, 0, 1, 3);

        int dropTick = h.Stepped[0].Where(e => e.Command.Player == 2).Select(e => e.Tick).DefaultIfEmpty(int.MaxValue).Min();
        Assert.Equal(0, before);   // nobody played slot 2 before the drop (the human client issued nothing)
        Assert.True(dropTick < 900, "the takeover AI issued nothing for the dropped slot");
        foreach (int p in new[] { 0, 1, 3 })
        {
            Assert.Contains(h.Sessions[p].Chat, l => l.System && l.Text == "Jun left; an AI takes over");
            Assert.Contains(h.Stepped[p], e => e.Command.Player == 2);
        }
        Assert.Equal(h.HashOf(0), h.HashOf(1));
        Assert.Equal(h.HashOf(0), h.HashOf(3));
        Assert.Equal(0, h.Host.HashMismatches);
    }

    [Fact]
    public void Anyone_pauses_and_resume_runs_a_countdown_on_every_peer()
    {
        using var h = new LanSessionHarness(seed: 3, lossPercent: 0);
        h.RunToTick(60);
        h.Host.Host!.EndTick = null;
        h.Sessions[2].TogglePause();
        h.RunUntil(() => h.Sessions.All(s => s.Paused));
        h.RunUntil(() => h.Sessions.All(s => s.Tick == h.Host.Tick));
        int frozen = h.Host.Tick;
        for (int i = 0; i < 200; i++) h.Advance();
        Assert.All(h.Sessions, s => Assert.Equal(frozen, s.Tick));
        Assert.All(h.Sessions, s => Assert.Equal(2, s.PausedBy));
        Assert.Contains("Paused by Jun", Describe(h.Sessions[1]));

        h.Sessions[3].TogglePause();   // the spectator resumes
        h.RunUntil(() => h.Sessions[1].ResumeCountdownMs > 0);
        Assert.StartsWith("Resuming in", Describe(h.Sessions[1]));
        h.RunUntil(() => h.Sessions.All(s => !s.Paused));
        h.RunUntil(() => h.Sessions.All(s => s.Tick > frozen + 20));
    }

    [Fact]
    public void A_corrupted_client_is_reported_resynced_from_the_host_and_stays_in_sync()
    {
        using var h = new LanSessionHarness(seed: 11);
        h.RunToTick(200);
        h.Host.Host!.EndTick = null;
        h.Sessions[1].Source.Sim.Players.Cash[1] += 777;   // a desync bug on client 1
        h.RunUntil(() => h.Host.DesyncTick is not null && h.Sessions[1].DesyncTick is not null);

        // every peer that saw the notice wrote a report; the host's names the part that differs
        string report = h.Host.DesyncReportFolder!;
        Assert.True(File.Exists(Path.Combine(report, "save.rtsg")));
        Assert.True(File.Exists(Path.Combine(report, "replay.rpl")));
        Assert.Contains("first differing part: players", File.ReadAllText(Path.Combine(report, "subhashes.txt")));
        Assert.True(Directory.Exists(h.Sessions[1].DesyncReportFolder));
        Assert.Contains(LanText.ResyncFromHost, DescribeButtons(h.Host));
        Assert.Equal(new[] { LanText.DesyncClient }, DescribeLines(h.Sessions[2]));

        h.Sessions[2].RequestResume();   // nobody resumes over the host's choice
        for (int i = 0; i < 400; i++) h.Advance();
        Assert.True(h.Host.Paused);

        h.Host.ResyncFromHost();
        h.RunUntil(() => h.Sessions.Skip(1).All(s => s.ResyncCount == 1) && h.Sessions.All(s => !s.Paused));
        int resyncTick = h.Host.ResyncCount == 1 ? h.Host.Tick : -1;
        Assert.True(resyncTick > 200);
        Assert.Null(h.Sessions[1].DesyncTick);
        int mismatches = h.Host.HashMismatches;
        int compared = h.Host.HashesCompared;

        h.RunToTick(resyncTick + 1000);
        ulong host = h.HashOf(0);
        for (int p = 1; p < LanSessionHarness.Peers; p++) Assert.Equal(host, h.HashOf(p));
        Assert.Equal(mismatches, h.Host.HashMismatches);
        Assert.True(h.Host.HashesCompared > compared + 150);
        Assert.Null(h.Host.DesyncTick);
    }

    [Fact]
    public void When_the_host_leaves_a_client_continues_against_ai_from_an_ordinary_skirmish_save()
    {
        using var h = new LanSessionHarness(seed: 5, lossPercent: 0);
        h.RunToTick(150);
        LanSession client = h.Sessions[2];
        h.Host.Dispose();
        h.Frozen[0] = true;
        h.RunUntil(() => client.HostGone);
        Assert.Equal(new[] { LanText.HostLeft }, DescribeLines(client));
        Assert.Contains(LanText.ContinueAgainstAi, DescribeButtons(client));
        Assert.DoesNotContain(LanText.ContinueAgainstAi, DescribeButtons(h.Sessions[3]));   // a spectator has no side

        int tick = client.Tick;
        ulong hash = h.HashOf(2);
        var catalog = new SaveCatalog(Path.Combine(h.Folder, "saves"));
        string path = client.WriteContinueSave(catalog);
        LoadedGame loaded = GameLoader.Load(path, h.RulesDir);
        Assert.Equal(SaveMode.Skirmish, loaded.Header.Mode);
        Assert.Equal(2, loaded.Header.LocalPlayer);
        Assert.Equal(tick, loaded.Sim.Tick);
        Assert.Equal(hash, loaded.Sim.ComputeStateHash());
        Assert.Equal(new byte[] { 0, 1, 3 }, loaded.Header.Ai.Select(a => a.Slot).ToArray());
        Assert.All(loaded.Header.Ai, a => Assert.Null(a.State));   // host-run AIs start with empty memory

        var source = new SimSnapshotSource(loaded) { Local = loaded.Header.LocalPlayer };
        Assert.NotNull(source.Host.Ai);
        source.StepTicks(300);
        Assert.Equal(tick + 300, source.Tick);

        // a restart of that game is an ordinary one-human skirmish
        SkirmishSettings restart = LanSession.SinglePlayerSettings(loaded.Header.Skirmish!, 2);
        Assert.Empty(restart.Validate());
        Assert.Equal(SlotKind.Human, restart.Slots[2].Kind);
        Assert.Equal(SlotKind.Ai, restart.Slots[0].Kind);
    }

    [Fact]
    public void Chat_input_toggles_audience_and_keeps_printable_ascii()
    {
        var input = new LanChatInput();
        input.Open();
        foreach (char ch in "ggé wp\n") input.Type(ch);
        input.ToggleAudience();
        Assert.True(input.Team);
        input.Backspace();
        Assert.Equal((true, "gg w"), input.Submit()!.Value);
        Assert.False(input.Typing);
        input.Open();
        Assert.Null(input.Submit());
    }

    private static string Describe(LanSession s) => string.Join("|", DescribeLines(s));

    private static List<string> DescribeLines(LanSession s)
    {
        var lines = new List<string>();
        LanOverlay.Describe(s, lines, new List<(string, LanOverlayAction)>());
        return lines;
    }

    private static List<string> DescribeButtons(LanSession s)
    {
        var buttons = new List<(string Label, LanOverlayAction Action)>();
        LanOverlay.Describe(s, new List<string>(), buttons);
        return buttons.Select(b => b.Label).ToList();
    }
}
