// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// A43-QOL-14: the cells of a drag-line placement (walls and defences). Shift + left-drag while placing lays a row of the same structure;
/// each cell is then an ordinary Place command, validated by the sim like a single click. Pure maths: nothing here asks the sim.
/// </summary>
public static class PlacementLine
{
    /// <summary>The most structures one line places (a sanity cap; the queue and the credits limit it first).</summary>
    public const int MaxCells = 24;

    /// <summary>
    /// Top-left cells of a row from <paramref name="start"/> toward <paramref name="end"/> along the longer axis, one footprint apart, so
    /// neighbouring structures touch without overlapping. Always holds at least <paramref name="start"/>.
    /// </summary>
    public static List<CellCoord> TopLefts(CellCoord start, CellCoord end, int footprintW, int footprintH)
    {
        int w = Math.Max(1, footprintW), h = Math.Max(1, footprintH);
        int dx = end.X - start.X, dy = end.Y - start.Y;
        var list = new List<CellCoord>();
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            int n = Math.Min(MaxCells, Math.Abs(dx) / w + 1), step = Math.Sign(dx) * w;
            for (int i = 0; i < n; i++) list.Add(new CellCoord(start.X + i * step, start.Y));
        }
        else
        {
            int n = Math.Min(MaxCells, Math.Abs(dy) / h + 1), step = Math.Sign(dy) * h;
            for (int i = 0; i < n; i++) list.Add(new CellCoord(start.X, start.Y + i * step));
        }

        return list;
    }

    /// <summary>
    /// How many structures of the type the player can place: the finished one waiting (at most one per lane) plus those already queued. The
    /// queued ones are paid for as they build, so this is the "affordable and queued" cap; the rest of the line is dropped.
    /// </summary>
    public static int Allowed(bool readyOfType, int queuedOfType) => (readyOfType ? 1 : 0) + Math.Max(0, queuedOfType);

    /// <summary>The first <paramref name="allowed"/> cells of a line (never fewer than none).</summary>
    public static List<CellCoord> Cap(List<CellCoord> cells, int allowed) =>
        cells.Count <= allowed ? cells : cells.GetRange(0, Math.Max(0, allowed));
}
