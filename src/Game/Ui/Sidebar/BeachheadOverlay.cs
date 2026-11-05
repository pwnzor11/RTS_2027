// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Ui.Sidebar;

/// <summary>M7-14: the faint tint over an Expedition player's beachhead zone while a building is being placed. Pure bit lookup, unit-tested.</summary>
public static class BeachheadOverlay
{
    /// <summary>Tint colour of the zone.</summary>
    public static readonly Color Tint = new(90, 190, 255);

    /// <summary>Tint opacity 0..255 (faint).</summary>
    public const byte TintAlpha = 36;

    /// <summary>True if cell (<paramref name="x"/>, <paramref name="y"/>) is set in the mask (bit <c>cell % 64</c> of word <c>cell / 64</c>,
    /// cell = y * width + x). Cells outside the mask or the map are false.</summary>
    public static bool Contains(ReadOnlySpan<ulong> mask, int width, int x, int y)
    {
        if (x < 0 || y < 0 || x >= width) return false;
        int cell = y * width + x, word = cell >> 6;
        return word < mask.Length && (mask[word] >> (cell & 63) & 1UL) != 0;
    }
}
