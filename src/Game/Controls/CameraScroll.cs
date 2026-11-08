// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Controls;

/// <summary>
/// The pure maths of camera scrolling (M11-02): the edge-scroll zone and the scroll speed. No input or window state is read here,
/// so the rules are testable; <c>GameScreen</c> feeds in the cursor, focus and drag state.
/// </summary>
public static class CameraScroll
{
    /// <summary>Width in window pixels of the band along each window edge that scrolls the camera.</summary>
    public const int EdgeBandPx = 12;

    /// <summary>
    /// The rectangle the edge band is measured against (M11-10b): the window's client area in client pixels, which is what the mouse
    /// position is reported in. The back buffer can differ from it while a windowed game is being resized, so it is only the
    /// fallback for a client size that is not known yet (zero or negative).
    /// </summary>
    public static Rectangle ClientRect(int clientWidth, int clientHeight, int fallbackWidth, int fallbackHeight) =>
        clientWidth > 0 && clientHeight > 0 ? new Rectangle(0, 0, clientWidth, clientHeight) : new Rectangle(0, 0, fallbackWidth, fallbackHeight);

    /// <summary>Speed multiplier while Shift is held.</summary>
    public const int ShiftMultiplier = 2;

    /// <summary>
    /// The scroll direction for a cursor at <paramref name="mouse"/>: each axis is -1, 0 or +1 (corners give both axes) and a
    /// diagonal is normalised. Zero unless edge scroll is on, the game window has focus, the cursor is inside the window and no
    /// selection box, minimap drag or similar is in progress. Only the window's own edges count: the left edge of the sidebar,
    /// inside the window, does not scroll, but the window's right edge (which is the sidebar's outer edge) does.
    /// </summary>
    /// <param name="mouse">Cursor in window pixels.</param>
    /// <param name="window">The window bounds in pixels.</param>
    /// <param name="enabled">The Edge scroll setting.</param>
    /// <param name="windowFocused">True while the game window has focus.</param>
    /// <param name="busy">True while a selection box, minimap drag, middle-drag or text entry is in progress.</param>
    /// <param name="band">Band width in pixels.</param>
    public static Vector2 EdgeDirection(Point mouse, Rectangle window, bool enabled, bool windowFocused, bool busy, int band = EdgeBandPx)
    {
        if (!enabled || !windowFocused || busy || !window.Contains(mouse)) return Vector2.Zero;
        float x = 0f, y = 0f;
        if (mouse.X < window.Left + band) x = -1f;
        else if (mouse.X >= window.Right - band) x = 1f;
        if (mouse.Y < window.Top + band) y = -1f;
        else if (mouse.Y >= window.Bottom - band) y = 1f;
        var dir = new Vector2(x, y);
        if (x != 0f && y != 0f) dir.Normalize();
        return dir;
    }

    /// <summary>The scroll speed in screen pixels per second at any zoom: the setting, doubled with Shift held.</summary>
    public static float Speed(int settingPxPerSecond, bool shift) => settingPxPerSecond * (shift ? ShiftMultiplier : 1);

    /// <summary>
    /// The camera move in screen pixels for one frame. <paramref name="direction"/> may be the sum of key and edge directions;
    /// it is limited to length 1 so keys plus edge never exceed one speed.
    /// </summary>
    public static Vector2 FrameDelta(Vector2 direction, int settingPxPerSecond, bool shift, float seconds)
    {
        if (direction == Vector2.Zero) return Vector2.Zero;
        if (direction.LengthSquared() > 1f) direction.Normalize();
        return direction * Speed(settingPxPerSecond, shift) * seconds;
    }
}
