// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Saves;

/// <summary>M3-21: the AI's own memory travels with a save (header version 3), a loaded game continues exactly like one that never saved,
/// and saves from before (header versions 1 and 2, no AI memory) still load with the AI starting fresh.</summary>
public sealed class AiSaveTests
{
    private const int Before = 900;
    private const int After = 900;

    private static (SimHost Host, SkirmishGame Game) NewGame(SaveTestSupport t, int tier)
    {
        SkirmishGame game = SkirmishGame.Create(t.RulesDir, 0, tier);
        return (new SimHost(game.Sim) { Ai = game.Ai }, game);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void An_AI_game_saved_loaded_and_continued_equals_the_game_that_never_saved(int tier)
    {
        using var t = new SaveTestSupport();
        (SimHost straight, _) = NewGame(t, tier);
        (SimHost host, SkirmishGame game) = NewGame(t, tier);
        straight.StepTicks(Before + After);

        host.StepTicks(Before);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, "slot");
        LoadedGame loaded = GameLoader.Load(path, t.RulesDir);
        Assert.Equal(GameLoader.AiResumedNote, loaded.Note);
        Assert.NotNull(Assert.Single(loaded.Header.Ai).State);

        var resumed = new SimSnapshotSource(loaded);
        Assert.NotNull(resumed.Host.Ai);
        resumed.StepTicks(After);

        Assert.Equal(straight.Tick, resumed.Tick);
        Assert.Equal(straight.Sim.ComputeStateHash(), resumed.Sim.ComputeStateHash());
        // and the AI's own bookkeeping agrees with the uninterrupted game's
        Assert.Equal(AiSummary(straight.Ai!), AiSummary(resumed.Host.Ai!));
    }

    private static string AiSummary(AiDriver driver)
    {
        var ai = (RecruitAi)driver.AiOf(1)!;
        return $"placed {ai.StructuresPlaced} queued {ai.UnitsQueued} waves {ai.Waves} scouts {ai.Scouts} tier {ai.MaxTier}/{ai.Tier2Tick}/{ai.Tier3Tick} target {ai.TargetPlayer}";
    }

