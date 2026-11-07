// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Game.Ui.Card;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Ui.Card;

public sealed class CardRoutingTests
{
    private static readonly Rectangle Map = new(0, 0, 1288, 900);

    private static CardModel Group(int n) => new()
    {
        Kind = CardKind.Multi,
        Cameos = Enumerable.Range(1, n).Select(i => new CardCameo(new UnitKey(ActorKind.Infantry, new EntityId((ushort)i, 1)), "r", 10, 10)).ToList(),
        Buttons = CardModelBuilder.UnitButtons(deployApplies: true),
    };

    private static CardLayout LayoutOf(CardModel m) => CardLayout.Compute(Map, 1.5f, 18, m.Kind, m.Buttons.Count, m.Cameos.Count);

    [Fact]
    public void A_click_off_the_card_passes_through()
    {
        CardModel m = Group(5);
        CardLayout l = LayoutOf(m);
        CardAction a = CardRouting.Click(l, m, new Point(10, 10), shift: false);
        Assert.Equal(CardActionKind.None, a.Kind);
        Assert.False(a.Consumed);
    }

    [Fact]
    public void A_click_on_empty_card_space_is_swallowed_not_passed_to_the_map()
    {
        CardModel m = Group(2);
        CardLayout l = LayoutOf(m);
        var empty = new Point(l.Card.X + 2, l.Card.Bottom - 2);
        CardAction a = CardRouting.Click(l, m, empty, shift: false);
        Assert.Equal(CardActionKind.Swallow, a.Kind);
        Assert.True(a.Consumed);
    }

    [Fact]
    public void Clicking_a_cameo_selects_only_that_unit_and_shift_removes_it()
    {
        CardModel m = Group(5);
        CardLayout l = LayoutOf(m);
        Point third = l.Tiles[2].Center;
        CardAction pick = CardRouting.Click(l, m, third, shift: false);
        Assert.Equal(CardActionKind.SelectOnly, pick.Kind);
        Assert.Equal(m.Cameos[2].Key, pick.Unit);
        CardAction drop = CardRouting.Click(l, m, third, shift: true);
        Assert.Equal(CardActionKind.Remove, drop.Kind);
        Assert.Equal(m.Cameos[2].Key, drop.Unit);
    }

    [Fact]
    public void The_plus_n_tile_does_nothing_but_swallows_the_click()
    {
        CardModel m = Group(40);
        CardLayout l = LayoutOf(m);
        CardAction a = CardRouting.Click(l, m, l.Tiles[^1].Center, shift: false);
        Assert.Equal(CardActionKind.Swallow, a.Kind);
    }

    [Fact]
    public void Order_buttons_route_to_their_key_table_action()
    {
        CardModel m = Group(3);
        CardLayout l = LayoutOf(m);
        GameAction[] expected = { GameAction.Stop, GameAction.Guard, GameAction.Scatter, GameAction.AttackMove, GameAction.Deploy };
        for (int i = 0; i < expected.Length; i++)
        {
            CardAction a = CardRouting.Click(l, m, l.Buttons[i].Center, shift: false);
            Assert.Equal(CardActionKind.Order, a.Kind);
            Assert.Equal(expected[i], a.Action);
        }
    }

    [Fact]
    public void Building_buttons_route_to_sell_and_repair()
    {
        var m = new CardModel { Kind = CardKind.Building, Buttons = CardModelBuilder.BuildingButtons() };
        CardLayout l = LayoutOf(m);
        Assert.Equal(CardActionKind.Sell, CardRouting.Click(l, m, l.Buttons[0].Center, false).Kind);
        Assert.Equal(CardActionKind.Repair, CardRouting.Click(l, m, l.Buttons[1].Center, false).Kind);
    }

    [Fact]
    public void Button_hover_finds_the_button_under_the_cursor()
    {
        CardModel m = Group(3);
        CardLayout l = LayoutOf(m);
        Assert.Equal(1, CardRouting.ButtonAt(l, l.Buttons[1].Center));
        Assert.Equal(-1, CardRouting.ButtonAt(l, new Point(0, 0)));
    }

    [Fact]
    public void Button_labels_show_the_current_bound_key()
    {
        KeyBindings saved = GameKeys.Active;
        try
        {
            Assert.Equal("Stop S", CardModelBuilder.UnitButtons(false)[0].Text);
            GameKeys.Active = saved.With(GameAction.Stop, Microsoft.Xna.Framework.Input.Keys.H);
            Assert.Equal("Stop H", CardModelBuilder.UnitButtons(false)[0].Text);
            Assert.Equal(4, CardModelBuilder.UnitButtons(false).Count);
            Assert.Equal("Deploy D", CardModelBuilder.UnitButtons(true)[4].Text);
        }
        finally
        {
            GameKeys.Active = saved;
        }
    }
}
