// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game;

/// <summary>
/// The window-size rules of U-07 (GAMESYS-03): which windowed sizes fit the current desktop, and when a size the player resized to
/// should be remembered. Pure, so it is unit-tested without a window.
/// </summary>
public static class WindowSizeRule
{
    /// <summary>The smallest window width the game remembers (the settings floor is 320).</summary>
    public const int MinWidth = 320;

    /// <summary>The smallest window height the game remembers (the settings floor is 240).</summary>
    public const int MinHeight = 240;

    /// <summary>True when a window of <paramref name="width"/> x <paramref name="height"/> fits the desktop. An unknown desktop (0) fits everything.</summary>
    public static bool Fits(int width, int height, int desktopWidth, int desktopHeight) =>
        desktopWidth <= 0 || desktopHeight <= 0 || (width <= desktopWidth && height <= desktopHeight);

    /// <summary>
    /// The settings with the client size of the window remembered. Full screen sizes are the desktop size, not the window, so they are
    /// not kept; a size below the floor is ignored. Returns <paramref name="settings"/> itself when nothing changes.
    /// </summary>
    public static Settings Remember(Settings settings, bool fullScreen, int clientWidth, int clientHeight)
    {
        if (fullScreen || clientWidth < MinWidth || clientHeight < MinHeight) return settings;
        if (settings.WindowWidth == clientWidth && settings.WindowHeight == clientHeight) return settings;
        return settings with { WindowWidth = clientWidth, WindowHeight = clientHeight };
    }
}