    [Fact]
    public void The_loaded_AI_keeps_its_counters_where_a_fresh_one_starts_at_zero()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = NewGame(t, 2);
        host.StepTicks(Before);
        var live = (RecruitAi)host.Ai!.AiOf(1)!;
        Assert.True(live.StructuresPlaced > 0, "the AI had built something by now");
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, "slot");

        LoadedGame loaded = GameLoader.Load(path, t.RulesDir);
        var resumed = new SimSnapshotSource(loaded);
        resumed.StepTicks(1);   // starts the driver: the state is applied
        var back = (RecruitAi)loaded.Ai!.AiOf(1)!;
        Assert.True(back.StructuresPlaced >= live.StructuresPlaced, "build-order progress survived the load");
        Assert.True(back.UnitsQueued >= live.UnitsQueued);
        Assert.True(loaded.Ai.Restored);
    }

    [Fact]
    public void Saving_a_loaded_game_before_it_ticks_hands_the_AI_state_on()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = NewGame(t, 2);
        host.StepTicks(300);
        string first = GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, "one");
        LoadedGame loaded = GameLoader.Load(first, t.RulesDir);
        byte[]? before = loaded.Header.Ai[0].State;
        Assert.NotNull(before);

        var resumed = new SimSnapshotSource(loaded);
        string second = GameSaver.Save(t.Catalog, resumed.SaveTemplate!, resumed.Host, t.RulesDir, "two");
        LoadedGame again = GameLoader.Load(second, t.RulesDir);
        Assert.Equal(before, again.Header.Ai[0].State);
    }

    [Fact]
    public void A_version_2_save_without_AI_memory_still_loads_and_the_AI_starts_fresh()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = NewGame(t, 2);
        host.StepTicks(200);
        GameSaveHeader v2 = GameSaver.Capture(game.SaveTemplate(SkirmishSettings.Default), host) with
        {
            Version = 2, Ai = new[] { game.SaveTemplate(SkirmishSettings.Default).Ai[0] },
        };
        var ms = new MemoryStream();
        GameSaveFile.Write(ms, v2, host.Sim, RulesBundle.ReadDirectory(t.RulesDir));

        ms.Position = 0;
        GameSaveHeader read = GameSaveFile.ReadHeader(ms);
        Assert.Equal(2, read.Version);
        Assert.Null(read.Ai[0].State);

        LoadedGame loaded = GameLoader.Load(new MemoryStream(ms.ToArray()), t.RulesDir);
        Assert.Equal(GameLoader.AiNote, loaded.Note);
        Assert.Equal(host.Sim.ComputeStateHash(), loaded.Sim.ComputeStateHash());
        var resumed = new SimSnapshotSource(loaded);
        resumed.StepTicks(120);   // the fresh AI just plays on
        Assert.Equal(320, resumed.Tick);
        Assert.Equal(0, ((RecruitAi)loaded.Ai!.AiOf(1)!).Waves);
    }

    [Fact]
    public void A_damaged_AI_blob_is_ignored_and_the_AI_starts_fresh()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = NewGame(t, 2);
        host.StepTicks(300);
        byte[] blob = host.Ai!.CaptureState(1)!;
        Assert.True(AiSnapshot.TryUnpack(blob, out _));

        byte[] damaged = (byte[])blob.Clone();
        damaged[^3] ^= 0x5A;
        Assert.False(AiSnapshot.TryUnpack(damaged, out _));

        (SimHost fresh, _) = NewGame(t, 2);
        fresh.Ai!.RestoreState(1, damaged);
        fresh.Ai.RestoreState(1, blob[..(blob.Length / 2)]);   // truncated: also refused
        fresh.StepTicks(5);
        Assert.Equal(5, fresh.Tick);
        Assert.True(fresh.Ai.Restored);   // refused up front, never half-applied
    }

    [Fact]
    public void The_envelope_round_trips_and_refuses_foreign_bytes()
    {
        byte[] payload = { 1, 2, 3, 4, 5 };
        byte[] packed = AiSnapshot.Pack(payload);
        Assert.True(AiSnapshot.TryUnpack(packed, out byte[] back));
        Assert.Equal(payload, back);
        Assert.False(AiSnapshot.TryUnpack(null, out _));
        Assert.False(AiSnapshot.TryUnpack(Array.Empty<byte>(), out _));
        Assert.False(AiSnapshot.TryUnpack(payload, out _));
        byte[] newer = (byte[])packed.Clone();
        newer[4] = 99;   // little-endian u16 format version after the magic
        Assert.False(AiSnapshot.TryUnpack(newer, out _));
    }

    [Fact]
    public void A_header_with_AI_state_round_trips_and_the_save_version_is_8()
    {
        Assert.Equal(8, GameSaveHeader.CurrentVersion);   // M13-15: version 8 appends the per-slot options
        using var t = new SaveTestSupport();
        (SimHost host, _) = t.NewSkirmish();
        var h = new GameSaveHeader
        {
            Mode = SaveMode.Skirmish, MapId = "generated:64:77", MapName = "Gen", RulesHash = 1, Tick = 5,
            Skirmish = new SkirmishSettings(77, 3, 2, Faction.Directorate, Faction.Authority),
            Ai = new[]
            {
                new SavedAi(1, "recruit", 3, Faction.Authority, 77) { State = new byte[] { 9, 8, 7 } },
                new SavedAi(2, "recruit", 2, Faction.Authority, 77),
            },
        };
        var ms = new MemoryStream();
        GameSaveFile.Write(ms, h, host.Sim, Array.Empty<(string, string)>());
        ms.Position = 0;
        GameSaveHeader back = GameSaveFile.ReadHeader(ms);
        Assert.Equal(8, back.Version);
        Assert.Equal(new byte[] { 9, 8, 7 }, back.Ai[0].State);
        Assert.Null(back.Ai[1].State);
    }
}
