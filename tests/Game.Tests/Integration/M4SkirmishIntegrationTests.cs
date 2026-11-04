// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Game.Tests.Saves;
using Rts.Game.Tests.Skirmish;
using Rts.Sim.Map;
using Rts.Sim.Replay;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Integration;

/// <summary>M4-INT2: setup v2 settings reach a running sim through the factory the game screen uses, saves and replays of N-player and generated games.</summary>
public sealed class M4SkirmishIntegrationTests
{
    private static SkirmishSlot Slot(SlotKind kind, int colour, int team, int tier = 1, Faction faction = Faction.Authority) =>
        new(kind, faction, tier, colour, team);

    private static SkirmishSettings Generated(int players, SlotKind first, Biome biome = Biome.RustBelt, ulong seed = 5)
    {
        var slots = new List<SkirmishSlot> { Slot(first, 0, 0) };
        for (int i = 1; i < players; i++) slots.Add(Slot(SlotKind.Ai, i, i, faction: i % 2 == 1 ? Faction.Directorate : Faction.Authority));
        return new SkirmishSettings(new MapChoice(null, new MapGenParams(biome, MapSize.Small, players, seed)), slots, seed);
    }

    private static int RealPlayers(SimSnapshotSource s) => s.Sim.Setup.PlayerCount - (s.Game!.MapMeta.Features.Count > 0 ? 1 : 0);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Generated_games_for_2_3_and_4_players_start_and_run(int players)
    {
        using var t = new SaveTestSupport();
        SkirmishSettings settings = Generated(players, SlotKind.Human);
        SimSnapshotSource source = GameScreen.BuildSkirmishSource(t.RulesDir, settings, Path.Combine(t.Folder, "r.rpl"));

        Assert.Equal(players, RealPlayers(source));
        Assert.Equal((byte)0, source.Game!.Local);
        Assert.False(source.Game.IsSpectator);
        Assert.NotNull(source.SaveTemplate);
        Assert.Equal(players - 1, source.Host.Ai!.Count);

        source.StepTicks(120);
        Assert.False(source.Sim.GameOver);
        source.SaveReplay();
    }

