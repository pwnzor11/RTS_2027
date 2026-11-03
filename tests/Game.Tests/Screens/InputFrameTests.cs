// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;

namespace Rts.Game.Tests.Screens;

public sealed class InputFrameTests
{
    private static MouseState MouseAt(int x, int y, bool left = false, bool right = false, int wheel = 0) =>
        new(x, y, wheel, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
            right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);

    [Fact]
    public void Pressed_is_true_only_on_the_edge()
    {
        var edge = new InputFrame(new KeyboardState(Keys.A), new KeyboardState(), new MouseState(), new MouseState());
        var held = new InputFrame(new KeyboardState(Keys.A), new KeyboardState(Keys.A), new MouseState(), new MouseState());
        Assert.True(edge.Pressed(Keys.A));
        Assert.False(held.Pressed(Keys.A));
        Assert.True(held.IsDown(Keys.A));
    }

    [Fact]
    public void Consumed_key_is_invisible_to_later_queries_but_other_keys_are_not()
    {
        var input = new InputFrame(new KeyboardState(Keys.Escape, Keys.A), new KeyboardState(), new MouseState(), new MouseState());
        Assert.True(input.ConsumePressed(Keys.Escape));
        Assert.False(input.Pressed(Keys.Escape));
        Assert.False(input.IsDown(Keys.Escape));
        Assert.False(input.ConsumePressed(Keys.Escape));
        Assert.True(input.Pressed(Keys.A));
        Assert.True(input.Keyboard.IsKeyDown(Keys.Escape)); // raw state is untouched
    }

    [Fact]
    public void ConsumeKeyboard_hides_all_keys_and_text()
    {
        var input = new InputFrame(new KeyboardState(Keys.A), new KeyboardState(), new MouseState(), new MouseState(), "ab");
        Assert.Equal(2, input.Text.Count);
        input.ConsumeKeyboard();
        Assert.False(input.Pressed(Keys.A));
        Assert.Empty(input.Text);
    }

    [Fact]
    public void ConsumeText_returns_the_characters_once()
    {
        var input = new InputFrame(new KeyboardState(), new KeyboardState(), new MouseState(), new MouseState(), "12");
        Assert.Equal(new[] { '1', '2' }, input.ConsumeText());
        Assert.Empty(input.ConsumeText());
        Assert.Empty(input.Text);
    }

    [Fact]
    public void Consuming_left_stops_press_down_and_release_for_screens_below()
    {
        var press = new InputFrame(new KeyboardState(), new KeyboardState(), MouseAt(5, 5, left: true), MouseAt(5, 5));
        Assert.True(press.LeftPressed);
        press.ConsumeLeft();
        Assert.False(press.LeftPressed);
        Assert.False(press.LeftDown);
        Assert.True(press.Mouse.LeftButton == ButtonState.Pressed);

        var release = new InputFrame(new KeyboardState(), new KeyboardState(), MouseAt(5, 5), MouseAt(5, 5, left: true));
        Assert.True(release.LeftReleased);
        release.ConsumeLeft();
        Assert.False(release.LeftReleased);
    }

    [Fact]
    public void Consuming_one_button_leaves_the_others()
    {
        var input = new InputFrame(new KeyboardState(), new KeyboardState(), MouseAt(0, 0, left: true, right: true, wheel: 120), MouseAt(0, 0));
        input.ConsumeLeft();
        Assert.True(input.RightPressed);
        Assert.Equal(120, input.WheelDelta);
        input.ConsumeWheel();
        Assert.Equal(0, input.WheelDelta);
    }

    [Fact]
    public void ConsumeMouseOver_blocks_the_mouse_only_inside_the_region()
    {
        var region = new RectRegion(new Rectangle(0, 0, 100, 100));
        var inside = new InputFrame(new KeyboardState(), new KeyboardState(), MouseAt(50, 50, left: true, right: true), MouseAt(50, 50));
        Assert.True(inside.ConsumeMouseOver(region));
        Assert.False(inside.LeftPressed);
        Assert.False(inside.RightPressed);

        var outside = new InputFrame(new KeyboardState(), new KeyboardState(), MouseAt(150, 50, left: true), MouseAt(150, 50));
        Assert.False(outside.ConsumeMouseOver(region));
        Assert.True(outside.LeftPressed);
    }
}
