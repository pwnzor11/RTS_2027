// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Map;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Pathing;

/// <summary>
/// M7-03: connected-region labels of the terrain per movement class (<see cref="MoveClass"/>). Terrain only: units and buildings are
/// ignored, so a label says "water connects these cells", never "the way is free right now" (and it leaks nothing hidden by fog).
/// Labels are 1..65535 in the order the flood fill meets them scanning cells by index (4-connected: the path search does not cut
/// blocked corners, so this is exactly 8-direction reachability), 0 = the class cannot enter the cell. A pure function of the land
/// types, so it is a cache: only the naval part's copy is saved, and everything is recomputed when <see cref="MapState.Version"/>
/// or the load epoch changes.
/// </summary>
public sealed class RegionCache
{
    private const int Slots = 8;   // Locomotion 0..6 and NavalDeep
    private readonly MapState _map;
    private readonly TerrainTable _terrain;
    private readonly int[] _version = new int[Slots];
    private readonly int[] _epoch = new int[Slots];
    private readonly ushort[]?[] _labels = new ushort[Slots][];
    private int[]? _queue;

    /// <summary>Creates an empty cache for the map and the terrain table.</summary>
    public RegionCache(MapState map, TerrainTable terrain)
    {
        _map = map; _terrain = terrain;
        Array.Fill(_version, -1);
    }

    private static int Slot(Locomotion c) => c == MoveClass.NavalDeep ? 7 : (int)c;

    /// <summary>
    /// The labels for a movement class, recomputed first if the land changed since last time. <paramref name="epoch"/> changes when a
    /// save is loaded. <paramref name="storage"/> (optional, map-sized) is used as the label array instead of an own one.
    /// </summary>
    public ushort[] Get(Locomotion c, int epoch, ushort[]? storage = null)
    {
        int s = Slot(c);
        ushort[] dest = _labels[s] ??= storage ?? new ushort[_map.Width * _map.Height];
        if (_version[s] == _map.Version && _epoch[s] == epoch) return dest;
        Flood(c, dest);
        _version[s] = _map.Version; _epoch[s] = epoch;
        return dest;
    }

    private void Flood(Locomotion c, ushort[] label)
    {
        int w = _map.Width, h = _map.Height, n = w * h;
        Array.Clear(label);
        _queue ??= new int[n];
        int[] q = _queue;
        int next = 0;
        for (int start = 0; start < n; start++)
        {
            if (label[start] != 0 || !Enter(start % w, start / w, c)) continue;
            if (next < ushort.MaxValue) next++;
            ushort id = (ushort)next;
            int head = 0, tail = 0;
            q[tail++] = start; label[start] = id;
            while (head < tail)
            {
                int cur = q[head++];
                int x = cur % w, y = cur / w;
                if (y > 0 && label[cur - w] == 0 && Enter(x, y - 1, c)) { label[cur - w] = id; q[tail++] = cur - w; }
                if (x < w - 1 && label[cur + 1] == 0 && Enter(x + 1, y, c)) { label[cur + 1] = id; q[tail++] = cur + 1; }
                if (y < h - 1 && label[cur + w] == 0 && Enter(x, y + 1, c)) { label[cur + w] = id; q[tail++] = cur + w; }
                if (x > 0 && label[cur - 1] == 0 && Enter(x - 1, y, c)) { label[cur - 1] = id; q[tail++] = cur - 1; }
            }
        }
    }

    private bool Enter(int x, int y, Locomotion c) => _map.SpeedPercent(new CellCoord(x, y), c, _terrain) > 0;
}