    [Fact]
    public void A_closed_slot_is_dropped_and_teams_and_factions_follow_the_open_slots()
    {
        using var t = new SaveTestSupport();
        var slots = new[]
        {
            Slot(SlotKind.Human, 0, 0), Slot(SlotKind.Closed, 5, 0), Slot(SlotKind.Ai, 1, 0, faction: Faction.Directorate), Slot(SlotKind.Ai, 2, 1),
        };
        var settings = new SkirmishSettings(new MapChoice(null, new MapGenParams(Biome.RustBelt, MapSize.Small, 3, 9)), slots, 9);
        SimSnapshotSource source = GameScreen.BuildSkirmishSource(t.RulesDir, settings, Path.Combine(t.Folder, "r.rpl"));

        Assert.Equal(3, RealPlayers(source));
        Assert.Equal(source.Sim.Players.Team[0], source.Sim.Players.Team[1]);
        Assert.NotEqual(source.Sim.Players.Team[0], source.Sim.Players.Team[2]);
        Assert.Equal(Faction.Directorate, source.Sim.Players.Faction[1]);
        source.StepTicks(60);
        source.SaveReplay();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void No_human_slot_starts_a_spectator_game_with_an_AI_per_player(int players)
    {
        using var t = new SaveTestSupport();
        SkirmishSettings settings = Generated(players, SlotKind.Ai);
        Assert.True(settings.IsSpectator);
        SimSnapshotSource source = GameScreen.BuildSkirmishSource(t.RulesDir, settings, Path.Combine(t.Folder, "r.rpl"));

        Assert.True(source.Game!.IsSpectator);
        Assert.Null(source.Game.Local);
        Assert.Equal(players, source.Host.Ai!.Count);
        Assert.Null(source.SaveTemplate);   // a spectator game is not saved
        source.StepTicks(120);
        Assert.False(source.Sim.GameOver);
        source.SaveReplay();
    }

    [Fact]
    public void The_curated_map_with_default_settings_still_builds_the_old_two_player_game()
    {
        using var t = new SaveTestSupport();
        SimSnapshotSource source = GameScreen.BuildSkirmishSource(t.RulesDir, SkirmishSettings.Default, Path.Combine(t.Folder, "r.rpl"));
        SkirmishGame legacy = SkirmishGame.Create(t.RulesDir, 0, 2);
        Assert.Equal(legacy.Sim.ComputeStateHash(), source.Sim.ComputeStateHash());
        source.SaveReplay();
    }

    [Theory]
    [InlineData(Biome.RustBelt, 2)]
    [InlineData(Biome.GlassCoast, 3)]
    [InlineData(Biome.Mountains, 4)]
    [InlineData(Biome.AshPlains, 4)]
    public void A_generated_map_picks_its_biome_theater(Biome biome, int players)
    {
        var gen = new MapGenParams(biome, MapSize.Small, players, 3);
        (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(gen);
        string expected = TerrainPalette.NormalizeTheaterId(MapFile.BiomeKeys[(int)biome]);

        Assert.Equal(players, map.StartPositions.Count);
        // The renderers call ForMap(map) with no metadata, so the header alone has to name the theater.
        Assert.Equal(expected, TerrainPalette.NormalizeTheaterId(TerrainPalette.TheaterIdFor(map, null)));
        Assert.Equal(expected, TerrainPalette.NormalizeTheaterId(TerrainPalette.TheaterIdFor(map, meta)));
        Assert.Same(TerrainPalette.ForMap(map), TerrainPalette.ForMap(map, meta));
        if (biome != Biome.RustBelt) Assert.NotSame(TerrainPalette.ForMap(map), TerrainPalette.Load(TerrainPalette.RustBeltTheater));
        TerrainBake.Result baked = TerrainBake.Bake(map, meta);
        Assert.True(baked.Width > 0);
    }

    [Theory]
    [InlineData(4, SlotKind.Human)]
    [InlineData(3, SlotKind.Human)]
    public void Save_then_load_of_a_generated_multi_player_game_gives_the_same_hash(int players, SlotKind first)
    {
        using var t = new SaveTestSupport();
        SimSnapshotSource source = GameScreen.BuildSkirmishSource(t.RulesDir, Generated(players, first, Biome.Mountains, 11), Path.Combine(t.Folder, "r.rpl"));
        source.StepTicks(80);
        ulong saved = source.Sim.ComputeStateHash();

        string path = GameSaver.Save(t.Catalog, source.SaveTemplate!, source.Host, t.RulesDir, "slot");
        LoadedGame loaded = GameLoader.Load(path, t.RulesDir);

        Assert.StartsWith("gen:mountains:", loaded.Header.MapId, StringComparison.Ordinal);
        Assert.Equal(source.Sim.Setup.PlayerCount, loaded.Sim.Setup.PlayerCount);
        Assert.Equal(saved, loaded.Sim.ComputeStateHash());
        Assert.Equal(players - 1, loaded.Ai!.Count);

        // And it carries on the same way: the AI restarts fresh, so compare the sim alone.
        source.Host.Ai = null;
        source.StepTicks(60);
        var resumed = new SimSnapshotSource(loaded);
        resumed.Host.Ai = null;
        resumed.StepTicks(60);
        Assert.Equal(source.Sim.ComputeStateHash(), resumed.Sim.ComputeStateHash());
        source.SaveReplay();
    }

    [Fact]
    public void Save_then_load_of_a_user_map_game_gives_the_same_hash()
    {
        using var maps = new SkirmishTestMaps();
        using var t = new SaveTestSupport();
        string map = maps.WriteFourStartMap();
        var slots = new[] { Slot(SlotKind.Human, 0, 0), Slot(SlotKind.Ai, 1, 1), Slot(SlotKind.Ai, 2, 2) };
        var settings = new SkirmishSettings(new MapChoice(map, null), slots, 21);
        SimSnapshotSource source = GameScreen.BuildSkirmishSource(t.RulesDir, settings, Path.Combine(t.Folder, "r.rpl"));
        source.StepTicks(50);

        string path = GameSaver.Save(t.Catalog, source.SaveTemplate!, source.Host, t.RulesDir, "slot");
        LoadedGame loaded = GameLoader.Load(path, t.RulesDir);

        Assert.StartsWith("user:", loaded.Header.MapId, StringComparison.Ordinal);
        Assert.Equal(source.Sim.ComputeStateHash(), loaded.Sim.ComputeStateHash());
        Assert.Equal(3, loaded.Sim.Setup.PlayerCount);
        source.SaveReplay();
    }

    [Fact]
    public void A_replay_of_a_generated_map_loads_its_beside_map_and_verifies()
    {
        using var t = new SaveTestSupport();
        string replay = Path.Combine(t.Folder, "gen.rpl");
        SimSnapshotSource source = GameScreen.BuildSkirmishSource(t.RulesDir, Generated(3, SlotKind.Human, Biome.GlassCoast, 17), replay);
        source.StepTicks(100);
        ulong finalHash = source.Sim.ComputeStateHash();
        Assert.True(source.SaveReplay());

        string beside = SkirmishGame.MapBesideReplay(replay);
        Assert.True(File.Exists(beside));

        ReplayPlayback play = ReplayPlaybackLoader.Open(replay, t.RulesDir);
        play.Clock.SetSpeed(8);
        for (int i = 0; i < 2000 && !play.Clock.Ended; i++) play.Advance(Rts.Game.Host.SimHost.TickMicroseconds);
        Assert.True(play.Clock.Ended);
        Assert.Equal(finalHash, play.Sim.ComputeStateHash());
        Assert.Equal(-1, play.DesyncTick);

        // Without the map beside it the replay cannot be played (it is not in data/maps and is not a legacy synthetic map).
        File.Delete(beside);
        Assert.Throws<SaveLoadException>(() => ReplayPlaybackLoader.Open(replay, t.RulesDir));
    }

    [Fact]
    public void A_replay_of_the_curated_map_writes_no_beside_map()
    {
        using var t = new SaveTestSupport();
        string replay = Path.Combine(t.Folder, "cur.rpl");
        SimSnapshotSource source = GameScreen.BuildSkirmishSource(t.RulesDir, SkirmishSettings.Default, replay);
        source.StepTicks(40);
        Assert.True(source.SaveReplay());
        Assert.False(File.Exists(SkirmishGame.MapBesideReplay(replay)));
        Assert.Equal(source.Sim.MapHash, ReplayPlaybackLoader.Open(replay, t.RulesDir).Sim.MapHash);
    }

    [Fact]
    public void The_map_editor_entry_is_on_and_play_this_map_preselects_the_map()
    {
        Assert.True(MainMenuModel.Build(false).Single(e => e.Action == MenuAction.MapEditor).Enabled);
        using var maps = new SkirmishTestMaps();
        string path = maps.WriteFourStartMap();

        // A map in the user folder is already listed: it is selected.
        IReadOnlyList<MapEntry> listed = MapPicker.List(maps.RulesDir, maps.Folder);
        IReadOnlyList<MapEntry> same = SkirmishSetupScreen.PreselectMap(maps.RulesDir, listed, new MapChoice(path, null), out int at);
        Assert.Equal(Path.GetFullPath(path), same[at].File);
        Assert.True(same[at].User);

        // A map outside it is added, before the Generated entry, and selected.
        IReadOnlyList<MapEntry> bare = MapPicker.List(maps.RulesDir, Path.Combine(maps.Folder, "none"));
        IReadOnlyList<MapEntry> added = SkirmishSetupScreen.PreselectMap(maps.RulesDir, bare, new MapChoice(path, null), out int at2);
        Assert.Equal(bare.Count + 1, added.Count);
        Assert.Equal(path, added[at2].File);
        Assert.True(added[^1].IsGenerated);

        // Generator parameters select the Generated entry.
        _ = SkirmishSetupScreen.PreselectMap(maps.RulesDir, bare, new MapChoice(null, new MapGenParams(Biome.AshPlains, MapSize.Small, 2, 4)), out int at3);
        Assert.True(bare[at3].IsGenerated);
    }
}
