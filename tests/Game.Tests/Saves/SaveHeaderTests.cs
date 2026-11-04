// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Host;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Saves;

public sealed class SaveHeaderTests
{
    private static GameSaveHeader Sample() => new()
    {
        Mode = SaveMode.Skirmish, MapId = "generated:64:77", MapName = "Gen", RulesHash = 0xABCDEF, LocalPlayer = 0, Tick = 321,
        Skirmish = new SkirmishSettings(77, 3, 2, Faction.Directorate, Faction.Authority), Build = "x",
        Ai = new[] { new SavedAi(1, "recruit", 3, Faction.Authority, 77) },
        Pending = new[] { new Command(5, 0, 2, CommandKind.Move, new EntityId(3, 1), EntityId.None, new CellCoord(4, 5), 7, 8) },
        Sequences = new ushort[] { 3, 0, 9 },
    };

    private static byte[] Write(GameSaveHeader header)
    {
        using var t = new SaveTestSupport();
        (SimHost host, _) = t.NewSkirmish();
        var ms = new MemoryStream();
        GameSaveFile.Write(ms, header, host.Sim, Array.Empty<(string, string)>());
        return ms.ToArray();
    }

    [Fact]
    public void Header_round_trips_every_field()
    {
        GameSaveHeader h = Sample();
        GameSaveHeader back = GameSaveFile.ReadHeader(new MemoryStream(Write(h)));
        Assert.Equal(GameSaveHeader.CurrentVersion, back.Version);
        Assert.Equal(h.Mode, back.Mode);
        Assert.Equal(h.MapId, back.MapId);
        Assert.Equal(h.RulesHash, back.RulesHash);
        Assert.Equal(h.Tick, back.Tick);
        Assert.Equal(h.Skirmish, back.Skirmish);
        Assert.Equal(h.Ai, back.Ai);
        Assert.Equal(h.Pending, back.Pending);
        Assert.Equal(h.Sequences, back.Sequences);
    }

    [Fact]
    public void A_newer_header_version_is_refused_with_a_clear_message()
    {
        byte[] bytes = Write(Sample());
        bytes[4] = 99;   // little-endian u16 version right after the magic
        var e = Assert.Throws<SaveLoadException>(() => GameSaveFile.ReadHeader(new MemoryStream(bytes)));
        Assert.Contains("newer version", e.Message);
    }

    [Fact]
    public void A_file_that_is_not_a_save_is_refused()
    {
        var e = Assert.Throws<SaveLoadException>(() => GameSaveFile.ReadHeader(new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })));
        Assert.Contains("not a saved game", e.Message);
    }

    [Fact]
    public void A_truncated_file_is_refused_not_crashed_on()
    {
        byte[] bytes = Write(Sample());
        Assert.Throws<SaveLoadException>(() => GameSaveFile.ReadHeader(new MemoryStream(bytes[..20])));
    }

    [Fact]
    public void A_rules_mismatch_gives_a_notice_message_not_an_exception_leak()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = t.NewSkirmish();
        host.StepTicks(5);
        GameSaveHeader template = game.SaveTemplate(SkirmishSettings.Default) with { RulesHash = game.Rules.RulesHash + 1 };
        string path = GameSaver.Save(t.Catalog, template, host, t.RulesDir, "old");
        var e = Assert.Throws<SaveLoadException>(() => GameLoader.Load(path, t.RulesDir));
        Assert.Contains("different game data", e.Message);
    }

    [Fact]
    public void A_changed_mission_script_is_refused()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "auth_01");
        var host = new SimHost(game.Sim);
        host.StepTicks(5);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate() with { ScriptHash = game.Host.SourceHash + 1 }, host, t.RulesDir, "m");
        var e = Assert.Throws<SaveLoadException>(() => GameLoader.Load(path, t.RulesDir));
        Assert.Contains("script", e.Message);
    }

    [Fact]
    public void A_save_of_a_missing_mission_is_refused()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "auth_01");
        var host = new SimHost(game.Sim);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate() with { MissionId = "nope" }, host, t.RulesDir, "m");
        var e = Assert.Throws<SaveLoadException>(() => GameLoader.Load(path, t.RulesDir));
        Assert.Contains("nope", e.Message);
    }

    [Fact]
    public void The_catalog_lists_saves_by_kind_and_Newest_picks_the_latest()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = t.NewSkirmish();
        host.StepTicks(20);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, SaveSlots.Quick);
        Assert.StartsWith(t.Catalog.FolderOf(SaveKind.Skirmish), path);
        SaveEntry entry = Assert.Single(t.Catalog.List(SaveKind.Skirmish));
        Assert.True(entry.Readable);
        Assert.Equal(SaveSlots.Quick, entry.Name);
        Assert.Contains("Skirmish", entry.Summary);
        Assert.Empty(t.Catalog.List(SaveKind.Campaign));
        Assert.Equal(path, t.Catalog.Newest()!.Path);
    }

    [Fact]
    public void A_damaged_file_is_listed_as_unreadable()
    {
        using var t = new SaveTestSupport();
        string dir = t.Catalog.FolderOf(SaveKind.Skirmish);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "bad.rtss"), new byte[] { 9, 9, 9 });
        SaveEntry entry = Assert.Single(t.Catalog.List(SaveKind.Skirmish));
        Assert.False(entry.Readable);
        Assert.Null(t.Catalog.Newest());
    }

    [Fact]
    public void Slot_names_are_made_safe_and_numbered()
    {
        using var t = new SaveTestSupport();
        Assert.Equal("a_b_c", SaveSlots.Clean("a/b\\c"));
        Assert.Equal("save", SaveSlots.Clean("  "));
        Assert.Equal("save 1", SaveSlots.NextName(t.Catalog, SaveKind.Skirmish));
        (SimHost host, SkirmishGame game) = t.NewSkirmish();
        GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, "save 1");
        Assert.Equal("save 2", SaveSlots.NextName(t.Catalog, SaveKind.Skirmish));
    }

    [Fact]
    public void Saving_over_a_slot_replaces_it_and_leaves_no_temp_file()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = t.NewSkirmish();
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, "keep");
        byte[] before = File.ReadAllBytes(path);
        host.StepTicks(10);
        GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, "keep");
        Assert.NotEqual(before, File.ReadAllBytes(path));          // overwritten in place
        Assert.False(File.Exists(path + ".tmp"));                   // and no temp file left behind
    }
}
