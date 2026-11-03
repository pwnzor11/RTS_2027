// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Screens;

namespace Rts.Game.Tests.Screens;

public sealed class MenuPanelTests
{
    private static readonly Viewport Window = new(0, 0, 1280, 720);
    private const int Scale = 2;

    private static MouseState MouseAt(Point p, bool left = false) =>
        new(p.X, p.Y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released,
            ButtonState.Released, ButtonState.Released);

    private static (MenuPanel Panel, List<string> Log) Build()
    {
        var log = new List<string>();
        var panel = new MenuPanel("Test", new[]
        {
            new MenuItem(() => "One", () => log.Add("one"), d => log.Add($"adjust {d}"), new MenuButton("+", () => log.Add("plus"))),
            new MenuItem(() => "Two", () => log.Add("two")),
        });
        panel.Update(new InputFrame(new KeyboardState(), new KeyboardState(), new MouseState(), new MouseState()), Window, Scale);
        return (panel, log);
    }

    [Fact]
    public void Keyboard_moves_selection_wraps_and_activates()
    {
        (MenuPanel panel, List<string> log) = Build();
        var down = new InputFrame(new KeyboardState(Keys.Down), new KeyboardState(), new MouseState(), new MouseState());
        panel.Update(down, Window, Scale);
        Assert.Equal(1, panel.Selected);
        Assert.False(down.Pressed(Keys.Down));

        panel.Update(new InputFrame(new KeyboardState(Keys.Down), new KeyboardState(), new MouseState(), new MouseState()), Window, Scale);
        Assert.Equal(0, panel.Selected); // wrapped

        panel.Update(new InputFrame(new KeyboardState(Keys.Right), new KeyboardState(), new MouseState(), new MouseState()), Window, Scale);
        panel.Update(new InputFrame(new KeyboardState(Keys.Enter), new KeyboardState(), new MouseState(), new MouseState()), Window, Scale);
        Assert.Equal(new[] { "adjust 1", "one" }, log);
    }

    [Fact]
    public void Mouse_hover_selects_click_activates_and_button_click_wins_over_the_row()
    {
        (MenuPanel panel, List<string> log) = Build();
        Point onTwo = panel.RowRect(1).Center;
        var hover = new InputFrame(new KeyboardState(), new KeyboardState(), MouseAt(onTwo), MouseAt(Point.Zero));
        panel.Update(hover, Window, Scale);
        Assert.Equal(1, panel.Selected);

        var click = new InputFrame(new KeyboardState(), new KeyboardState(), MouseAt(onTwo, left: true), MouseAt(onTwo));
        panel.Update(click, Window, Scale);
        Assert.Equal(new[] { "two" }, log);
        Assert.False(click.LeftPressed); // consumed: the map below never sees the click

        Point onPlus = panel.ButtonRect(0, 0).Center;
        panel.Update(new InputFrame(new KeyboardState(), new KeyboardState(), MouseAt(onPlus, left: true), MouseAt(onPlus)), Window, Scale);
        Assert.Equal(new[] { "two", "plus" }, log);
    }

    [Fact]
    public void Mouse_over_the_panel_is_consumed_and_outside_it_is_not()
    {
        (MenuPanel panel, _) = Build();
        var outside = new InputFrame(new KeyboardState(), new KeyboardState(), MouseAt(new Point(2, 2), true), MouseAt(new Point(2, 2)));
        panel.Update(outside, Window, Scale);
        Assert.True(outside.LeftPressed);
        Assert.False(panel.HitTest(new Point(2, 2)));
        Assert.True(panel.HitTest(panel.RowRect(0).Center));
    }
}
