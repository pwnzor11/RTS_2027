// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui;

/// <summary>
/// The UI scale from the backbuffer height: 1 up to 800 px, 1.5 up to 1200 px, 2 above. Text is rasterised at the nearest
/// atlas size to 12 px times the scale, so nothing is stretched.
/// </summary>
public static class UiScale
{
    /// <summary>Forced scale in whole steps; 0 means automatic from the backbuffer height.</summary>
    public static int Override { get; set; }

    /// <summary>The UI scale for a backbuffer of <paramref name="height"/> pixels, or the override when one is set.</summary>
    /// <param name="height">Backbuffer height in pixels.</param>
    public static float FromBackbuffer(int height)
    {
        if (Override > 0) return Override;
        if (height <= 800) return 1f;
        if (height <= 1200) return 1.5f;
        return 2f;
    }
}
