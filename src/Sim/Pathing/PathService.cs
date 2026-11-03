// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Pathing;

/// <summary>
/// Deterministic budgeted path service (design 01 section 7). Sim state ("paths"): the request queue, budget
/// debt, unpolled results and the next request id are saved; the result cache is a pure function of map state
/// and is not.
/// <para>Rules: a request is either untouched in the queue or fully solved inside one <see cref="Process"/>.
/// Process solves queued requests FIFO while budget remains; each solve costs its node-expansion count and
/// may overdraw, the overdraft is carried as debt into the next tick. A cache hit charges the cost of the
/// original solve, so timing never depends on cache state.</para>
/// <para>Search: A* over 8 directions, open set ordered by (f, h, cell index) in a hand-written heap (a total
/// order). Straight step 10, diagonal 14, times the destination cell factor 10000 / SpeedPercent. Blocked =
/// outside the map, SpeedPercent 0 or a building. Diagonals may not cut a blocked corner. The start cell is
/// never checked. Paths exclude the start and include the end; every cell is returned.</para>
/// <para>Simplifications: a goal that is blocked or enclosed gives Partial toward the reachable cell closest
/// by Chebyshev distance (tie: lowest cell index), or NotFound when that cell is the start. A group search is a
/// backward Dijkstra from the goal bounded to the bounding box of the starts and goal plus 16 cells; starts that
/// are blocked, outside the map or not reached inside the box get NotFound, and a blocked goal gives NotFound
/// for all. Groups are not cached.</para>
/// </summary>
public sealed class PathService : ISimStatePart
{
    private const int GroupMargin = 16;
    private const int CacheLimit = 4096;

    private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

    private sealed class Req
    {
        public int Id;
        public Locomotion Loco;
        public bool Group;
        public CellCoord To;
        public CellCoord[] Froms = Array.Empty<CellCoord>();
    }

    private sealed class Entry
    {
        public PathStatus Status;
        public CellCoord[] Path = Array.Empty<CellCoord>();
    }

    private readonly record struct CacheKey(int From, int To, Locomotion Loco, int Version);

    private sealed class CacheValue
    {
        public PathStatus Status;
        public CellCoord[] Path = Array.Empty<CellCoord>();
        public int Expansions;
    }

    private readonly MapState _map;
    private readonly TerrainTable _terrain;
    private readonly int _perTick;
    private readonly int _w;
    private readonly int _h;

    private readonly List<Req> _queue = new();
    private readonly SortedDictionary<int, Entry[]> _results = new();
    private int _nextId = 1;
    private int _debt;

    // Not saved: scratch and cache.
    private readonly Dictionary<CacheKey, CacheValue> _cache = new(); // lookup only, never iterated
    private readonly PathHeap _heap = new();
    private readonly long[] _g;
    private readonly int[] _parent;
    private readonly int[] _seen;   // == _stamp when g is valid
    private readonly int[] _closed; // == _stamp when expanded
    private int _stamp;

