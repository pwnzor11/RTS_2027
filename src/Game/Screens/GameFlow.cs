// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>Stack changes shared by the pause and game-over screens.</summary>
public static class GameFlow
{
    /// <summary>
    /// Removes the menu on top and the game under it (which saves its replay), then pushes a fresh game screen. The
    /// factory runs only after the old game has exited, so the new replay does not collide with the old file. When the factory
    /// fails on a damaged data file, the stack is not left empty (S18 GS-10): the main menu comes back with a Notice saying why.
    /// <paramref name="menu"/> builds that menu (tests); null = the real main menu.
    /// </summary>
    public static void Restart(GameServices s, Func<IScreen> restart, Func<IScreen>? menu = null) => s.Screens.Post(() =>
    {
        s.Screens.Pop();
        s.Screens.Pop();
        try
        {
            s.Screens.Push(restart());
        }
        catch (Exception e) when (ScreenGuard.IsDataError(e))
        {
            if (s.Screens.Count == 0) s.Screens.Push(menu?.Invoke() ?? new MainMenuScreen(s));
            s.Screens.Push(new NoticeScreen(s, "Cannot restart", ScreenGuard.Describe(e)));
        }
    });

    /// <summary>Exits every screen (the game saves its replay) and shows a new main menu.</summary>
    public static void QuitToMenu(GameServices s) => s.Screens.Post(() => s.Screens.ReplaceAll(new MainMenuScreen(s)));
}
