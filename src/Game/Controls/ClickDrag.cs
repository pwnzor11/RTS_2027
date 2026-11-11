// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Controls;

/// <summary>
/// S18 GAMEUI-04: when a left-button press that moved a little is a click and not a box select. The threshold is in client pixels and
/// grows with the UI scale, so a jittery mouse or a high-DPI screen does not turn a click on a unit into an empty box that clears the selection.
/// </summary>
public static class ClickDrag
{
    /// <summary>Movement, at UI scale 1, up to which a press-and-release is still a click.</summary>
    public const float ThresholdPx = 8f;

    /// <summary>The movement, in pixels, beyond which a press becomes a drag (box select).</summary>
    public static float Threshold(float uiScale) => ThresholdPx * Math.Max(1f, uiScale);

    /// <summary>True when the pointer has moved far enough from <paramref name="start"/> for the press to be a drag.</summary>
    public static bool IsDrag(Point start, Point now, float uiScale) =>
        Vector2.Distance(start.ToVector2(), now.ToVector2()) > Threshold(uiScale);
}
