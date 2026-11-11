// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

/// <summary>
/// The opt-in ocean edge of the generator (M7-13, <see cref="MapGenParams.OceanEdges"/>). <see cref="Paint"/> floods the chosen edges
/// (deep Water with a smoothed wobble, then a two-cell Shallow band), <see cref="PaintCoast"/> turns the land two cells inland of that
/// sea into <see cref="LandType.Coast"/>, and <see cref="Markers"/> reports the <c>convoy_edge</c> cells and one landing beach per
/// start. All writes go through <see cref="GenGrid"/>, so symmetry holds by construction; integer maths and the builder's
/// <see cref="SimRandom"/> only. Never constructed when the option is off, so no random numbers are drawn then.
/// </summary>
internal sealed class OceanEdgePass
{
    private const int Wobble = 2, ShallowWidth = 2, CoastWidth = 2;
    private const int ConvoyStride = 8;

    private readonly GenGrid _g;
    private readonly SimRandom _rng;
    private readonly OceanEdges _edges;
    private readonly int _ow;
    private readonly bool[] _strip;   // cells within the edge band (water, shallow and coast), on every image
    private bool[] _sea = Array.Empty<bool>();   // the edge sea: Water/Shallow cells 4-connected to a border cell inside the band

    public OceanEdgePass(GenGrid g, SimRandom rng, OceanEdges edges, int deepWidth)
    {
        _g = g; _rng = rng; _edges = edges; _ow = deepWidth;
        _strip = new bool[g.N * g.N];
    }

    /// <summary>Widest the sea plus the beach can get from an edge, in cells.</summary>
    public static int MaxBand(int deepWidth) => deepWidth + Wobble + ShallowWidth + CoastWidth;

    /// <summary>Paints the sea of every chosen edge (and its symmetry images). Shallow is painted first so Water wins.</summary>
    public void Paint()
    {
        int n = _g.N, band = MaxBand(_ow);
        var depth = new int[4][];
        for (int e = 0; e < 4; e++)
        {
            if (((int)_edges & (1 << e)) == 0) continue;
            depth[e] = new int[n];
            int d = 1;
            for (int t = 0; t < n; t++)
            {
                if (_rng.Next(3) == 0) d = Math.Clamp(d + _rng.Next(-1, 2), 0, Wobble);
                depth[e][t] = _ow + d;
            }
        }
        for (int pass = 0; pass < 2; pass++)
            for (int e = 0; e < 4; e++)
            {
                if (depth[e] is null) continue;
                for (int t = 0; t < n; t++)
                {
                    int limit = depth[e][t] + (pass == 0 ? ShallowWidth : 0);
                    for (int k = 0; k < limit; k++)
                    {
                        int i = _g.Idx(CellX(e, t, k), CellY(e, t, k));
                        if (pass == 0) { if (_g.All(i, j => _g.Land[j] != LandType.Water)) _g.SetLand(i, LandType.Shallow); }
                        else _g.SetLand(i, LandType.Water);
                    }
                    if (pass == 0)
                        for (int k = 0; k < band; k++)
                            foreach (int j in _g.Images(_g.Idx(CellX(e, t, k), CellY(e, t, k)))) _strip[j] = true;
                }
            }
    }