    public PathService(MapState map, TerrainTable terrain, int expansionBudgetPerTick = 4000)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(terrain);
        if (expansionBudgetPerTick < 1) throw new ArgumentOutOfRangeException(nameof(expansionBudgetPerTick));
        _map = map; _terrain = terrain; _perTick = expansionBudgetPerTick;
        _w = map.Width; _h = map.Height;
        int n = _w * _h;
        _g = new long[n]; _parent = new int[n]; _seen = new int[n]; _closed = new int[n];
    }

    public string Name => "paths";

    /// <summary>Test hook: when false the cache is bypassed. Results and costs must be identical either way.</summary>
    internal bool CacheEnabled { get; set; } = true;

    /// <summary>Queues a single path request and returns its id (monotonic).</summary>
    public int Request(CellCoord from, CellCoord to, Locomotion loco)
    {
        var r = new Req { Id = _nextId++, Loco = loco, To = to, Froms = new[] { from } };
        _queue.Add(r);
        return r.Id;
    }

    /// <summary>Queues one backward search giving a path for each start; poll with index i for the i-th start.</summary>
    public int RequestGroup(IReadOnlyList<CellCoord> froms, CellCoord to, Locomotion loco)
    {
        ArgumentNullException.ThrowIfNull(froms);
        if (froms.Count == 0) throw new ArgumentException("A group needs at least one start.", nameof(froms));
        var arr = new CellCoord[froms.Count];
        for (int i = 0; i < arr.Length; i++) arr[i] = froms[i];
        var r = new Req { Id = _nextId++, Loco = loco, Group = true, To = to, Froms = arr };
        _queue.Add(r);
        return r.Id;
    }

    /// <summary>Solves queued requests FIFO while budget remains. Call once per tick.</summary>
    public void Process()
    {
        long budget = (long)_perTick - _debt;
        _debt = 0;
        while (_queue.Count > 0 && budget > 0)
        {
            Req r = _queue[0];
            _queue.RemoveAt(0);
            int cost = Solve(r, out Entry[] entries);
            _results[r.Id] = entries;
            budget -= cost;
        }
        if (budget < 0) _debt = (int)(-budget);
    }

    /// <summary>
    /// Polls a request. Found/Partial/NotFound release that result (Unknown afterwards). The path excludes the
    /// start and includes the end; it is empty unless Found or Partial.
    /// </summary>
    public PathStatus Poll(int id, int index, out IReadOnlyList<CellCoord> path)
    {
        path = Array.Empty<CellCoord>();
        for (int i = 0; i < _queue.Count; i++)
            if (_queue[i].Id == id) return index >= 0 && index < _queue[i].Froms.Length ? PathStatus.Pending : PathStatus.Unknown;
        if (!_results.TryGetValue(id, out Entry[]? entries) || index < 0 || index >= entries.Length) return PathStatus.Unknown;
        Entry e = entries[index];
        if (e.Status == PathStatus.Unknown) return PathStatus.Unknown;
        PathStatus s = e.Status;
        path = e.Path;
        entries[index] = new Entry { Status = PathStatus.Unknown };
        bool any = false;
        for (int i = 0; i < entries.Length; i++) if (entries[i].Status != PathStatus.Unknown) { any = true; break; }
        if (!any) _results.Remove(id);
        return s;
    }

    /// <summary>Drops a queued request or an unpolled result.</summary>
    public void Cancel(int id)
    {
        for (int i = 0; i < _queue.Count; i++)
            if (_queue[i].Id == id) { _queue.RemoveAt(i); return; }
        _results.Remove(id);
    }

    // ---------------------------------------------------------------- solving

    private int Solve(Req r, out Entry[] entries)
    {
        if (!r.Group) return SolveSingle(r, out entries);
        return SolveGroup(r, out entries);
    }

    private bool InMap(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h;

    /// <summary>Cost factor of entering a cell, 0 if blocked.</summary>
    private int Factor(int x, int y, Locomotion loco)
    {
        if (!InMap(x, y)) return 0;
        var c = new CellCoord(x, y);
        if (!_map.BuildingAt(c).IsNone) return 0;
        int sp = _map.SpeedPercent(c, loco, _terrain);
        if (sp <= 0) return 0;
        int f = 10000 / sp;
        return f < 1 ? 1 : f;
    }

    private int MinFactor(Locomotion loco)
    {
        int best = 100;
        for (int l = 0; l < RuleCounts.LandTypes; l++)
        {
            int sp = _terrain.SpeedPercent((LandType)l, loco);
            if (sp > 0 && 10000 / sp < best) best = 10000 / sp;
        }
        return best < 1 ? 1 : best;
    }

    private static CellCoord[] Rebuild(int[] parent, int start, int end, int w)
    {
        int len = 0;
        for (int c = end; c != start; c = parent[c]) len++;
        var path = new CellCoord[len];
        int k = len - 1;
        for (int c = end; c != start; c = parent[c]) path[k--] = new CellCoord(c % w, c / w);
        return path;
    }

    private int SolveSingle(Req r, out Entry[] entries)
    {
        CellCoord from = r.Froms[0], to = r.To;
        entries = new[] { new Entry { Status = PathStatus.NotFound } };
        if (!InMap(from.X, from.Y) || !InMap(to.X, to.Y)) return 0;

        var key = new CacheKey(from.Y * _w + from.X, to.Y * _w + to.X, r.Loco, _map.Version);
        if (CacheEnabled && _cache.TryGetValue(key, out CacheValue? hit))
        {
            entries[0] = new Entry { Status = hit.Status, Path = hit.Path };
            return hit.Expansions;
        }

        int expansions = Astar(from, to, r.Loco, out PathStatus status, out CellCoord[] path);
        entries[0] = new Entry { Status = status, Path = path };
        if (CacheEnabled)
        {
            if (_cache.Count >= CacheLimit) _cache.Clear(); // eviction only affects speed, never results or cost
            _cache[key] = new CacheValue { Status = status, Path = path, Expansions = expansions };
        }
        return expansions;
    }

    private int Astar(CellCoord from, CellCoord to, Locomotion loco, out PathStatus status, out CellCoord[] path)
    {
        int w = _w;
        int start = from.Y * w + from.X, goal = to.Y * w + to.X;
        status = PathStatus.Found; path = Array.Empty<CellCoord>();
        if (start == goal) return 0;

        _stamp++;
        _heap.Clear();
        int minF = MinFactor(loco);
        long h0 = Heur(from.X, from.Y, to, minF);
        _g[start] = 0; _parent[start] = start; _seen[start] = _stamp;
        _heap.Push(h0, h0, start);

        int expansions = 0;
        int bestCell = start, bestCheb = Cheb(from.X, from.Y, to);
        while (_heap.Count > 0)
        {
            int cur = _heap.Pop();
            if (_closed[cur] == _stamp) continue;
            _closed[cur] = _stamp;
            expansions++;
            if (cur == goal)
            {
                path = Rebuild(_parent, start, goal, w);
                return expansions;
            }
            int cx = cur % w, cy = cur / w;
            int cheb = Cheb(cx, cy, to);
            if (cheb < bestCheb || (cheb == bestCheb && cur < bestCell)) { bestCheb = cheb; bestCell = cur; }

            for (int d = 0; d < 8; d++)
            {
                int nx = cx + Dx[d], ny = cy + Dy[d];
                int f = Factor(nx, ny, loco);
                if (f == 0) continue;
                bool diag = (d & 1) == 1;
                if (diag && (Factor(cx + Dx[d], cy, loco) == 0 || Factor(cx, cy + Dy[d], loco) == 0)) continue;
                int n = ny * w + nx;
                if (_closed[n] == _stamp) continue;
                long ng = _g[cur] + (diag ? 14L : 10L) * f;
                if (_seen[n] == _stamp && ng >= _g[n]) continue;
                _g[n] = ng; _parent[n] = cur; _seen[n] = _stamp;
                long h = Heur(nx, ny, to, minF);
                _heap.Push(ng + h, h, n);
            }
        }

        // Exhausted: goal unreachable.
        if (bestCell == start) { status = PathStatus.NotFound; return expansions; }
        status = PathStatus.Partial;
        path = Rebuild(_parent, start, bestCell, w);
        return expansions;
    }

    private static long Heur(int x, int y, CellCoord to, int minFactor)
    {
        int dx = x > to.X ? x - to.X : to.X - x;
        int dy = y > to.Y ? y - to.Y : to.Y - y;
        int mn = dx < dy ? dx : dy;
        return (10L * (dx + dy) - 6L * mn) * minFactor;
    }

    private static int Cheb(int x, int y, CellCoord to)
    {
        int dx = x > to.X ? x - to.X : to.X - x;
        int dy = y > to.Y ? y - to.Y : to.Y - y;
        return dx > dy ? dx : dy;
    }

    private int SolveGroup(Req r, out Entry[] entries)
    {
        int w = _w;
        CellCoord to = r.To;
        entries = new Entry[r.Froms.Length];
        for (int i = 0; i < entries.Length; i++) entries[i] = new Entry { Status = PathStatus.NotFound };
        if (!InMap(to.X, to.Y) || Factor(to.X, to.Y, r.Loco) == 0) return 0;

        int minX = to.X, maxX = to.X, minY = to.Y, maxY = to.Y;
        foreach (CellCoord f in r.Froms)
        {
            if (f.X < minX) minX = f.X;
            if (f.X > maxX) maxX = f.X;
            if (f.Y < minY) minY = f.Y;
            if (f.Y > maxY) maxY = f.Y;
        }
        minX = Math.Max(0, minX - GroupMargin); minY = Math.Max(0, minY - GroupMargin);
        maxX = Math.Min(_w - 1, maxX + GroupMargin); maxY = Math.Min(_h - 1, maxY + GroupMargin);

        int remaining = 0;
        var wanted = new bool[_w * _h];
        foreach (CellCoord f in r.Froms)
        {
            if (!InMap(f.X, f.Y)) continue;
            int ci = f.Y * w + f.X;
            if (!wanted[ci]) { wanted[ci] = true; remaining++; }
        }

        _stamp++;
        _heap.Clear();
        int goal = to.Y * w + to.X;
        _g[goal] = 0; _seen[goal] = _stamp;
        _heap.Push(0, 0, goal);
        int expansions = 0;
        while (_heap.Count > 0 && remaining > 0)
        {
            int cur = _heap.Pop();
            if (_closed[cur] == _stamp) continue;
            _closed[cur] = _stamp;
            expansions++;
            if (wanted[cur]) remaining--;
            int cx = cur % w, cy = cur / w;
            int enterCur = Factor(cx, cy, r.Loco);
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + Dx[d], ny = cy + Dy[d];
                if (nx < minX || nx > maxX || ny < minY || ny > maxY) continue;
                if (Factor(nx, ny, r.Loco) == 0) continue;
                bool diag = (d & 1) == 1;
                if (diag && (Factor(cx + Dx[d], cy, r.Loco) == 0 || Factor(cx, cy + Dy[d], r.Loco) == 0)) continue;
                int n = ny * w + nx;
                if (_closed[n] == _stamp) continue;
                // The forward move is n -> cur and costs the factor of the cell entered (cur).
                long ng = _g[cur] + (diag ? 14L : 10L) * enterCur;
                if (_seen[n] == _stamp && ng >= _g[n]) continue;
                _g[n] = ng; _seen[n] = _stamp;
                _heap.Push(ng, 0, n);
            }
        }

        for (int i = 0; i < r.Froms.Length; i++)
        {
            CellCoord f = r.Froms[i];
            if (!InMap(f.X, f.Y)) continue;
            int s = f.Y * w + f.X;
            if (_closed[s] != _stamp || Factor(f.X, f.Y, r.Loco) == 0) continue;
            entries[i] = new Entry { Status = PathStatus.Found, Path = GroupWalk(s, goal, r.Loco) };
        }
        return expansions;
    }

    /// <summary>Follows the settled distance field from a start to the goal; best next cell, lowest index on ties.</summary>
    private CellCoord[] GroupWalk(int start, int goal, Locomotion loco)
    {
        var list = new List<CellCoord>();
        int cur = start;
        while (cur != goal)
        {
            int cx = cur % _w, cy = cur / _w;
            int bestN = -1; long bestV = 0;
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + Dx[d], ny = cy + Dy[d];
                int f = Factor(nx, ny, loco);
                if (f == 0) continue;
                bool diag = (d & 1) == 1;
                if (diag && (Factor(cx + Dx[d], cy, loco) == 0 || Factor(cx, cy + Dy[d], loco) == 0)) continue;
                int n = ny * _w + nx;
                if (_closed[n] != _stamp) continue;
                long v = _g[n] + (diag ? 14L : 10L) * f;
                if (bestN < 0 || v < bestV || (v == bestV && n < bestN)) { bestN = n; bestV = v; }
            }
            cur = bestN;
            list.Add(new CellCoord(cur % _w, cur / _w));
        }
        return list.ToArray();
    }

    // ---------------------------------------------------------------- state part

    public void Write(BinaryWriter w)
    {
        w.Write(_nextId); w.Write(_debt);
        w.Write(_queue.Count);
        foreach (Req r in _queue)
        {
            w.Write(r.Id); w.Write((byte)r.Loco); w.Write(r.Group); w.Write(r.To.X); w.Write(r.To.Y);
            w.Write(r.Froms.Length);
            foreach (CellCoord f in r.Froms) { w.Write(f.X); w.Write(f.Y); }
        }
        w.Write(_results.Count);
        foreach (KeyValuePair<int, Entry[]> kv in _results) // SortedDictionary: ascending id
        {
            w.Write(kv.Key); w.Write(kv.Value.Length);
            foreach (Entry e in kv.Value)
            {
                w.Write((byte)e.Status); w.Write(e.Path.Length);
                foreach (CellCoord c in e.Path) { w.Write(c.X); w.Write(c.Y); }
            }
        }
    }

    public void Read(BinaryReader r)
    {
        _nextId = r.ReadInt32(); _debt = r.ReadInt32();
        _queue.Clear(); _results.Clear(); _cache.Clear();
        int q = r.ReadInt32();
        for (int i = 0; i < q; i++)
        {
            var req = new Req { Id = r.ReadInt32(), Loco = (Locomotion)r.ReadByte(), Group = r.ReadBoolean() };
            req.To = new CellCoord(r.ReadInt32(), r.ReadInt32());
            req.Froms = new CellCoord[r.ReadInt32()];
            for (int k = 0; k < req.Froms.Length; k++) req.Froms[k] = new CellCoord(r.ReadInt32(), r.ReadInt32());
            _queue.Add(req);
        }
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            int id = r.ReadInt32();
            var arr = new Entry[r.ReadInt32()];
            for (int k = 0; k < arr.Length; k++)
            {
                var e = new Entry { Status = (PathStatus)r.ReadByte() };
                e.Path = new CellCoord[r.ReadInt32()];
                for (int p = 0; p < e.Path.Length; p++) e.Path[p] = new CellCoord(r.ReadInt32(), r.ReadInt32());
                arr[k] = e;
            }
            _results[id] = arr;
        }
    }
}
