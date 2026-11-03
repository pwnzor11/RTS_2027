// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>Whether a draw item is a building or a unit.</summary>
public enum DrawKind : byte
{
    /// <summary>A building, sorted by its footprint's bottom edge.</summary>
    Building = 0,

    /// <summary>A unit, sorted by its ground position.</summary>
    Unit = 1,
}

/// <summary>
/// One sprite in the y-sorted pass. For a building the sort key is its footprint's bottom edge and left edge, so the
/// row it sits on is the bottom row of the footprint. For a unit it is the ground position. Units and buildings
/// share one pass, so a tall building overlaps the units north of it and is overlapped by the units south of it.
/// </summary>
/// <param name="Kind">Building or unit.</param>
/// <param name="Index">Index in the snapshot's building or unit list.</param>
/// <param name="SortY">Bottom edge (building) or ground Y (unit), zoom-1 world pixels.</param>
/// <param name="SortX">Left edge (building) or ground X (unit), zoom-1 world pixels.</param>
public readonly record struct DrawItem(DrawKind Kind, int Index, float SortY, float SortX);

/// <summary>
/// Draw order: by <see cref="DrawItem.SortY"/>, then <see cref="DrawItem.SortX"/>. On a tie a building comes before a
/// unit, so a unit standing exactly on a building's bottom edge is drawn over it. Remaining ties fall back to kind and
/// index, which keeps the order total and the output stable.
/// </summary>
public sealed class DrawOrder : IComparer<DrawItem>
{
    /// <summary>The shared instance.</summary>
    public static readonly DrawOrder Instance = new();

    /// <inheritdoc />
    public int Compare(DrawItem a, DrawItem b)
    {
        int c = a.SortY.CompareTo(b.SortY);
        if (c != 0) return c;
        c = a.SortX.CompareTo(b.SortX);
        if (c != 0) return c;
        c = a.Kind.CompareTo(b.Kind);
        if (c != 0) return c;
        return a.Index.CompareTo(b.Index);
    }
}
