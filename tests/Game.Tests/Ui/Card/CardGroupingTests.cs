// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Game.Ui.Card;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Ui.Card;

public sealed class CardGroupingTests
{
    private static CardCameo C(string id, int n, int hp = 10) => new(new UnitKey(ActorKind.Infantry, new EntityId((ushort)n, 1)), id, hp, 10);

    [Fact]
    public void Units_are_grouped_by_type_in_first_seen_order_keeping_selection_order_inside_a_type()
    {
        var input = new[] { C("a", 1), C("b", 2), C("a", 3), C("c", 4), C("b", 5) };
        IReadOnlyList<CardCameo> ordered = CardGrouping.Order(input);
        Assert.Equal(new[] { 1, 3, 2, 5, 4 }, ordered.Select(c => (int)c.Key.Id.Index).ToArray());
        IReadOnlyList<CardGroup> groups = CardGrouping.Groups(ordered, id => id.ToUpperInvariant());
        Assert.Equal(new[] { ("a", 2), ("b", 2), ("c", 1) }, groups.Select(g => (g.RulesId, g.Count)).ToArray());
        Assert.Equal("A", groups[0].Name);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 0)]
    [InlineData(24, 24, 0)]
    [InlineData(25, 23, 2)]
    [InlineData(100, 23, 77)]
    public void Overflow_turns_the_last_tile_into_plus_n(int count, int shown, int overflow) =>
        Assert.Equal((shown, overflow), CardGrouping.Visible(count));

    [Fact]
    public void Header_lists_the_counts_per_type()
    {
        var groups = new[] { new CardGroup("r", "Rifleman", 4), new CardGroup("s", "Scout", 1) };
        Assert.Equal("5 selected  -  4 Rifleman, 1 Scout", CardGrouping.Header(5, groups));
    }

    [Theory]
    [InlineData(45, 50, "45 / 50", 900)]
    [InlineData(-3, 50, "0 / 50", 0)]
    [InlineData(80, 50, "80 / 50", 1000)]
    [InlineData(5, 0, "5 / 0", 0)]
    public void Health_text_and_fill_are_clamped(int hp, int max, string text, int permille)
    {
        Assert.Equal(text, CardGrouping.HealthText(hp, max));
        Assert.Equal(permille, CardGrouping.HealthPermille(hp, max));
    }

    [Theory]
    [InlineData(25, "2.5")]
    [InlineData(40, "4.0")]
    [InlineData(3, "0.3")]
    public void Tenths_prints_one_decimal(int tenths, string text) => Assert.Equal(text, CardGrouping.Tenths(tenths));

    [Fact]
    public void Armour_words_cover_every_class()
    {
        foreach (ArmorClass a in Enum.GetValues<ArmorClass>()) Assert.False(string.IsNullOrWhiteSpace(CardGrouping.ArmourText(a)));
        Assert.Equal("light", CardGrouping.ArmourText(ArmorClass.Light));
    }

    [Fact]
    public void Stats_wrap_into_at_most_the_allowed_lines()
    {
        var items = new[] { "Damage 6", "Range 4.0", "Speed 3.0", "Armour light", "Sight 5" };
        int Measure(string s) => s.Length * 10;
        Assert.Equal(new[] { "Damage 6  Range 4.0  Speed 3.0  Armour light  Sight 5" }, CardGrouping.WrapStats(items, 1000, 3, Measure));
        IReadOnlyList<string> two = CardGrouping.WrapStats(items, 300, 3, Measure);
        Assert.True(two.Count is 2 or 3);
        Assert.All(two, l => Assert.True(Measure(l) <= 300));
        IReadOnlyList<string> capped = CardGrouping.WrapStats(items, 120, 2, Measure);
        Assert.Equal(2, capped.Count);
    }
}
