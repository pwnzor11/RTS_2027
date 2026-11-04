// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;

namespace Rts.Game.Screens.Editor;

/// <summary>Pure brush maths: which cells a square brush covers, and the symmetric images of them.</summary>
public static class BrushShape
{
    /// <summary>The brush sizes the editor offers.</summary>
    public static readonly int[] Sizes = [1, 3, 5];

    /// <summary>True when <paramref name="s"/> can be applied to a <paramref name="w"/>x<paramref name="h"/> map (Rot4 and MirrorDiag need a square map).</summary>
    public static bool SymmetryFits(MapSymmetry s, int w, int h) => w == h || s is not (MapSymmetry.Rot4 or MapSymmetry.MirrorDiag);

    /// <summary>Cells of a <paramref name="size"/>x<paramref name="size"/> square centred on (cx, cy) (even sizes lean up-left), clipped to the map, row by row.</summary>
    public static List<(int X, int Y)> Square(int cx, int cy, int size, int w, int h)
    {
        var cells = new List<(int, int)>();
        int lo = -((size - 1) / 2), hi = size / 2;
        for (int dy = lo; dy <= hi; dy++)
            for (int dx = lo; dx <= hi; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x >= 0 && y >= 0 && x < w && y < h) cells.Add((x, y));
            }
        return cells;
    }

    /// <summary>The distinct images of one cell under a symmetry, the cell itself first. An unfit symmetry counts as None.</summary>
    public static List<(int X, int Y)> Images(MapSymmetry s, int w, int h, int x, int y)
    {
        if (!SymmetryFits(s, w, h)) s = MapSymmetry.None;
        var list = new List<(int, int)>(4);
        foreach (var p in MapSymmetries.Images(s, w, h, x, y))
            if (!list.Contains(p)) list.Add(p);
        return list;
    }

    /// <summary>Every cell a stroke at (cx, cy) touches: the brush square and all its symmetric images, each cell once, in discovery order.</summary>
    public static List<(int X, int Y)> Stroke(MapSymmetry s, int w, int h, int cx, int cy, int size)
    {
        var seen = new bool[w * h];
        var result = new List<(int, int)>();
        foreach (var (x, y) in Square(cx, cy, size, w, h))
            foreach (var (ix, iy) in Images(s, w, h, x, y))
                if (!seen[iy * w + ix]) { seen[iy * w + ix] = true; result.Add((ix, iy)); }
        return result;
    }

    /// <summary>Cells on the straight line from a to b (Bresenham), both ends included. Used to fill the gaps of a fast drag.</summary>
    public static List<(int X, int Y)> Line(int x0, int y0, int x1, int y1)
    {
        var cells = new List<(int, int)>();
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        while (true)
        {
            cells.Add((x0, y0));
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
        return cells;
    }
}
