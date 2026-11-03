// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>Stack changes shared by the pause and game-over screens.</summary>
internal static class GameFlow
{
    /// <summary>
    /// Removes the menu on top and the game under it (which saves its replay), then pushes a fresh game screen. The
    /// factory runs only after the old game has exited, so the new replay does not collide with the old file.
    /// </summary>
    public static void Restart(GameServices s, Func<IScreen> restart) => s.Screens.Post(() =>
    {
        s.Screens.Pop();
        s.Screens.Pop();
        s.Screens.Push(restart());
    });

    /// <summary>Exits every screen (the game saves its replay) and shows a new main menu.</summary>
    public static void QuitToMenu(GameServices s) => s.Screens.Post(() => s.Screens.ReplaceAll(new MainMenuScreen(s)));
}
