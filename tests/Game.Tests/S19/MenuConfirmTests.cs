// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Audio;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Screens;

namespace Rts.Game.Tests.S19;

/// <summary>S19 UI1 item 1 (D-205): Quit to menu, Restart and Overwrite ask first; Esc on the main menu does not quit.</summary>
[Collection("UiStatics")]
public sealed class MenuConfirmTests
{
    private sealed class Dummy : IScreen
    {
        public bool BlocksUpdateBelow => true;
        public bool BlocksDrawBelow => true;
        public void Enter() { }
        public void Exit() { }
        public void Update(InputFrame input, GameTime t) { }
        public void Draw(SpriteBatch sb) { }
    }

    private sealed class Rig
    {
        public readonly ScreenStack Stack = new();
        public readonly GameServices S;
        public int QuitCalls;
        public Rig()
        {
            S = new GameServices(null!, null!, null!, "data/rules", NullAudio.Instance, new Settings(), Stack, () => QuitCalls++)
            {
                ViewportOverride = () => new Viewport(0, 0, 1280, 720),
            };
        }

        public void Press(Keys key)
        {
            Stack.Update(new InputFrame(new KeyboardState(key), new KeyboardState(), new MouseState(), new MouseState()), new GameTime());
        }
    }

    private static PauseScreen Pause(Rig r) => new(r.S, () => new Dummy());

    // Resume, Settings, Archive, Restart, Quit to menu (no save target).
    private static void Select(Rig r, int row)
    {
        for (int i = 0; i < row; i++) r.Press(Keys.Down);
    }

    [Fact]
    public void Quit_to_menu_asks_first_and_Esc_cancels()
    {
        var r = new Rig();
        r.Stack.Push(new Dummy());
        r.Stack.Push(Pause(r));
        Select(r, 4);
        r.Press(Keys.Enter);
        Assert.IsType<ConfirmScreen>(r.Stack.Top);
        Assert.Equal(3, r.Stack.Count);
        r.Press(Keys.Escape);
        Assert.IsType<PauseScreen>(r.Stack.Top);
        Assert.Equal(2, r.Stack.Count);
    }

    [Fact]
    public void Quit_to_menu_goes_to_the_menu_only_after_yes()
    {
        var r = new Rig();
        r.Stack.Push(new Dummy());
        r.Stack.Push(Pause(r));
        Select(r, 4);
        r.Press(Keys.Enter);
        r.Press(Keys.Enter);   // Cancel is preselected: Enter must not confirm
        Assert.IsType<PauseScreen>(r.Stack.Top);
        Select(r, 0);
        r.Press(Keys.Enter);   // Quit to menu again
        r.Press(Keys.Up);      // to the confirming row
        r.Press(Keys.Enter);
        Assert.IsType<MainMenuScreen>(r.Stack.Top);
        Assert.Equal(1, r.Stack.Count);
    }

    [Fact]
    public void Restart_asks_first_and_builds_the_game_only_after_yes()
    {
        var r = new Rig();
        int built = 0;
        r.Stack.Push(new Dummy());
        r.Stack.Push(new PauseScreen(r.S, () => { built++; return new Dummy(); }));
        Select(r, 3);
        r.Press(Keys.Enter);
        Assert.IsType<ConfirmScreen>(r.Stack.Top);
        Assert.Equal(0, built);
        r.Press(Keys.Up);
        r.Press(Keys.Enter);
        Assert.Equal(1, built);
        Assert.Equal(1, r.Stack.Count);
        Assert.IsType<Dummy>(r.Stack.Top);
    }

    [Fact]
    public void Esc_on_the_main_menu_does_not_quit()
    {
        var r = new Rig();
        r.Stack.Push(new MainMenuScreen(r.S, new SaveCatalog(Path.Combine(Path.GetTempPath(), "rts_s19_nosaves_" + Guid.NewGuid().ToString("N")))));
        r.Press(Keys.Escape);
        Assert.Equal(0, r.QuitCalls);
        Assert.IsType<MainMenuScreen>(r.Stack.Top);
    }
}
