// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Ui;

/// <summary>Pure layout helper for the stack of text panels in the left column of the map view (audit S3).</summary>
public static class HudStack
{
    /// <summary>
    /// The y a new panel must start at so it sits below every non-empty rectangle in <paramref name="panels"/>, one
    /// <paramref name="gap"/> under the lowest. With no non-empty panel it returns <paramref name="top"/>.
    /// </summary>
    public static int NextTop(int top, int gap, IEnumerable<Rectangle> panels)
    {
        int y = top;
        foreach (Rectangle r in panels)
        {
            if (r.Width <= 0 || r.Height <= 0) continue;
            y = Math.Max(y, r.Bottom + gap);
        }

        return y;
    }
}
