// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim;

/// <summary>
/// M16 M-28 (D-314): deterministic uniform-grid bucket index over the slots of ONE mobile store (vehicles or infantry). Derived data:
/// never saved, never hashed, rebuilt from the positions it is given. Buckets are 8x8 cells. Each bucket lists its slots in ascending
/// slot order (counting-sort fill walks slots ascending) and <see cref="Query"/> returns the candidates of the buckets a square touches
/// merged into ONE ascending slot list, so a caller that used to walk every slot in order sees the same candidates in the same order,
/// minus slots that are provably outside the square. No hashing, no float, no allocation after construction.
/// </summary>
internal sealed class SpatialIndex
{
    /// <summary>log2 of the bucket edge in leptons: 8 cells x 256 leptons = 2048.</summary>
    internal const int BucketShift = 11;

    private readonly int _capacity;
    private int _bw = 1, _bh = 1;
    private int[] _start = new int[2];      // bucket -> first entry in _items (length buckets + 1)
    private int[] _items;                   // slot indices grouped by bucket, ascending inside a bucket
    private readonly int[] _bucketOf;       // slot -> bucket at build time (scratch for the fill pass)
    private readonly int[] _out;            // query result buffer (reused)
    private int _count;

    /// <summary>Slot-allocation serial of the store when the index was built; the owner compares it to detect later spawns.</summary>
    internal int BuiltSerial;

    public SpatialIndex(int slotCapacity)
    {
        _capacity = slotCapacity;
        _items = new int[slotCapacity];
        _bucketOf = new int[slotCapacity];
        _out = new int[slotCapacity];
    }

    /// <summary>Number of slots indexed by the last <see cref="Build"/>.</summary>
    internal int Count => _count;

    /// <summary>Index every live slot of <paramref name="slots"/> at <paramref name="pos"/>[slot]. Positions outside the map are clamped to the edge buckets.</summary>
    internal void Build(SlotArray slots, LeptonCoord[] pos, int mapWidthCells, int mapHeightCells)
    {
        int bw = Math.Max(1, ((mapWidthCells + 7) >> 3)), bh = Math.Max(1, ((mapHeightCells + 7) >> 3));
        int buckets = bw * bh;
        if (_start.Length < buckets + 1) _start = new int[buckets + 1];
        _bw = bw; _bh = bh;
        Array.Clear(_start, 0, buckets + 1);
        int n = 0;
        int cap = Math.Min(_capacity, slots.Capacity);
        for (int i = 1; i < cap; i++)
        {
            if (!slots.IsIndexAlive(i)) { _bucketOf[i] = -1; continue; }
            int b = BucketOf(pos[i]);
            _bucketOf[i] = b;
            _start[b + 1]++;
            n++;
        }
        for (int b = 0; b < buckets; b++) _start[b + 1] += _start[b];
        // Fill: _start[b] doubles as the write cursor, then is restored by shifting back.
        for (int i = 1; i < cap; i++)
        {
            int b = _bucketOf[i];
            if (b < 0) continue;
            _items[_start[b]++] = i;
        }
        for (int b = buckets; b > 0; b--) _start[b] = _start[b - 1];
        _start[0] = 0;
        _count = n;
    }

    private int BucketOf(LeptonCoord p) => Clamp(p.Y >> BucketShift, _bh) * _bw + Clamp(p.X >> BucketShift, _bw);

    private static int Clamp(int v, int n) => v < 0 ? 0 : (v >= n ? n - 1 : v);

    /// <summary>
    /// Every indexed slot whose bucket touches the square [from - radius, from + radius], in ascending slot order, as a span over a reused
    /// buffer (valid until the next call). Includes every slot whose indexed position lies within <paramref name="radius"/> of
    /// <paramref name="from"/> (Euclidean or Chebyshev); callers still apply their exact tests.
    /// </summary>
    internal ReadOnlySpan<int> Query(LeptonCoord from, int radius)
    {
        if (radius < 0) radius = 0;
        long x0 = ((long)from.X - radius) >> BucketShift, x1 = ((long)from.X + radius) >> BucketShift;
        long y0 = ((long)from.Y - radius) >> BucketShift, y1 = ((long)from.Y + radius) >> BucketShift;
        int bx0 = (int)Math.Clamp(x0, 0, _bw - 1), bx1 = (int)Math.Clamp(x1, 0, _bw - 1);
        int by0 = (int)Math.Clamp(y0, 0, _bh - 1), by1 = (int)Math.Clamp(y1, 0, _bh - 1);
        int n = 0;
        for (int by = by0; by <= by1; by++)
        {
            int row = by * _bw;
            // Buckets of one row are contiguous in the bucket numbering, so their item ranges are contiguous too.
            int from0 = _start[row + bx0], to0 = _start[row + bx1 + 1];
            for (int k = from0; k < to0; k++) _out[n++] = _items[k];
        }
        Span<int> res = _out.AsSpan(0, n);
        res.Sort();   // slot indices are distinct ints: the order is total, so the result is deterministic
        return res;
    }
}
