// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Qol;

/// <summary>QOL-01 (D-313): the last skirmish setup is remembered in skirmish.toml (atomic write, tolerant load) and QOL-07's rules ride with it.</summary>
public sealed class LastSkirmishTests
{
    private static string TempFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts2027-last-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "skirmish.toml");
    }

    private static SkirmishSettings Custom() => new(
        new MapChoice(null, new MapGenParams(Biome.RustBelt, MapSize.Medium, 3, 4242, null, OceanEdges.None, MapGenerator.CurrentLayout, true)),
        new[]
        {
            new SkirmishSlot(SlotKind.Human, Faction.Directorate, 2, 3, 0, false, 75, 2, true),
            new SkirmishSlot(SlotKind.Ai, Faction.LeftBehind, 4, 1, 1, true, 125, 0, false),
            new SkirmishSlot(SlotKind.Ai, Faction.Authority, 1, 2, 2),
            new SkirmishSlot(SlotKind.Closed, Faction.Authority, 2, 4, 3),
        }, 4242) { Rules = new MatchRules(1, true) };

    [Fact]
    public void A_custom_setup_survives_the_round_trip()
    {
        SkirmishSettings back = LastSkirmish.Parse(LastSkirmish.ToToml(Custom()))!;
        Assert.NotNull(back);
        Assert.Equal(Custom().Slots, back.Slots);
        Assert.Equal(new MatchRules(1, true), back.Rules);
        Assert.Equal(Custom().Map.Generated! with { Players = 2 }, back.Map.Generated);   // the player count is worked out from the open slots when the screen builds the choice
    }

    [Fact]
    public void A_map_file_choice_and_the_default_rules_round_trip()
    {
        var s = new SkirmishSettings(new MapChoice("rustbelt_m2.map.toml", null), SkirmishSettings.Default.Slots, 1);
        SkirmishSettings back = LastSkirmish.Parse(LastSkirmish.ToToml(s))!;
        Assert.Equal("rustbelt_m2.map.toml", back.Map.File);
        Assert.Equal(MatchRules.Default, back.Rules);
        Assert.Equal(s.Slots, back.Slots);
    }

    [Fact]
    public void Save_then_Load_through_a_file_leaves_no_temp_file()
    {
        string file = TempFile();
        Assert.True(LastSkirmish.Save(Custom(), file));
        Assert.False(File.Exists(file + ".tmp"));
        Assert.Equal(Custom().Slots, LastSkirmish.Load(file)!.Slots);
        Assert.True(LastSkirmish.Save(SkirmishSettings.Default, file));   // overwrites
        Assert.Equal(SkirmishSettings.Default.Slots, LastSkirmish.Load(file)!.Slots);
    }

    [Fact]
    public void Clear_forgets_the_setup()
    {
        string file = TempFile();
        LastSkirmish.Save(Custom(), file);
        Assert.True(LastSkirmish.Clear(file));
        Assert.Null(LastSkirmish.Load(file));
        Assert.True(LastSkirmish.Clear(file));   // nothing to clear is fine
    }

    [Theory]
    [InlineData("")]
    [InlineData("this is not toml = = =")]
    [InlineData("version = 99\nmap_file = \"x.map.toml\"\n[[slot]]\nkind = 1\nfaction = 0")]
    [InlineData("version = 1\n[[slot]]\nkind = 1\nfaction = 0")]   // no map
    [InlineData("version = 1\nmap_file = \"x.map.toml\"")]   // no slots
    public void A_missing_unreadable_or_incomplete_file_gives_null(string text)
    {
        Assert.Null(LastSkirmish.Parse(text));
        string file = TempFile();
        File.WriteAllText(file, text);
        Assert.Null(LastSkirmish.Load(file));
    }

    [Fact]
    public void A_setup_that_cannot_start_is_not_restored()
    {
        // two slots with the same colour
        string text = "version = 1\nmap_file = \"x.map.toml\"\n[[slot]]\nkind = 1\nfaction = 0\nai_tier = 2\ncolour = 0\nteam = 0\n"
            + "[[slot]]\nkind = 2\nfaction = 0\nai_tier = 2\ncolour = 0\nteam = 1\n";
        Assert.Null(LastSkirmish.Parse(text));
        Assert.Null(LastSkirmish.Load(Path.Combine(Path.GetTempPath(), "rts2027-none-" + Guid.NewGuid().ToString("N"), "skirmish.toml")));
    }

    [Fact]
    public void The_defaults_are_todays_game()
    {
        Assert.Equal(MatchRules.Default, SkirmishSettings.Default.Rules);
        Assert.True(MatchRules.Default.IsDefault);
        Assert.Equal(-1, MatchRules.Default.SuperweaponLimit);
        Assert.False(MatchRules.Default.RevealMap);
    }
}
