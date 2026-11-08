// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Skirmish;

public sealed class SkirmishSetupTests
{
    private static SkirmishSlot Human(int colour = 0, int team = 0, Faction f = Faction.Authority) => new(SlotKind.Human, f, 2, colour, team);
    private static SkirmishSlot Ai(int colour, int team, int tier = 2, Faction f = Faction.Authority) => new(SlotKind.Ai, f, tier, colour, team);
    private static SkirmishSlot Closed() => new(SlotKind.Closed, Faction.Authority, 2, 7, 0);

    private static IEnumerable<CellCoord> BuildingCells(Simulation sim, byte player)
    {
        for (int i = 0; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player) yield return sim.Buildings.Cell[i];
    }

    private static SkirmishSettings OnFile(string file, params SkirmishSlot[] slots) => new(new MapChoice(file, null), slots, 4242);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Settings_build_a_game_for_two_three_and_four_players(int players)
    {
        using var t = new SkirmishTestMaps();
        string map = t.WriteFourStartMap();
        var slots = new List<SkirmishSlot> { Human(0, 0) };
        for (int i = 1; i < players; i++) slots.Add(Ai(i, i));
        SkirmishGame g = SkirmishGame.Create(t.RulesDir, OnFile(map, slots.ToArray()));

        Assert.Equal(players, g.Sim.Setup.PlayerCount);
        Assert.Equal(players - 1, g.Ai.Count);
        Assert.Equal((byte)0, g.Local);
        Assert.False(g.IsSpectator);
        Assert.False(g.Sim.Setup.AllowDebug);
        Assert.Equal(4242UL, g.Sim.Setup.Seed);
        for (byte p = 0; p < players; p++)
        {
            PlayerHud hud = g.Sim.HudFor(p);
            Assert.True(hud.Cash > 0);
            Assert.NotEmpty(hud.Options);
            Assert.Single(BuildingCells(g.Sim, p));
        }
    }

    [Fact]
    public void Each_player_gets_its_own_start_and_a_closed_slot_gets_none()
    {
        using var t = new SkirmishTestMaps();
        string map = t.WriteFourStartMap();
        SkirmishGame g = SkirmishGame.Create(t.RulesDir, OnFile(map, Human(0, 0), Closed(), Ai(1, 1), Ai(2, 2)));
        Assert.Equal(3, g.Sim.Setup.PlayerCount);
        var starts = g.Map.StartPositions;
        var centres = Enumerable.Range(0, 3).Select(p => BuildingCells(g.Sim, (byte)p).Single()).ToList();
        for (int p = 0; p < 3; p++)
            Assert.True(Math.Max(Math.Abs(centres[p].X - starts[p].X), Math.Abs(centres[p].Y - starts[p].Y)) <= 3, $"player {p} is not at start {p}");
        Assert.Equal(3, centres.Distinct().Count());
    }

