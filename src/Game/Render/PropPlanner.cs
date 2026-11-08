// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// Decides where the land-linked sprites go (M12-02): boulders (or glass crests) on clusters of Rock cells, rubble on clusters of Rough
/// cells, ice slabs on water cells that touch land. Nothing is stored: the plan is a pure function of the map, the theater's
/// <see cref="DecorTable"/> and the map hash, built once per map. Render only; the sim never reads it and nothing here changes a hash.
/// </summary>
public static class PropPlanner
{
    /// <summary>Props keep this many cells (Chebyshev) away from a start position so a base is never cluttered.</summary>
    public const int StartKeepOut = 4;

    /// <summary>Plans the props of a map, sorted by (row, column) so a renderer can cull by row range.</summary>
    /// <param name="map">The map.</param>
    /// <param name="palette">Gives the sprite rules.</param>
    /// <param name="seed">The map hash.</param>
    public static PropInstance[] Plan(MapData map, TerrainPalette palette, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(palette);
        DecorTable table = palette.Decor;
        var list = new List<PropInstance>();
        if (table.Rock is null && table.Rough is null && table.Bank is null) return Array.Empty<PropInstance>();

        int w = map.Width, h = map.Height;
        bool[] keepOut = new bool[w * h];
        foreach (CellCoord start in map.StartPositions)
        {
            for (int y = Math.Max(0, start.Y - StartKeepOut); y <= Math.Min(h - 1, start.Y + StartKeepOut); y++)
                for (int x = Math.Max(0, start.X - StartKeepOut); x <= Math.Min(w - 1, start.X + StartKeepOut); x++) keepOut[y * w + x] = true;
        }

        if (table.Rock is { } rock) Clusters(map, LandType.Rock, rock, 14, keepOut, seed, list);
        if (table.Rough is { } rough) Clusters(map, LandType.Rough, rough, 12, keepOut, seed, list);
        if (table.Bank is { } bank) Banks(map, bank, keepOut, seed, list);

        list.Sort(static (a, b) => a.CellY != b.CellY ? a.CellY.CompareTo(b.CellY) : a.CellX != b.CellX ? a.CellX.CompareTo(b.CellX) : a.Kind.CompareTo(b.Kind));
        return list.ToArray();
    }

    /// <summary>
    /// Index of the first prop whose row is at least <paramref name="minRow"/> in a plan from <see cref="Plan"/> (props are sorted by row), so a
    /// renderer walks only the rows on screen.
    /// </summary>
    public static int FirstAtRow(IReadOnlyList<PropInstance> props, int minRow)
    {
        ArgumentNullException.ThrowIfNull(props);
        int lo = 0, hi = props.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (props[mid].CellY < minRow) lo = mid + 1;
            else hi = mid;
        }

        return lo;
    }

    private static void Clusters(MapData map, LandType land, SpriteRule rule, int spread, bool[] keepOut, ulong seed, List<PropInstance> output)
    {
        int w = map.Width, h = map.Height;
        var seen = new bool[w * h];
        var cells = new List<int>();
        var stack = new Stack<int>();
        for (int start = 0; start < w * h; start++)
        {
            if (seen[start] || map.Land[start] != land) continue;
            cells.Clear();
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int k = stack.Pop();
                cells.Add(k);
                int x = k % w, y = k / w;
                if (x > 0) Visit(k - 1);
                if (x < w - 1) Visit(k + 1);
                if (y > 0) Visit(k - w);
                if (y < h - 1) Visit(k + w);
            }

            cells.Sort();
            int placed = 0, bestCell = -1;
            uint bestRoll = uint.MaxValue;
            foreach (int k in cells)
            {
                if (keepOut[k] || map.Overlay[k] != OverlayKind.None) continue;
                int x = k % w, y = k / w;
                uint roll = DecorHash.Mix(seed, x, y, 20) % 1000u;
                if (roll < bestRoll) { bestRoll = roll; bestCell = k; }
                if (roll < (uint)rule.Permille) { Add(k, cells.Count, rule.Kind, spread, seed, output); placed++; }
            }

            if (placed == 0 && bestCell >= 0) Add(bestCell, cells.Count, rule.Kind, spread, seed, output);
        }

        void Visit(int k)
        {
            if (seen[k] || map.Land[k] != land) return;
            seen[k] = true;
            stack.Push(k);
        }

        void Add(int k, int clusterSize, PropKind kind, int range, ulong sd, List<PropInstance> sink)
        {
            int x = k % w, y = k / w;
            int v = (int)(DecorHash.Mix(sd, x, y, 21) % 3u);
            if (clusterSize < 3) v = 0;
            else if (clusterSize < 6 && v == 2) v = 1;
            sink.Add(Make(kind, v, x, y, range, sd));
        }
    }

    private static void Banks(MapData map, SpriteRule rule, bool[] keepOut, ulong seed, List<PropInstance> output)
    {
        int w = map.Width, h = map.Height;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int k = y * w + x;
                if (map.Land[k] != LandType.Water || keepOut[k] || map.Overlay[k] != OverlayKind.None) continue;
                bool shore = IsLand(map, x - 1, y) || IsLand(map, x + 1, y) || IsLand(map, x, y - 1) || IsLand(map, x, y + 1);
                if (!shore || DecorHash.Mix(seed, x, y, 22) % 1000u >= (uint)rule.Permille) continue;
                output.Add(Make(rule.Kind, (int)(DecorHash.Mix(seed, x, y, 21) % 3u), x, y, 10, seed));
            }
        }
    }

    private static bool IsLand(MapData map, int x, int y)
    {
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height) return false;
        LandType l = map.Land[y * map.Width + x];
        return l is not (LandType.Water or LandType.Shallow);
    }

    private static PropInstance Make(PropKind kind, int variant, int x, int y, int range, ulong seed)
    {
        int ox = (int)(DecorHash.Mix(seed, x, y, 23) % (uint)(2 * range + 1)) - range;
        int oy = (int)(DecorHash.Mix(seed, x, y, 24) % (uint)(2 * range + 1)) - range;
        return new PropInstance(kind, (byte)variant, (short)x, (short)y, (sbyte)ox, (sbyte)oy);
    }
}
