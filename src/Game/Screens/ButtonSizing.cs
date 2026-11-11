// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>Sizes a text button from its label, so a larger text size never leaves the label wider than its box.</summary>
public static class ButtonSizing
{
    /// <summary>The width of a button: at least <paramref name="minUnits"/> font pixels wide, and always the label plus 3 units of padding each side.</summary>
    public static int WidthFor(string label, UiUnit scale, int minUnits) =>
        Math.Max(minUnits * scale, BitmapFont.Measure(label, scale) + 6 * scale);
}