    [Fact]
    public void Two_players_on_a_four_start_map_take_opposite_starts()
    {
        Assert.Equal(new[] { 0, 2 }, SkirmishStart.ChooseStarts(2, 4));
        Assert.Equal(new[] { 0, 1 }, SkirmishStart.ChooseStarts(2, 2));
        Assert.Equal(new[] { 0, 1, 2 }, SkirmishStart.ChooseStarts(3, 4));
        Assert.Equal(new[] { 0, 1, 2, 3 }, SkirmishStart.ChooseStarts(4, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkirmishStart.ChooseStarts(3, 2));
    }

    [Fact]
    public void Teams_are_renumbered_by_first_appearance_and_null_when_everyone_is_alone()
    {
        Assert.Null(SkirmishSettings.Default.Teams);
        Assert.Null(OnFile("x", Human(0, 0), Ai(1, 1), Ai(2, 2)).Teams);
        Assert.Equal(new byte[] { 0, 1, 0, 1 }, OnFile("x", Human(0, 2), Ai(1, 3), Ai(2, 2), Ai(3, 3)).Teams!.ToArray());
        // A closed slot's team is ignored.
        Assert.Null(OnFile("x", Human(0, 0), new SkirmishSlot(SlotKind.Closed, Faction.Authority, 2, 5, 0), Ai(1, 3)).Teams);
    }

    [Fact]
    public void Four_players_on_a_layout_2_map_default_to_teams_12_versus_34_and_layout_1_keeps_one_team_each()
    {
        int zoned = MapGenerator.ZonedLayout, old = MapGenerator.CurrentLayout;
        Assert.Equal(new[] { 0, 0, 1, 1 }, Enumerable.Range(0, 4).Select(i => SkirmishSettings.DefaultTeam(i, 4, zoned)).ToArray());
        Assert.Equal(new[] { 0, 1, 2, 3 }, Enumerable.Range(0, 4).Select(i => SkirmishSettings.DefaultTeam(i, 4, old)).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, Enumerable.Range(0, 3).Select(i => SkirmishSettings.DefaultTeam(i, 3, zoned)).ToArray());
        var slots = Enumerable.Range(0, 4).Select(i => i == 0 ? Human(0, SkirmishSettings.DefaultTeam(i, 4, zoned)) : Ai(i, SkirmishSettings.DefaultTeam(i, 4, zoned))).ToArray();
        Assert.Equal(new byte[] { 0, 0, 1, 1 }, OnFile("x", slots).Teams!.ToArray());
    }

    [Fact]
    public void Teams_reach_the_simulation_setup()
    {
        using var t = new SkirmishTestMaps();
        string map = t.WriteFourStartMap();
        SkirmishGame g = SkirmishGame.Create(t.RulesDir, OnFile(map, Human(0, 0), Ai(1, 1), Ai(2, 0), Ai(3, 1)));
        Assert.Equal(Relation.Ally, g.Sim.RelationOf(0, 2));
        Assert.Equal(Relation.Ally, g.Sim.RelationOf(1, 3));
        Assert.Equal(Relation.Enemy, g.Sim.RelationOf(0, 1));
    }

    [Fact]
    public void Factions_are_per_player_and_null_when_all_Authority()
    {
        Assert.Null(SkirmishSettings.Default.Factions);
        SkirmishSettings s = OnFile("x", Ai(0, 0, 2, Faction.Directorate), Human(1, 1), Ai(2, 2));
        Assert.Equal(new[] { Faction.Authority, Faction.Directorate, Faction.Authority }, s.Factions);
    }

    [Fact]
    public void Spectate_has_no_local_player_and_an_AI_on_every_slot()
    {
        using var t = new SkirmishTestMaps();
        string map = t.WriteFourStartMap();
        SkirmishGame g = SkirmishGame.Create(t.RulesDir, OnFile(map, Ai(0, 0), Ai(1, 1), Ai(2, 2), Ai(3, 3)));
        Assert.Null(g.Local);
        Assert.True(g.IsSpectator);
        Assert.Equal(4, g.Ai.Count);
        var host = new SimHost(g.Sim) { Ai = g.Ai };
        host.StepTicks(300);
        Assert.Equal(300, g.Sim.Tick);
    }

    [Fact]
    public void Each_AI_slot_keeps_its_own_tier_and_faction_in_the_save_template()
    {
        using var t = new SkirmishTestMaps();
        string map = t.WriteFourStartMap();
        SkirmishSettings s = OnFile(map, Human(0, 0), Ai(1, 1, 4), Ai(2, 2, 1));
        var template = SkirmishGame.Create(t.RulesDir, s).SaveTemplate(s);
        Assert.Equal(new[] { 4, 1 }, template.Ai.Select(a => a.Tier).ToArray());
        Assert.Equal(new byte[] { 1, 2 }, template.Ai.Select(a => a.Slot).ToArray());
        Assert.Equal(s, template.Skirmish);
    }

    [Fact]
    public void More_players_than_starts_or_than_the_map_allows_is_refused()
    {
        using var t = new SkirmishTestMaps();
        string two = t.WriteFourStartMap("capped", maxPlayers: 2);
        var e = Assert.Throws<InvalidOperationException>(() =>
            SkirmishGame.Create(t.RulesDir, OnFile(two, Human(0, 0), Ai(1, 1), Ai(2, 2))));
        Assert.Contains("at most 2", e.Message);

        // The curated map has two starts.
        Assert.Throws<InvalidOperationException>(() =>
            SkirmishGame.Create(t.RulesDir, OnFile(SkirmishSettings.CuratedFile, Human(0, 0), Ai(1, 1), Ai(2, 2))));
    }

    [Fact]
    public void Invalid_settings_and_missing_maps_are_refused_with_InvalidOperationException()
    {
        using var t = new SkirmishTestMaps();
        Assert.Throws<InvalidOperationException>(() => SkirmishGame.Create(t.RulesDir, OnFile("x", Human(0, 0), Human(1, 1))));
        Assert.Throws<InvalidOperationException>(() =>
            SkirmishGame.Create(t.RulesDir, OnFile(Path.Combine(t.Folder, "nope.map.toml"), Human(0, 0), Ai(1, 1))));
    }

    [Fact]
    public void Map_picker_lists_curated_first_then_user_maps_then_generated()
    {
        using var t = new SkirmishTestMaps();
        string userDir = Path.Combine(t.Folder, "user");
        Directory.CreateDirectory(userDir);
        File.Copy(t.WriteFourStartMap("mine"), Path.Combine(userDir, "mine.map.toml"));
        File.WriteAllText(Path.Combine(userDir, "broken.map.toml"), "not a map");

        IReadOnlyList<MapEntry> list = MapPicker.List(t.RulesDir, userDir);
        Assert.Equal(SkirmishSettings.CuratedFile, list[0].File);
        Assert.True(list[^1].IsGenerated);
        MapEntry user = Assert.Single(list, e => e.User);
        Assert.Equal(4, user.MaxPlayers);
        Assert.Equal(Path.GetFullPath(Path.Combine(userDir, "mine.map.toml")), user.File);
        Assert.DoesNotContain(list, e => e.File is not null && e.File.Contains("broken"));
    }
}
