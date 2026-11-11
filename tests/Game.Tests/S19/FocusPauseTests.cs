// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Input;
using Rts.Game.Screens;

namespace Rts.Game.Tests.S19;

/// <summary>S19 UI1 item 2 (D-205): single-player games pause on focus loss and resume on focus regain unless the player had paused.</summary>
public sealed class FocusPauseTests
{
    private sealed class Pause : IScreen
    {
        public bool BlocksUpdateBelow => true;
        public bool BlocksDrawBelow => false;
        public void Enter() { }
        public void Exit() { }
        public void Update(InputFrame input, GameTime t) { }
        public void Draw(SpriteBatch sb) { }
    }

    private sealed class Game : IScreen, IFocusPausable
    {
        public bool CanPause = true;
        public bool BlocksUpdateBelow => true;
        public bool BlocksDrawBelow => true;
        public void Enter() { }
        public void Exit() { }
        public void Update(InputFrame input, GameTime t) { }
        public void Draw(SpriteBatch sb) { }
        public IScreen? CreateFocusPause() => CanPause ? new Pause() : null;
    }

    [Fact]
    public void Focus_loss_pauses_and_focus_regain_resumes()
    {
        var stack = new ScreenStack();
        var focus = new FocusPause();
        stack.Push(new Game());
        focus.Update(true, stack);
        Assert.Equal(1, stack.Count);
        focus.Update(false, stack);
        Assert.IsType<Pause>(stack.Top);
        focus.Update(false, stack);
        Assert.Equal(2, stack.Count);
        focus.Update(true, stack);
        Assert.IsType<Game>(stack.Top);
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void A_game_that_must_not_pause_is_left_running()
    {
        var stack = new ScreenStack();
        var focus = new FocusPause();
        stack.Push(new Game { CanPause = false });   // multiplayer, spectator, sandbox or already paused by P
        focus.Update(false, stack);
        focus.Update(true, stack);
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void A_pause_menu_the_player_opened_is_not_closed_on_regain()
    {
        var stack = new ScreenStack();
        var focus = new FocusPause();
        stack.Push(new Game());
        stack.Push(new Pause());   // the player's own pause menu is on top: it is not a game screen
        focus.Update(false, stack);
        focus.Update(true, stack);
        Assert.Equal(2, stack.Count);
    }

    [Fact]
    public void Non_game_screens_are_never_paused()
    {
        var stack = new ScreenStack();
        var focus = new FocusPause();
        stack.Push(new Pause());
        focus.Update(false, stack);
        Assert.Equal(1, stack.Count);
    }
}