    /// <summary>M13-01 (D-205): where no sea that reaches the map edge lies within <see cref="NearSea"/> cells of the start (Mountains layout 1:
    /// the start sits mid-edge and the ocean is on the north and south edges), carves a three-wide Shallow channel from the edge sea to
    /// within <see cref="ChannelEnd"/> cells of the start. Shallow is passable on foot and by ship, so land routes are never cut. The path
    /// is the shortest 4-way one over cells free of resources, features, start surroundings, plateaus and rivers (ties in cell-index order).
    /// Writes go to every image, and the channel joins the edge band so <see cref="PaintCoast"/> gives it a beach. Does nothing (and draws no
    /// random numbers) when the start already has such sea, so maps without the problem stay byte-identical. Run before the props.</summary>
    public void Channel(CellCoord start)
    {
        int n = _g.N;
        var edge = new bool[n * n];
        var queue = new int[n * n];
        int head = 0, tail = 0;
        for (int i = 0; i < n * n; i++)
        {
            int x = i % n, y = i / n;
            if ((x == 0 || y == 0 || x == n - 1 || y == n - 1) && IsSea(_g.Land[i])) { edge[i] = true; queue[tail++] = i; }
        }
        while (head < tail)
        {
            int i = queue[head++], cx = i % n, cy = i / n;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= n || ny >= n || edge[ny * n + nx] || !IsSea(_g.Land[ny * n + nx])) continue;
                    edge[ny * n + nx] = true; queue[tail++] = ny * n + nx;
                }
        }
        for (int i = 0; i < n * n; i++)
            if (edge[i] && Math.Max(Math.Abs(i % n - start.X), Math.Abs(i / n - start.Y)) <= NearSea) return;

        // shortest 4-way path from the edge sea to a free cell near the start
        var parent = new int[n * n];
        Array.Fill(parent, -2);
        head = 0; tail = 0;
        for (int i = 0; i < n * n; i++) if (edge[i]) { parent[i] = -1; queue[tail++] = i; }
        int goal = -1;
        while (head < tail && goal < 0)
        {
            int i = queue[head++], cx = i % n, cy = i / n;
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                int j = ny * n + nx;
                if (parent[j] != -2 || !Carvable(j)) continue;
                parent[j] = i; queue[tail++] = j;
                if (Math.Max(Math.Abs(nx - start.X), Math.Abs(ny - start.Y)) <= ChannelEnd) { goal = j; break; }
            }
        }
        if (goal < 0) return;   // walled in: the validator and SkirmishStart's whole-map fallback cover it
        var path = new List<int>();
        for (int i = goal; i >= 0 && !edge[i]; i = parent[i]) path.Add(i);
        foreach (int i in path)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = i % n + dx, y = i / n + dy;
                    if (!_g.In(x, y) || !Carvable(_g.Idx(x, y))) continue;
                    _g.SetLand(_g.Idx(x, y), LandType.Shallow);
                }
        foreach (int i in path)
            for (int dy = -CoastWidth - 1; dy <= CoastWidth + 1; dy++)
                for (int dx = -CoastWidth - 1; dx <= CoastWidth + 1; dx++)
                {
                    int x = i % n + dx, y = i / n + dy;
                    if (_g.In(x, y)) foreach (int j in _g.Images(_g.Idx(x, y))) _strip[j] = true;
                }
    }

    /// <summary>Cells the channel may take: plain flat ground with nothing on it, outside start surroundings and rivers (the sea itself is skipped by the caller).</summary>
    private bool Carvable(int i) =>
        !IsSea(_g.Land[i]) && _g.All(i, j => _g.Overlay[j] == OverlayKind.None && !_g.Feature[j] && !_g.Reserved[j] && !_g.River[j] && !_g.Ramp[j] && _g.Height[j] == 0);

    /// <summary>The Command Ship wants edge-connected sea this close to the start (SkirmishStart's ring-16 fallback).</summary>
    private const int NearSea = 16, ChannelEnd = 10;

    /// <summary>Flood the edge sea, then make the two rings of plain free land beside it Coast (the landing beach). Run after every
    /// other terrain step so rivers, ruins and start squares are already in place.</summary>
    public void PaintCoast()
    {
        int n = _g.N;
        _sea = new bool[n * n];
        var queue = new int[n * n];
        int head = 0, tail = 0;
        for (int i = 0; i < n * n; i++)
        {
            int x = i % n, y = i / n;
            bool border = x == 0 || y == 0 || x == n - 1 || y == n - 1;
            if (border && _strip[i] && IsSea(_g.Land[i])) { _sea[i] = true; queue[tail++] = i; }
        }
        while (head < tail)
        {
            int i = queue[head++];
            foreach (int j in Neighbours(i))
                if (!_sea[j] && _strip[j] && IsSea(_g.Land[j])) { _sea[j] = true; queue[tail++] = j; }
        }
        var ring = new bool[n * n];
        for (int r = 0; r < CoastWidth; r++)
        {
            var next = new bool[n * n];
            for (int i = 0; i < n * n; i++)
            {
                if (!Eligible(i) || ring[i]) continue;
                foreach (int j in Neighbours(i))
                    if (r == 0 ? _sea[j] : ring[j]) { next[i] = true; break; }
            }
            for (int i = 0; i < n * n; i++) ring[i] |= next[i];
        }
        for (int i = 0; i < n * n; i++) if (ring[i]) _g.SetLand(i, LandType.Coast);
    }

    /// <summary>The <c>convoy_edge</c> cells (every 8th sea cell along each chosen border, edge order N, E, S, W) and, per start in order,
    /// the sea-facing Coast cell nearest by Foot path (none for a start that cannot reach one: the validator then reports it).</summary>
    public (List<CellCoord> Convoy, List<CellCoord> Landing) Markers(MapData map, IReadOnlyList<CellCoord> starts, bool[]? blocked)
    {
        int n = _g.N;
        var convoy = new List<CellCoord>();
        var seen = new bool[n * n];
        for (int e = 0; e < 4; e++)
        {
            int taken = 0;
            for (int t = 0; t < n; t++)
            {
                int x = CellX(e, t, 0), y = CellY(e, t, 0), i = y * n + x;
                if (!_sea[i] || seen[i] || _g.Land[i] != LandType.Water || taken++ % ConvoyStride != 0) continue;
                seen[i] = true;
                convoy.Add(new CellCoord(x, y));
            }
        }
        var landing = new List<CellCoord>();
        foreach (CellCoord s in starts)
        {
            int[] dist = MapAnalysis.Distances(map, s, Locomotion.Foot, null, blocked);
            int best = -1;
            for (int i = 0; i < n * n; i++)
            {
                if (map.Land[i] != LandType.Coast || dist[i] == MapAnalysis.Unreachable || !TouchesSea(i)) continue;
                if (best < 0 || dist[i] < dist[best]) best = i;
            }
            if (best < 0) continue;
            var c = new CellCoord(best % n, best / n);
            if (!landing.Contains(c)) landing.Add(c);
        }
        return (convoy, landing);
    }

    private bool TouchesSea(int i)
    {
        foreach (int j in Neighbours(i)) if (_sea[j]) return true;
        return false;
    }

    private bool Eligible(int i) =>
        _strip[i] && _g.Land[i] is LandType.Clear or LandType.Rough or LandType.Ash && _g.Overlay[i] == OverlayKind.None
        && !_g.Feature[i] && !_g.Core[i] && _g.Height[i] == 0;

    private static bool IsSea(LandType l) => l is LandType.Water or LandType.Shallow;

    private IEnumerable<int> Neighbours(int i)
    {
        int n = _g.N, x = i % n, y = i / n;
        if (x + 1 < n) yield return i + 1;
        if (x > 0) yield return i - 1;
        if (y + 1 < n) yield return i + n;
        if (y > 0) yield return i - n;
    }

    // Edge numbering as ProceduralBuilder.Ocean: 0 top, 1 right, 2 bottom, 3 left; t runs along the edge, k inland.
    private int CellX(int e, int t, int k) => e switch { 0 => t, 1 => _g.N - 1 - k, 2 => t, _ => k };
    private int CellY(int e, int t, int k) => e switch { 0 => k, 1 => t, 2 => _g.N - 1 - k, _ => t };
}
