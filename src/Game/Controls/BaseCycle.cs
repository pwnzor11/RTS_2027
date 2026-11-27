// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;

namespace Rts.Game.Controls;

/// <summary>
/// S47 U-13 (cnc#2): the Home key steps through your bases. The first press goes to the first base; pressing again while the view still sits on
/// the base you jumped to goes to the next one (wrapping); with one base nothing changes. Pure: the screen supplies the base points.
/// </summary>
public static class BaseCycle
{
    /// <summary>How close (in cells) the view centre has to be to the last base for the next press to move on.</summary>
    public const int StayCells = 4;

    /// <summary>
    /// The index of the base to jump to, or -1 when there are none. <paramref name="last"/> is the index jumped to last (-1 = none yet),
    /// <paramref name="viewCentre"/> the world point (zoom-1 pixels) at the middle of the view now.
    /// </summary>
    public static int Next(IReadOnlyList<Vector2> bases, Vector2 viewCentre, int last)
    {
        if (bases.Count == 0) return -1;
        if (last < 0 || last >= bases.Count) return 0;
        bool stillThere = Vector2.Distance(viewCentre, bases[last]) <= StayCells * Camera.CellPx;
        return stillThere ? (last + 1) % bases.Count : 0;
    }
}
