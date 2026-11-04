// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Skirmish;

public sealed class SkirmishMapBesideReplayTests
{
    private static SkirmishSettings Generated(ulong seed) => new(
        new MapChoice(null, new MapGenParams(Biome.Mountains, MapSize.Small, 2, seed)),
        new[]
        {
            new SkirmishSlot(SlotKind.Human, Faction.Authority, 2, 0, 0),
            new SkirmishSlot(SlotKind.Ai, Faction.Authority, 2, 1, 1),
        }, seed);

    [Fact]
    public void A_generated_map_is_saved_as_replay_dot_map_dot_toml_and_loads_back_with_the_same_hash()
    {
        using var t = new SkirmishTestMaps();
        SkirmishGame g = SkirmishGame.Create(t.RulesDir, Generated(31));
        string replay = Path.Combine(t.Folder, "game.rpl");

        string? saved = g.SaveMapBeside(replay);

        Assert.Equal(replay + ".map.toml", saved);
        Assert.Equal(SkirmishGame.MapBesideReplay(replay), saved);
        Assert.True(File.Exists(saved));
        (MapData back, MapMeta meta) = MapFile.LoadWithMeta(saved!);
        Assert.Equal(MapHasher.Compute(g.Map), MapHasher.Compute(back));
        Assert.Equal(g.Sim.MapHash, MapHasher.Compute(back));
        Assert.Equal("mountains", meta.Biome);
        Assert.Equal(31UL, meta.Generator!.Seed);
        Assert.Equal(2, meta.MaxPlayers);
    }

    [Fact]
    public void A_user_map_is_saved_too_but_the_curated_map_is_not()
    {
        using var t = new SkirmishTestMaps();
        string map = t.WriteFourStartMap();
        var slots = new[]
        {
            new SkirmishSlot(SlotKind.Human, Faction.Authority, 2, 0, 0), new SkirmishSlot(SlotKind.Ai, Faction.Authority, 2, 1, 1),
        };
        SkirmishGame user = SkirmishGame.Create(t.RulesDir, new SkirmishSettings(new MapChoice(map, null), slots, 7));
        string? saved = user.SaveMapBeside(Path.Combine(t.Folder, "u.rpl"));
        Assert.NotNull(saved);
        Assert.Equal(user.Sim.MapHash, MapHasher.Compute(MapFile.Load(saved!)));

        SkirmishGame curated = SkirmishGame.Create(t.RulesDir, SkirmishSettings.Default);
        Assert.Null(curated.SaveMapBeside(Path.Combine(t.Folder, "c.rpl")));
        Assert.False(File.Exists(Path.Combine(t.Folder, "c.rpl.map.toml")));
    }

    [Fact]
    public void The_same_generated_settings_build_the_same_map()
    {
        using var t = new SkirmishTestMaps();
        ulong a = SkirmishGame.Create(t.RulesDir, Generated(5)).Sim.MapHash;
        ulong b = SkirmishGame.Create(t.RulesDir, Generated(5)).Sim.MapHash;
        Assert.Equal(a, b);
    }
}
