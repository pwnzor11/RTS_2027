// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Host;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Game.Stats;
using Rts.Game.Tests.Saves;
using Rts.Game.Ui.Spectator;

namespace Rts.Game.Tests.S45;

/// <summary>U-09 (GAMESYS-07): the end-of-match graph samples are saved in an optional trailing block and restored on load; the sim hash is untouched.</summary>
public sealed class StatsSaveTests
{
    private static MatchTimeline MakeTimeline()
    {
        var t = new MatchTimeline(new[] { 0, 1 });
        t.Add(200, new[] { new PlayerSample(100, 50, 1, 3, 0), new PlayerSample(90, 40, 0, 2, 1) });
        t.Add(400, new[] { new PlayerSample(300, 150, 4, 5, 2), new PlayerSample(250, 140, 3, 4, 1) });
        return t;
    }

    private static void AssertSame(MatchTimeline a, MatchTimeline b)
    {
        Assert.Equal(a.Slots, b.Slots);
        Assert.Equal(a.Ticks, b.Ticks);
        for (int i = 0; i < a.Count; i++)
            for (int c = 0; c < a.Slots.Count; c++)
                foreach (GraphMetric m in Enum.GetValues<GraphMetric>())
                    Assert.Equal(a.Value(i, c, m), b.Value(i, c, m));
    }

    [Fact]
    public void SamplerSamples_SurviveSaveAndLoad()
    {
        using var t = new SaveTestSupport();
        SkirmishGame game = SkirmishGame.Create(t.RulesDir, 0, 1);
        var host = new SimHost(game.Sim) { Ai = game.Ai };
        host.StepTicks(300);
        MatchTimeline timeline = MakeTimeline();
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, "slot", null, timeline);
        ulong before = host.Sim.ComputeStateHash();

        LoadedGame loaded = GameLoader.Load(path, t.RulesDir);
        Assert.NotNull(loaded.Timeline);
        AssertSame(timeline, loaded.Timeline!);
        Assert.Equal(before, loaded.Sim.ComputeStateHash());   // the stats block is outside the sim

        var sampler = new StatsSampler();
        sampler.Restore(loaded.Timeline);
        Assert.Equal(2, sampler.Timeline!.Count);
        sampler.Observe(450, () => Array.Empty<SpectatorRow>());   // not yet due (next sample at 600)
        Assert.Equal(2, sampler.Timeline.Count);
    }

    [Fact]
    public void SaveWithoutSamples_LoadsWithNoTimeline()
    {
        using var t = new SaveTestSupport();
        SkirmishGame game = SkirmishGame.Create(t.RulesDir, 0, 1);
        var host = new SimHost(game.Sim) { Ai = game.Ai };
        host.StepTicks(50);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, "slot");
        Assert.Null(GameLoader.Load(path, t.RulesDir).Timeline);
    }

    [Fact]
    public void DamagedStatsBlock_IsRefusedWithAMessage()
    {
        using var t = new SaveTestSupport();
        SkirmishGame game = SkirmishGame.Create(t.RulesDir, 0, 1);
        var host = new SimHost(game.Sim) { Ai = game.Ai };
        host.StepTicks(50);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, "slot", null, MakeTimeline());
        byte[] bytes = File.ReadAllBytes(path);
        bytes[^20] ^= 1;
        File.WriteAllBytes(path, bytes);
        var e = Assert.Throws<SaveLoadException>(() => GameLoader.Load(path, t.RulesDir));
        Assert.Contains("damaged", e.Message);
    }

    [Fact]
    public void TruncatedAndAlteredGameSaves_AreRefused()
    {
        using var t = new SaveTestSupport();
        SkirmishGame game = SkirmishGame.Create(t.RulesDir, 0, 1);
        var host = new SimHost(game.Sim) { Ai = game.Ai };
        host.StepTicks(50);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, "slot");
        byte[] bytes = File.ReadAllBytes(path);
        string cut = path + ".cut";
        File.WriteAllBytes(cut, bytes[..^30]);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Throws<SaveLoadException>(() => GameLoader.Load(cut, t.RulesDir)).Message));
        bytes[bytes.Length / 2] ^= 1;
        File.WriteAllBytes(cut, bytes);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Throws<SaveLoadException>(() => GameLoader.Load(cut, t.RulesDir)).Message));
    }

    [Fact]
    public void ChatOverlay_ShowsLinesAtTheirTickForAWhile()
    {
        var chat = new[]
        {
            new Rts.Sim.Replay.ReplayChatLine(100, 0, "hello"),
            new Rts.Sim.Replay.ReplayChatLine(200, Rts.Sim.Replay.ReplayWriter.NoSlot, "psst"),
        };
        Assert.Empty(ReplayViewerScreen.ChatLinesAt(chat, 99));
        Assert.Equal(new[] { "Player 1: hello" }, ReplayViewerScreen.ChatLinesAt(chat, 150));
        Assert.Equal(new[] { "Player 1: hello", "Spectator: psst" }, ReplayViewerScreen.ChatLinesAt(chat, 210));
        Assert.Empty(ReplayViewerScreen.ChatLinesAt(chat, 200 + 12 * Rts.Sim.Core.SimConstants.TicksPerSecond));
    }
}
