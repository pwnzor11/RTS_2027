// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M3;

/// <summary>M3-08: the setup screen's faction picks reach <c>SimSetup.Factions</c>, and the default is the M2 setup.</summary>
public sealed class SkirmishFactionTests
{
    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/rules not found");

    [Fact]
    public void Default_settings_are_Authority_on_both_sides_and_map_to_no_factions_list()
    {
        SkirmishSettings d = SkirmishSettings.Default;
        Assert.Equal(Faction.Authority, d.PlayerFaction);
        Assert.Equal(Faction.Authority, d.AiFaction);
        Assert.Null(d.Factions);
    }

    [Fact]
    public void A_three_argument_settings_value_keeps_working_and_is_Authority()
    {
        var s = new SkirmishSettings(5, 3, 2);
        Assert.Null(s.Factions);
    }

    [Theory]
    [InlineData(Faction.Directorate, Faction.Authority)]
    [InlineData(Faction.Authority, Faction.Directorate)]
    [InlineData(Faction.Directorate, Faction.Directorate)]
    public void Picks_map_to_one_faction_per_slot_player_first(Faction player, Faction ai)
    {
        var s = new SkirmishSettings(0, 2, 0, player, ai);
        IReadOnlyList<Faction>? f = s.Factions;
        Assert.NotNull(f);
        Assert.Equal(new[] { player, ai }, f);
    }

    [Fact]
    public void Mapped_factions_are_what_SimSetup_reports_for_each_slot()
    {
        RuleSet rules = RulesLoader.Load(RulesDir);
        var map = MapFile.Load(Path.Combine(RulesDir, "..", "maps", SkirmishGame.CuratedMapFile));
        var s = new SkirmishSettings(0, 2, 0, Faction.Directorate, Faction.Authority);
        var setup = new SimSetup(1, map, 2, rules.Sim, rules.RulesHash) { Factions = s.Factions };
        Assert.Equal(Faction.Directorate, setup.FactionOf(SkirmishGame.LocalPlayer));
        Assert.Equal(Faction.Authority, setup.FactionOf(SkirmishGame.AiPlayer));
    }

    [Fact]
    public void Skirmish_create_passes_the_factions_into_the_simulation_setup()
    {
        SkirmishGame g = SkirmishGame.Create(RulesDir, 0, 1, new[] { Faction.Authority, Faction.Authority });
        Assert.Equal(new[] { Faction.Authority, Faction.Authority }, g.Sim.Setup.Factions);
        Assert.Equal(Faction.Authority, g.Sim.HudFor(0).Faction);
    }

    [Fact]
    public void Skirmish_create_without_factions_is_the_M2_setup()
    {
        SkirmishGame g = SkirmishGame.Create(RulesDir, 0, 1);
        Assert.Null(g.Sim.Setup.Factions);
    }

    [Fact]
    public void A_Directorate_skirmish_starts_with_that_faction_and_the_dir_shaft_placed()
    {
        SkirmishGame g = SkirmishGame.Create(RulesDir, 0, 1, new[] { Faction.Directorate, Faction.Authority });
        Assert.Equal(Faction.Directorate, g.Sim.HudFor(0).Faction);
        int shaft = SimRules.IndexOf(g.Sim.Setup.Rules.Buildings, "dir_shaft", b => b.Id);
        Assert.True(shaft >= 0, "dir_shaft is not in the rules");
        var buildings = g.Sim.Buildings;
        bool placed = false;
        for (int i = 1; i < buildings.Capacity; i++)
            placed |= buildings.Slots.IsIndexAlive(i) && buildings.Player[i] == 0 && buildings.TypeIndex[i] == shaft;
        Assert.True(placed, "the Directorate start kit did not place dir_shaft for player 0");
    }

    [Theory]
    [InlineData(Faction.Authority, 1, Faction.Directorate)]
    [InlineData(Faction.Directorate, 1, Faction.LeftBehind)]
    [InlineData(Faction.LeftBehind, 1, Faction.Expedition)]   // M7-14: the Overseas Expedition follows Left Behind
    [InlineData(Faction.Expedition, 1, Faction.Authority)]
    [InlineData(Faction.Authority, -1, Faction.Expedition)]
    [InlineData(Faction.Directorate, -1, Faction.Authority)]
    [InlineData(Faction.Expedition, -1, Faction.LeftBehind)]
    public void The_faction_pick_wraps_round(Faction from, int delta, Faction expected) =>
        Assert.Equal(expected, SkirmishSetupScreen.NextFaction(from, delta));

    [Fact]
    public void Faction_names_and_error_text_helpers()
    {
        Assert.Equal("Directorate", SkirmishSetupScreen.FactionName(Faction.Directorate));
        Assert.Equal("Authority", SkirmishSetupScreen.FactionName(Faction.Authority));
        Assert.Equal("short", SkirmishSetupScreen.Shorten("short", 10));
        Assert.Equal("abcdefg...", SkirmishSetupScreen.Shorten("abcdefghijkl", 10));
    }
}
