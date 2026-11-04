// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Economy;

/// <summary>
/// M4-16: the order in which ashfall regrowth visits the cells of a map. Regrowth used to sweep in plain row-major order, so a field
/// near the top-left start grew up to a whole interval before its mirror near the opposite start (start-0 bias). Here every cell is
/// keyed by the smallest index among its images under the map's dihedral symmetries (rotation by 180 degrees, both mirrors, and on
/// square maps the quarter turns and both diagonals), and cells with the same key are always visited on the same tick. Any symmetric
/// map (rot2, rot4, mirror x/y/diag) therefore regrows each field and its mirror images together. Derived from the map size only:
/// not sim state, never saved or hashed. Integer maths, no unordered collections.
/// </summary>
internal sealed class RegrowthSchedule
{
    /// <summary>Cell indices sorted by (orbit key, index).</summary>
    private readonly int[] _order;
    /// <summary>_start[k] = first position in <see cref="_order"/> whose key is &gt;= k (length cells + 1).</summary>
    private readonly int[] _start;

    /// <summary>Number of cells (width × height).</summary>
    public int Cells { get; }

    public RegrowthSchedule(int width, int height)
    {
        int n = width * height;
        Cells = n;
        var key = new int[n];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                key[y * width + x] = OrbitKey(x, y, width, height);
        // counting sort by key (keys are cell indices 0..n-1), stable in cell index
        _start = new int[n + 1];
        for (int i = 0; i < n; i++) _start[key[i] + 1]++;
        for (int k = 0; k < n; k++) _start[k + 1] += _start[k];
        _order = new int[n];
        var fill = new int[n];
        for (int i = 0; i < n; i++) _order[_start[key[i]] + fill[key[i]]++] = i;
    }

    /// <summary>Smallest row-major index among the images of (x, y) under the symmetries the map shape allows.</summary>
    internal static int OrbitKey(int x, int y, int w, int h)
    {
        int mx = w - 1 - x, my = h - 1 - y;
        int best = y * w + x;
        best = Math.Min(best, my * w + mx);   // rot2
        best = Math.Min(best, y * w + mx);    // mirror x
        best = Math.Min(best, my * w + x);    // mirror y
        if (w == h)
        {
            best = Math.Min(best, x * w + y);     // diagonal
            best = Math.Min(best, mx * w + my);   // anti-diagonal
            best = Math.Min(best, x * w + my);    // rot90 image (w-1-y, x) at index x*w + (w-1-y)
            best = Math.Min(best, mx * w + y);    // rot270 image (y, w-1-x) at index (w-1-x)*w + y
        }
        return best;
    }

    /// <summary>The cells to visit on <paramref name="phase"/> of <paramref name="interval"/>: every cell whose orbit key falls in
    /// this phase's share of the key range. Over one interval every cell is visited exactly once.</summary>
    public ReadOnlySpan<int> Slice(int phase, int interval)
    {
        int from = (int)((long)phase * Cells / interval), to = (int)((long)(phase + 1) * Cells / interval);
        int a = _start[from], b = _start[to];
        return _order.AsSpan(a, b - a);
    }
}
