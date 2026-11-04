// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>Cell transforms of a <see cref="MapSymmetry"/> (M4). Pure integer maths; shared by the generator, the validator and the
/// editor's symmetry helper.</summary>
public static class MapSymmetries
{
    /// <summary>Number of images a symmetry gives each cell (its group order): None 1, Rot2 2, Rot4 4, mirrors 2.</summary>
    public static int Order(MapSymmetry s) => s switch
    {
        MapSymmetry.None => 1,
        MapSymmetry.Rot4 => 4,
        MapSymmetry.Rot2 or MapSymmetry.MirrorX or MapSymmetry.MirrorY or MapSymmetry.MirrorDiag => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(s)),
    };

    /// <summary>
    /// The images of cell (<paramref name="x"/>, <paramref name="y"/>) on a <paramref name="w"/>×<paramref name="h"/> map, the cell
    /// itself first; always <see cref="Order"/> entries, so a cell on an axis or at the centre appears more than once.
    /// Rot2: (w-1-x, h-1-y). Rot4: quarter turns clockwise (y down): (w-1-y, x), (w-1-x, h-1-y), (y, h-1-x). MirrorX: across the
    /// vertical centre line, (w-1-x, y). MirrorY: across the horizontal centre line, (x, h-1-y). MirrorDiag: across the main
    /// diagonal, (y, x). Throws for a cell off the map, and for Rot4 / MirrorDiag on a non-square map.
    /// </summary>
    public static (int X, int Y)[] Images(MapSymmetry s, int w, int h, int x, int y)
    {
        if (w < 1 || h < 1) throw new ArgumentOutOfRangeException(nameof(w), "map size must be positive");
        if (x < 0 || y < 0 || x >= w || y >= h) throw new ArgumentOutOfRangeException(nameof(x), "cell is off the map");
        if ((s is MapSymmetry.Rot4 or MapSymmetry.MirrorDiag) && w != h)
            throw new ArgumentException($"{s} needs a square map, got {w}x{h}", nameof(s));
        return s switch
        {
            MapSymmetry.None => new[] { (x, y) },
            MapSymmetry.Rot2 => new[] { (x, y), (w - 1 - x, h - 1 - y) },
            MapSymmetry.Rot4 => new[] { (x, y), (w - 1 - y, x), (w - 1 - x, h - 1 - y), (y, h - 1 - x) },
            MapSymmetry.MirrorX => new[] { (x, y), (w - 1 - x, y) },
            MapSymmetry.MirrorY => new[] { (x, y), (x, h - 1 - y) },
            MapSymmetry.MirrorDiag => new[] { (x, y), (y, x) },
            _ => throw new ArgumentOutOfRangeException(nameof(s)),
        };
    }
}
