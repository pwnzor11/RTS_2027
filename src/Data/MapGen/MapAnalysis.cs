// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

/// <summary>
/// Map measurements shared by the generator, <see cref="MapValidator"/> and tests (design 08 §1): passability (the real
/// <see cref="TerrainTable"/> or the built-in conservative rule), 4-way BFS path lengths, resource fields and path equality.
/// Integer maths, cell-index order, no hashing containers.
/// </summary>
public static class MapAnalysis
{
    /// <summary>Distance value for an unreachable cell (and spread value when a target is unreachable).</summary>
    public const int Unreachable = int.MaxValue;

    /// <summary>Built-in rule (no rules data): Rock and Cliff block every ground locomotion; deep Water passes only Hover;
    /// Shallow passes Foot and Hover; Air crosses everything.</summary>
    public static bool BuiltInPassable(LandType land, Locomotion loco)
    {
        if (loco == Locomotion.Air) return true;
        if (loco == Locomotion.Naval) return land is LandType.Water or LandType.Shallow;   // M7-13: ships never touch land
        return land switch
        {
            LandType.Rock or LandType.Cliff => false,
            LandType.Water => loco == Locomotion.Hover,
            LandType.Shallow => loco is Locomotion.Foot or Locomotion.Hover,
            _ => true,
        };
    }

    /// <summary>Passability through <paramref name="table"/> (speed &gt; 0), or the built-in rule when it is null.</summary>
    public static bool Passable(LandType land, Locomotion loco, TerrainTable? table) =>
        table is not null ? table.SpeedPercent(land, loco) > 0 : BuiltInPassable(land, loco);

    /// <summary>Buildability through <paramref name="table"/>, or the built-in rule (Clear, Road, Ash) when it is null.</summary>
    public static bool Buildable(LandType land, TerrainTable? table) =>
        table is not null ? table.Buildable(land) : land is LandType.Clear or LandType.Road or LandType.Ash;

    /// <summary>Footprint of a map feature type. Known neutral types (design 08 §3): neu_ruin_tenement 3×2, neu_ruin_depot 2×2;
    /// any other type is treated as 1×1 (the map layer has no building rules).</summary>
    public static (int W, int H) FootprintOf(string type) => type switch
    {
        "neu_ruin_tenement" => (3, 2),
        "neu_ruin_depot" => (2, 2),
        _ => (1, 1),
    };

