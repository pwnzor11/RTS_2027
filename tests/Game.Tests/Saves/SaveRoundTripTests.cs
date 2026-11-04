// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Host;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Saves;

public sealed class SaveRoundTripTests
{
    private const int Before = 60;
    private const int After = 120;

    [Fact]
    public void Skirmish_save_then_load_gives_the_same_state_hash()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = t.NewSkirmish();
        SaveTestSupport.GiveOrders(host);
        host.StepTicks(Before);
        ulong saved = host.Sim.ComputeStateHash();

        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(Rts.Game.Screens.SkirmishSettings.Default), host, t.RulesDir, "slot");
        LoadedGame loaded = GameLoader.Load(path, t.RulesDir);

        Assert.Equal(saved, loaded.Sim.ComputeStateHash());
        Assert.Equal(Before, loaded.Sim.Tick);
        Assert.Equal(SaveMode.Skirmish, loaded.Header.Mode);
        Assert.NotNull(loaded.Ai);
        Assert.False(string.IsNullOrEmpty(loaded.Note));
    }

    [Fact]
    public void Skirmish_continues_deterministically_after_a_load()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = t.NewSkirmish();
        SaveTestSupport.GiveOrders(host);
        host.StepTicks(Before);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(Rts.Game.Screens.SkirmishSettings.Default), host, t.RulesDir, "slot");

        host.StepTicks(After);                       // the game that never saved
        ulong straight = host.Sim.ComputeStateHash();

        var resumed = new SimSnapshotSource(GameLoader.Load(path, t.RulesDir));
        resumed.Host.Ai = null;                      // the AI restarts fresh by design, so compare the sim alone
        resumed.StepTicks(After);                    // the game that loaded

        Assert.Equal(host.Tick, resumed.Tick);
        Assert.Equal(straight, resumed.Sim.ComputeStateHash());
    }

    [Fact]
    public void Commands_queued_at_save_time_survive_the_load()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = t.NewSkirmish();
        host.StepTicks(10);
        SaveTestSupport.GiveOrders(host);            // queued for the next tick, not yet run
        Assert.NotEmpty(host.Pending);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(Rts.Game.Screens.SkirmishSettings.Default), host, t.RulesDir, "slot");

        host.StepTicks(After);
        var resumed = new SimSnapshotSource(GameLoader.Load(path, t.RulesDir));
        resumed.Host.Ai = null;
        Assert.NotEmpty(resumed.Host.Pending);
        resumed.StepTicks(After);

        Assert.Equal(host.Sim.ComputeStateHash(), resumed.Sim.ComputeStateHash());
    }

    [Fact]
    public void A_loaded_skirmish_with_its_fresh_AI_runs_on()
    {
        using var t = new SaveTestSupport();
        SkirmishGame game = SkirmishGame.Create(t.RulesDir, 0, 2);
        var host = new SimHost(game.Sim) { Ai = game.Ai };
        host.StepTicks(80);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(Rts.Game.Screens.SkirmishSettings.Default), host, t.RulesDir, "slot");

        var resumed = new SimSnapshotSource(GameLoader.Load(path, t.RulesDir));
        Assert.NotNull(resumed.Host.Ai);
        resumed.StepTicks(200);
        Assert.Equal(280, resumed.Tick);
    }

    [Fact]
    public void A_generated_map_skirmish_round_trips()
    {
        using var t = new SaveTestSupport();
        var settings = new Rts.Game.Screens.SkirmishSettings(77, 2, 0);
        SkirmishGame game = SkirmishGame.Create(t.RulesDir, settings.MapSeed, settings.AiTier);
        var host = new SimHost(game.Sim);
        host.StepTicks(30);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(settings), host, t.RulesDir, "gen");
        LoadedGame loaded = GameLoader.Load(path, t.RulesDir);
        Assert.Equal(host.Sim.ComputeStateHash(), loaded.Sim.ComputeStateHash());
    }

    [Fact]
    public void Mission_save_restores_script_state_and_continues_deterministically()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "auth_01");
        var host = new SimHost(game.Sim);   // no AI: a pure function of sim and script
        host.StepTicks(Before);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(), host, t.RulesDir, "mission");
        ulong saved = host.Sim.ComputeStateHash();

        LoadedGame loaded = GameLoader.Load(path, t.RulesDir);
        Assert.Equal(SaveMode.Mission, loaded.Header.Mode);
        Assert.NotNull(loaded.Host);
        Assert.Equal(saved, loaded.Sim.ComputeStateHash());

        host.StepTicks(After);
        var resumed = new SimSnapshotSource(loaded);
        resumed.Host.Ai = null;
        resumed.StepTicks(After);
        Assert.Equal(host.Sim.ComputeStateHash(), resumed.Sim.ComputeStateHash());
        Assert.Equal(game.Host.Outcome, loaded.Host!.Outcome);
    }

    [Fact]
    public void Saving_again_after_a_load_works()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = t.NewSkirmish();
        host.StepTicks(20);
        string first = GameSaver.Save(t.Catalog, game.SaveTemplate(Rts.Game.Screens.SkirmishSettings.Default), host, t.RulesDir, "one");
        var resumed = new SimSnapshotSource(GameLoader.Load(first, t.RulesDir));
        resumed.StepTicks(20);
        string second = GameSaver.Save(t.Catalog, resumed.SaveTemplate!, resumed.Host, t.RulesDir, "two");
        LoadedGame again = GameLoader.Load(second, t.RulesDir);
        Assert.Equal(40, again.Sim.Tick);
        Assert.Equal(resumed.Sim.ComputeStateHash(), again.Sim.ComputeStateHash());
    }
}
