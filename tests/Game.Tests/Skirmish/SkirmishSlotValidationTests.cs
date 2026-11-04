// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Skirmish;

public sealed class SkirmishSlotValidationTests
{
    private static SkirmishSlot Human(int colour = 0, int team = 0) => new(SlotKind.Human, Faction.Authority, 2, colour, team);
    private static SkirmishSlot Ai(int colour, int team, int tier = 2) => new(SlotKind.Ai, Faction.Authority, tier, colour, team);
    private static SkirmishSlot Closed() => new(SlotKind.Closed, Faction.Authority, 2, 7, 0);
    private static SkirmishSettings Of(params SkirmishSlot[] slots) =>
        new(new MapChoice(SkirmishSettings.CuratedFile, null), slots, 1);

    [Fact]
    public void The_default_settings_are_valid()
    {
        Assert.Empty(SkirmishSettings.Default.Validate());
        Assert.Equal(SlotKind.Human, SkirmishSettings.Default.Slots[0].Kind);
        Assert.Equal(SlotKind.Ai, SkirmishSettings.Default.Slots[1].Kind);
    }

    [Fact]
    public void Two_three_and_four_open_slots_are_valid()
    {
        Assert.Empty(Of(Human(0, 0), Ai(1, 1)).Validate());
        Assert.Empty(Of(Human(0, 0), Ai(1, 1), Ai(2, 2)).Validate());
        Assert.Empty(Of(Human(0, 0), Ai(1, 1), Ai(2, 2), Ai(3, 3)).Validate());
    }

    [Fact]
    public void A_closed_slot_does_not_count_and_is_allowed()
    {
        SkirmishSettings s = Of(Human(0, 0), Closed(), Ai(1, 1), Ai(2, 2));
        Assert.Empty(s.Validate());
        Assert.Equal(3, s.PlayerCount);
        Assert.Equal(new[] { 0, 2, 3 }, s.PlayerSlots);
    }

    [Fact]
    public void Fewer_than_two_open_slots_is_refused()
    {
        Assert.NotEmpty(Of(Human(0, 0), Closed()).Validate());
        Assert.NotEmpty(Of(Closed(), Closed(), Ai(0, 0)).Validate());
    }

    [Fact]
    public void Slot_count_must_be_two_to_four()
    {
        Assert.NotEmpty(Of(Human()).Validate());
        Assert.NotEmpty(Of(Human(0, 0), Ai(1, 1), Ai(2, 2), Ai(3, 3), Ai(4, 3)).Validate());
    }

    [Fact]
    public void At_most_one_slot_is_Human()
    {
        IReadOnlyList<string> e = Of(Human(0, 0), Human(1, 1)).Validate();
        Assert.Contains(e, m => m.Contains("Human"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void AI_tier_must_be_one_to_five(int tier)
    {
        Assert.NotEmpty(Of(Human(0, 0), Ai(1, 1, tier)).Validate());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void AI_tiers_one_to_five_are_valid(int tier)
    {
        Assert.Empty(Of(Human(0, 0), Ai(1, 1, tier)).Validate());
    }

    [Fact]
    public void Two_open_slots_cannot_share_a_colour_but_a_closed_slot_can()
    {
        Assert.NotEmpty(Of(Human(1, 0), Ai(1, 1)).Validate());
        Assert.Empty(Of(Human(1, 0), Ai(2, 1), new SkirmishSlot(SlotKind.Closed, Faction.Authority, 2, 1, 3)).Validate());
    }

    [Fact]
    public void Everyone_on_one_team_is_refused()
    {
        Assert.Contains(Of(Human(0, 0), Ai(1, 0)).Validate(), m => m.Contains("team"));
        Assert.Empty(Of(Human(0, 0), Ai(1, 0), Ai(2, 1)).Validate());
    }

    [Fact]
    public void Team_and_colour_ranges_are_checked()
    {
        Assert.NotEmpty(Of(Human(0, 0), Ai(1, 4)).Validate());
        Assert.NotEmpty(Of(Human(0, 0), Ai(99, 1)).Validate());
    }

    [Fact]
    public void A_spectator_game_has_no_Human_slot_and_is_valid()
    {
        SkirmishSettings s = Of(Ai(0, 0), Ai(1, 1), Ai(2, 2));
        Assert.True(s.IsSpectator);
        Assert.Empty(s.Validate());
        Assert.False(SkirmishSettings.Default.IsSpectator);
    }

    [Fact]
    public void Exactly_one_of_file_and_generator_must_be_set()
    {
        var slots = new[] { Human(0, 0), Ai(1, 1) };
        Assert.NotEmpty(new SkirmishSettings(new MapChoice(null, null), slots, 1).Validate());
        var both = new MapChoice("x.map.toml", new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 1));
        Assert.NotEmpty(new SkirmishSettings(both, slots, 1).Validate());
    }

    [Fact]
    public void A_generated_map_must_be_for_the_number_of_open_slots()
    {
        var gen3 = new MapChoice(null, new MapGenParams(Biome.Mountains, MapSize.Medium, 3, 5));
        Assert.NotEmpty(new SkirmishSettings(gen3, new[] { Human(0, 0), Ai(1, 1) }, 5).Validate());
        Assert.Empty(new SkirmishSettings(gen3, new[] { Human(0, 0), Ai(1, 1), Ai(2, 2) }, 5).Validate());
        Assert.Equal(3, new SkirmishSettings(gen3, new[] { Human(0, 0), Ai(1, 1), Ai(2, 2) }, 5).ResolvedGenerator!.Players);
    }

    [Fact]
    public void The_human_is_player_zero_then_the_AIs_in_slot_order()
    {
        SkirmishSettings s = Of(Ai(0, 0), Closed(), Human(1, 1), Ai(2, 2));
        Assert.Equal(new[] { 2, 0, 3 }, s.PlayerSlots);
        Assert.Equal(new byte[] { 1, 2 }, s.AiPlayers.Select(a => a.Player).ToArray());
    }

    [Fact]
    public void Settings_compare_by_value_including_the_slots()
    {
        Assert.Equal(SkirmishSettings.Default, new SkirmishSettings(0, 2, 0));
        Assert.NotEqual(SkirmishSettings.Default, new SkirmishSettings(0, 3, 0));
        Assert.Equal(SkirmishSettings.Default.GetHashCode(), new SkirmishSettings(0, 2, 0).GetHashCode());
    }

    [Fact]
    public void Slot_labels_name_kind_faction_tier_and_team()
    {
        string[] tiers = { "Recruit", "Regular", "Veteran", "Elite", "Master" };
        Assert.Equal("1 Closed", SkirmishSetupScreen.SlotLabel(Closed(), 0, tiers));
        Assert.Equal("2 Human Authority T1", SkirmishSetupScreen.SlotLabel(Human(), 1, tiers));
        Assert.Equal("3 AI Veteran Directorate T3",
            SkirmishSetupScreen.SlotLabel(new SkirmishSlot(SlotKind.Ai, Faction.Directorate, 3, 2, 2), 2, tiers));
    }
}