    /// <summary>Cells covered by feature footprints (clipped to the map); null when there are none.</summary>
    public static bool[]? FeatureMask(MapData map, IReadOnlyList<MapFeature>? features)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (features is null || features.Count == 0) return null;
        var mask = new bool[map.Width * map.Height];
        foreach (MapFeature f in features)
        {
            (int w, int h) = FootprintOf(f.Type);
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    var c = new CellCoord(f.Cell.X + dx, f.Cell.Y + dy);
                    if (map.Contains(c)) mask[map.IndexOf(c)] = true;
                }
        }
        return mask;
    }

    /// <summary>4-way BFS path lengths from <paramref name="from"/> for <paramref name="loco"/>; <see cref="Unreachable"/> elsewhere.
    /// <paramref name="blocked"/> cells (features) are impassable.</summary>
    public static int[] Distances(MapData map, CellCoord from, Locomotion loco, TerrainTable? table, bool[]? blocked = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        return Distances(map, new[] { map.IndexOf(from) }, loco, table, blocked);
    }

    /// <summary>Multi-source 4-way BFS (distance 0 at every source).</summary>
    public static int[] Distances(MapData map, IReadOnlyList<int> sources, Locomotion loco, TerrainTable? table, bool[]? blocked = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(sources);
        int w = map.Width, h = map.Height;
        var dist = new int[w * h];
        Array.Fill(dist, Unreachable);
        var queue = new int[w * h];
        int head = 0, tail = 0;
        foreach (int s in sources)
        {
            if (dist[s] == 0) continue;
            dist[s] = 0; queue[tail++] = s;
        }
        while (head < tail)
        {
            int i = queue[head++];
            int x = i % w, y = i / w, nd = dist[i] + 1;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int j = ny * w + nx;
                if (dist[j] != Unreachable || (blocked is not null && blocked[j]) || !Passable(map.Land[j], loco, table)) continue;
                dist[j] = nd; queue[tail++] = j;
            }
        }
        return dist;
    }

    /// <summary>Resource field id per cell (8-connected components of Ashfall or Pitchblende cells, numbered in cell-index order),
    /// −1 where there is no resource. <paramref name="count"/> is the number of fields.</summary>
    public static int[] FieldIds(MapData map, out int count)
    {
        ArgumentNullException.ThrowIfNull(map);
        int w = map.Width, h = map.Height, n = w * h;
        var id = new int[n];
        Array.Fill(id, -1);
        var stack = new Stack<int>();
        count = 0;
        for (int s = 0; s < n; s++)
        {
            if (id[s] >= 0 || !IsResource(map.Overlay[s])) continue;
            id[s] = count; stack.Push(s);
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % w, y = i / w;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (id[j] >= 0 || !IsResource(map.Overlay[j])) continue;
                        id[j] = count; stack.Push(j);
                    }
            }
            count++;
        }
        return id;
    }

    /// <summary>Number of distinct fields with a cell at path distance ≤ <paramref name="maxDistance"/>.</summary>
    public static int FieldsWithin(int[] dist, int[] fieldIds, int fieldCount, int maxDistance)
    {
        ArgumentNullException.ThrowIfNull(dist);
        ArgumentNullException.ThrowIfNull(fieldIds);
        var seen = new bool[fieldCount];
        int c = 0;
        for (int i = 0; i < dist.Length; i++)
        {
            int f = fieldIds[i];
            if (f < 0 || seen[f] || dist[i] > maxDistance) continue;
            seen[f] = true; c++;
        }
        return c;
    }

    /// <summary>Smallest path distance to any resource cell, or <see cref="Unreachable"/>.</summary>
    public static int NearestField(int[] dist, int[] fieldIds)
    {
        ArgumentNullException.ThrowIfNull(dist);
        ArgumentNullException.ThrowIfNull(fieldIds);
        int best = Unreachable;
        for (int i = 0; i < dist.Length; i++)
            if (fieldIds[i] >= 0 && dist[i] < best) best = dist[i];
        return best;
    }

    /// <summary>Chebyshev radius around the middle cells that counts as "the map centre" for path equality.</summary>
    public const int CentreRadius = 2;

    /// <summary>The map centre as cell indices: the middle 1, 2 or 4 cells grown by <see cref="CentreRadius"/> (invariant under every
    /// symmetry), so a river or rock exactly at the middle does not make the centre unreachable.</summary>
    public static int[] CentreCells(MapData map)
    {
        ArgumentNullException.ThrowIfNull(map);
        int x0 = Math.Max(0, (map.Width - 1) / 2 - CentreRadius), x1 = Math.Min(map.Width - 1, map.Width / 2 + CentreRadius);
        int y0 = Math.Max(0, (map.Height - 1) / 2 - CentreRadius), y1 = Math.Min(map.Height - 1, map.Height / 2 + CentreRadius);
        var cells = new List<int>();
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++) cells.Add(y * map.Width + x);
        return cells.ToArray();
    }

    /// <summary>Path-equality measures (Tracked BFS) across <paramref name="starts"/> (design 08 §1 check 3).</summary>
    public static PathSpread Spread(MapData map, IReadOnlyList<CellCoord> starts, TerrainTable? table, bool[]? blocked = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(starts);
        int[] fields = FieldIds(map, out _);
        int[] centre = CentreCells(map);
        int k = starts.Count;
        var nearest = new int[k];
        var toCentre = new int[k];
        var others = new int[k][];
        for (int s = 0; s < k; s++)
        {
            int[] d = Distances(map, starts[s], Locomotion.Tracked, table, blocked);
            nearest[s] = NearestField(d, fields);
            int c = Unreachable;
            foreach (int ci in centre) if (d[ci] < c) c = d[ci];
            toCentre[s] = c;
            var o = new int[k - 1];
            for (int t = 0, m = 0; t < k; t++) if (t != s) o[m++] = d[map.IndexOf(starts[t])];
            Array.Sort(o);
            others[s] = o;
        }
        int otherSpread = 0;
        for (int e = 0; e < k - 1; e++)
        {
            int lo = Unreachable, hi = 0;
            for (int s = 0; s < k; s++) { lo = Math.Min(lo, others[s][e]); hi = Math.Max(hi, others[s][e]); }
            otherSpread = Math.Max(otherSpread, Diff(lo, hi));
        }
        return new PathSpread(Range(nearest), Range(toCentre), otherSpread);
    }

    private static int Range(int[] v)
    {
        if (v.Length == 0) return 0;
        int lo = v.Min(), hi = v.Max();
        return Diff(lo, hi);
    }

    private static int Diff(int lo, int hi) => hi == Unreachable ? (lo == Unreachable ? 0 : Unreachable) : hi - lo;

    private static bool IsResource(OverlayKind o) => o is OverlayKind.Ashfall or OverlayKind.Pitchblende;
}
