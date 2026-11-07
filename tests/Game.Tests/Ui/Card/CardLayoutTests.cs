// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;
using Rts.Game.Ui.Card;

namespace Rts.Game.Tests.Ui.Card;

public sealed class CardLayoutTests
{
    private static readonly Rectangle Map = new(0, 0, 1288, 900);   // 1600x900 minus a 312 px sidebar

    private static CardLayout Compute(Rectangle map, float s, CardKind kind, int buttons, int units = 0) =>
        CardLayout.Compute(map, s, (int)(12 * s), kind, buttons, units);

    [Fact]
    public void Nothing_selected_has_no_card()
    {
        CardLayout l = Compute(Map, 1.5f, CardKind.None, 0);
        Assert.Equal(Rectangle.Empty, l.Card);
        Assert.False(CardRouting.Over(l, new Point(644, 850)));
    }

    [Theory]
    [InlineData(CardKind.Unit, 5)]
    [InlineData(CardKind.Multi, 5)]
    [InlineData(CardKind.Building, 2)]
    public void Card_is_centred_in_the_map_view_and_clear_of_the_sidebar_and_the_edge_band(CardKind kind, int buttons)
    {
        CardLayout l = Compute(Map, 1.5f, kind, buttons, 5);
        Assert.InRange(Math.Abs(Map.Center.X - l.Card.Center.X), 0, 1);
        Assert.True(l.Card.Right <= Map.Right, "inside the map view, so never over the sidebar");
        Assert.True(l.Card.Left >= Map.Left);
        Assert.True(l.Card.Bottom <= Map.Bottom - CameraScroll.EdgeBandPx, "bottom edge scroll band stays free");
        Assert.True(l.Card.Top >= Map.Top);
    }

    [Fact]
    public void Edge_band_below_the_card_is_not_part_of_the_card()
    {
        CardLayout l = Compute(Map, 1.5f, CardKind.Unit, 5);
        Assert.False(CardRouting.Over(l, new Point(Map.Center.X, Map.Bottom - 1)));
        Assert.False(CardRouting.Over(l, new Point(Map.Center.X, Map.Bottom - CameraScroll.EdgeBandPx)));
    }

    [Fact]
    public void Card_scale_follows_ui_then_shrinks_in_a_narrow_map_view_but_never_below_one()
    {
        Assert.Equal(1.5f, CardLayout.ScaleFor(1288, 1.5f));
        Assert.Equal(2f, CardLayout.ScaleFor(2400, 2f));
        float narrow = CardLayout.ScaleFor(700, 2f);
        Assert.InRange(narrow, 1f, 1.3f);
        Assert.Equal(1f, CardLayout.ScaleFor(300, 1.5f));
        Assert.Equal(1f, CardLayout.ScaleFor(300, 1f));
    }

    [Fact]
    public void Card_width_adapts_to_the_map_view_width()
    {
        var narrow = new Rectangle(0, 0, 500, 600);
        CardLayout wide = Compute(Map, 1f, CardKind.Unit, 5);
        CardLayout small = Compute(narrow, 1f, CardKind.Unit, 5);
        Assert.True(small.Card.Width < wide.Card.Width);
        Assert.True(small.Card.Left >= narrow.Left && small.Card.Right <= narrow.Right);
        Assert.True(small.Buttons.All(b => small.Card.Contains(b)), "buttons stay inside a narrow card");
    }

    [Fact]
    public void Buttons_sit_inside_the_card_and_do_not_overlap_each_other_or_the_text()
    {
        foreach (float s in new[] { 1f, 1.5f, 2f })
        {
            CardLayout l = Compute(Map, s, CardKind.Unit, 5);
            Assert.Equal(5, l.Buttons.Count);
            foreach (Rectangle b in l.Buttons) Assert.True(l.Card.Contains(b));
            for (int i = 0; i < l.Buttons.Count; i++)
            {
                Assert.False(l.Buttons[i].Intersects(l.Text));
                for (int j = i + 1; j < l.Buttons.Count; j++) Assert.False(l.Buttons[i].Intersects(l.Buttons[j]));
            }

            Assert.False(l.Portrait.Intersects(l.Text));
            Assert.True(l.Card.Contains(l.Portrait));
        }
    }

    [Fact]
    public void Group_grid_has_one_tile_per_unit_up_to_the_limit_and_a_plus_n_tile_beyond()
    {
        CardLayout few = Compute(Map, 1.5f, CardKind.Multi, 4, 5);
        Assert.Equal(5, few.Tiles.Count);
        Assert.Equal(5, few.TileCount);
        Assert.Equal(0, few.Overflow);

        CardLayout full = Compute(Map, 1.5f, CardKind.Multi, 4, 24);
        Assert.Equal(24, full.Tiles.Count);
        Assert.Equal(0, full.Overflow);

        CardLayout many = Compute(Map, 1.5f, CardKind.Multi, 4, 40);
        Assert.Equal(CardGrouping.MaxTiles, many.Tiles.Count);
        Assert.Equal(23, many.TileCount);
        Assert.Equal(17, many.Overflow);
        foreach (Rectangle t in many.Tiles) Assert.True(many.Card.Contains(t));
        foreach (Rectangle t in many.Tiles) foreach (Rectangle b in many.Buttons) Assert.False(t.Intersects(b));
    }

    [Fact]
    public void Group_grid_tiles_do_not_overlap()
    {
        CardLayout l = Compute(Map, 1f, CardKind.Multi, 4, 30);
        for (int i = 0; i < l.Tiles.Count; i++)
            for (int j = i + 1; j < l.Tiles.Count; j++) Assert.False(l.Tiles[i].Intersects(l.Tiles[j]));
    }

    [Fact]
    public void Card_never_rises_above_a_short_map_view()
    {
        var tiny = new Rectangle(0, 100, 640, 120);
        CardLayout l = Compute(tiny, 1f, CardKind.Unit, 5);
        Assert.True(l.Card.Top >= tiny.Top);
    }
}
