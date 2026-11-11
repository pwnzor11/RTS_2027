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
/// Process, when budget is left, solves in FIFO order every request that has already waited through an earlier call, then the
/// requests filed since the last call, but those only while the call has charged less than <see cref="BurstFactor"/> times the
/// per-tick budget (M13-05, D-205). The requests of one tick are solved in the same call or all wait (D-063: no slot-order bias, so
/// mirrored players stay mirrored); a waiting request is flagged and saved with the flag. Each solve costs its node-expansion count
/// and may overdraw; the overdraft is carried as debt into the next tick. A cache hit charges the cost of the original solve, so
/// timing never depends on cache state.</para>
/// <para>Search: A* over 8 directions, open set ordered by (f, h, line tie-break, cell index) in a hand-written heap (a total
/// order). Straight step 10, diagonal 14, times the destination cell factor 10000 / SpeedPercent. Blocked =
/// outside the map, SpeedPercent 0 or a building. Diagonals may not cut a blocked corner. The start cell is
/// never checked. Paths exclude the start and include the end; every cell is returned.</para>
/// <para>Blocked or enclosed goal (M13-05, D-205): reachability comes from region labels (4-connected flood over <c>Factor</c> for the
/// request's locomotion and passer; a pure function of map state, cached per map version, not saved). When the goal is not in a region
/// the start can enter, it is retargeted before searching to the nearest cell (squared straight-line distance to the goal) in such a
/// region (tie: octile distance to the start, then <see cref="LineKey"/>, then lowest cell index); the answer is Partial toward that cell, or NotFound
/// when that cell is the start. So A* never expands a whole region looking for a goal it cannot reach.</para>
/// <para>Group search (M13-05, S18 SP-13): a backward A* from the goal toward the starts in the goal's region, guided by the octile
/// distance to the bounding box of those starts (consistent, so every settled cell has its exact distance). It ends when all of them
/// are settled. Starts that are blocked, outside the map or in another region get NotFound (their actors then ask for a plain path),
/// and a blocked goal gives NotFound for all. Groups are not cached.</para>
/// <para>Gates (M3-04, D-076): a request may carry a passer (player slot, <see cref="NoPasser"/> = none). A building cell is
/// passable for it when <see cref="GateOpen"/> says the building is a gate of the passer's team; for everyone else a gate is a
/// building like any other. The passer is part of the cache key and is saved with a queued request only when set.</para>
/// </summary>
public sealed class PathService : ISimStatePart
{
    private const int CacheLimit = 4096;

    /// <summary>M13-05: one <see cref="Process"/> call leaves the newly filed requests waiting when the requests that had already waited
    /// cost at least this many times the per-tick budget.</summary>
    public const int BurstFactor = 4;

    /// <summary>Passer value meaning "no player": every building blocks (M3-04).</summary>
    public const byte NoPasser = 0xFF;

    private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

    private sealed class Req
    {
        public int Id;
        public Locomotion Loco;
        public byte Passer = NoPasser;   // M3-04
        public bool Group;
        public bool Waited;   // M13-05: has waited through a Process call (saved as bit 5 of the loco byte)
        public CellCoord To;
        public CellCoord[] Froms = Array.Empty<CellCoord>();
    }

    private sealed class Entry
    {
        public PathStatus Status;
        public CellCoord[] Path = Array.Empty<CellCoord>();
    }

    private readonly record struct CacheKey(int From, int To, Locomotion Loco, int Version, byte Passer);   // M3-04: + Passer

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
    private readonly int[] _wanted; // == _stamp when a group start (M13-05: stamped instead of a new array per group)
    private int _stamp;
    private byte _passer = NoPasser;   // M3-04: passer of the request being solved (scratch)

    // M13-05: region labels per (locomotion, passer, map version, load epoch). A cache of a pure function of map state.
    private sealed class Regions
    {
        public Locomotion Loco;
        public byte Passer;
        public int Version;
        public int Epoch;
        public int[] Label = Array.Empty<int>();
        public int MinFactor;   // cheapest Factor of any enterable cell on the map now: the heuristic's lower bound
    }
    private const int RegionSlots = 8;
    private readonly List<Regions> _regions = new();   // index order only, never keyed
    private int _epoch;
    private int[]? _floodQueue;
    private readonly int[] _startLabels = new int[8];

    public PathService(MapState map, TerrainTable terrain, int expansionBudgetPerTick = 4000)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(terrain);
        if (expansionBudgetPerTick < 1) throw new ArgumentOutOfRangeException(nameof(expansionBudgetPerTick));
        _map = map; _terrain = terrain; _perTick = expansionBudgetPerTick;
        _w = map.Width; _h = map.Height;
        int n = _w * _h;
        _g = new long[n]; _parent = new int[n]; _seen = new int[n]; _closed = new int[n]; _wanted = new int[n];
    }

    public string Name => "paths";

    /// <summary>M3-04: gate rule, (building on the cell, passer) =&gt; true if that building lets the passer through. Set by the
    /// Simulation; null = no gates (every building blocks). Must be a pure function of sim state.</summary>
    public Func<EntityId, byte, bool>? GateOpen { get; set; }

    /// <summary>Test hook: when false the cache is bypassed. Results and costs must be identical either way.</summary>
    internal bool CacheEnabled { get; set; } = true;

    /// <summary>Diagnostics (not state): expansions charged by the last <see cref="Process"/> call.</summary>
    internal int LastProcessCost { get; private set; }

    /// <summary>Diagnostics (not state): requests solved by the last <see cref="Process"/> call.</summary>
    internal int LastProcessCount { get; private set; }

    /// <summary>Queues a single path request and returns its id (monotonic).</summary>
    public int Request(CellCoord from, CellCoord to, Locomotion loco) => Request(from, to, loco, NoPasser);

    /// <summary>As <see cref="Request(CellCoord, CellCoord, Locomotion)"/> for a passer that may go through its team's gates (M3-04).</summary>
    public int Request(CellCoord from, CellCoord to, Locomotion loco, byte passer)
    {
        var r = new Req { Id = _nextId++, Loco = loco, Passer = passer, To = to, Froms = new[] { from } };
        _queue.Add(r);
        return r.Id;
    }

    /// <summary>Queues one backward search giving a path for each start; poll with index i for the i-th start.</summary>
    public int RequestGroup(IReadOnlyList<CellCoord> froms, CellCoord to, Locomotion loco) => RequestGroup(froms, to, loco, NoPasser);

    /// <summary>As <see cref="RequestGroup(IReadOnlyList{CellCoord}, CellCoord, Locomotion)"/> with a gate passer (M3-04).</summary>
    public int RequestGroup(IReadOnlyList<CellCoord> froms, CellCoord to, Locomotion loco, byte passer)
    {
        ArgumentNullException.ThrowIfNull(froms);
        if (froms.Count == 0) throw new ArgumentException("A group needs at least one start.", nameof(froms));
        var arr = new CellCoord[froms.Count];
        for (int i = 0; i < arr.Length; i++) arr[i] = froms[i];
        var r = new Req { Id = _nextId++, Loco = loco, Passer = passer, Group = true, To = to, Froms = arr };
        _queue.Add(r);
        return r.Id;
    }

    /// <summary>Solves all queued requests if budget remains (see the batch rule inside). Call once per tick.</summary>
    public void Process()
    {
        long budget = (long)_perTick - _debt;
        _debt = 0;
        long spent = 0;
        LastProcessCount = 0;
        // Batch rule (M2-01, D-063): while budget is left, the requests are solved a tick's worth at a time, never split, so one tick's
        // askers are answered together whichever player's actors (low or high slot) asked first. M13-05 (S18 SP-01): the requests
        // that already waited (FIFO, they come first) are always solved; the ones filed since the last call only while this call has
        // charged less than BurstFactor x the per-tick budget, else they all wait. The overdraft becomes debt that delays the next batch.
        if (budget > 0)
        {
            long cap = (long)_perTick * BurstFactor;
            int k = 0;
            while (k < _queue.Count && _queue[k].Waited) spent += SolveAt(k++);
            if (spent < cap) while (k < _queue.Count) spent += SolveAt(k++);
            _queue.RemoveRange(0, k);
            LastProcessCount = k;
            budget -= spent;
        }
        for (int i = 0; i < _queue.Count; i++) _queue[i].Waited = true;
        if (budget < 0) _debt = (int)(-budget);
        LastProcessCost = (int)Math.Min(spent, int.MaxValue);
    }

    private int SolveAt(int k)
    {
        Req r = _queue[k];
        int cost = Solve(r, out Entry[] entries);
        _results[r.Id] = entries;
        return cost;
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
        _passer = r.Passer;   // M3-04: read by Factor
        if (!r.Group) return SolveSingle(r, out entries);
        return SolveGroup(r, out entries);
    }

    private bool InMap(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h;

    /// <summary>Cost factor of entering a cell, 0 if blocked.</summary>
    private int Factor(int x, int y, Locomotion loco)
    {
        if (!InMap(x, y)) return 0;
        var c = new CellCoord(x, y);
        EntityId b = _map.BuildingAt(c);
        if (!b.IsNone && (_passer == NoPasser || GateOpen == null || !GateOpen(b, _passer))) return 0;   // M3-04: own-team gate
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
            int sp = _terrain.SpeedPercent((LandType)l, MoveClass.Table(loco));   // M7-03: NavalDeep reads the naval column
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

        var key = new CacheKey(from.Y * _w + from.X, to.Y * _w + to.X, r.Loco, _map.Version, r.Passer);
        if (CacheEnabled && _cache.TryGetValue(key, out CacheValue? hit))
        {
            entries[0] = new Entry { Status = hit.Status, Path = hit.Path };
            return hit.Expansions;
        }

        int expansions;
        PathStatus status;
        CellCoord[] path;
        CellCoord target = ReachableGoal(from, to, r.Loco);   // M13-05: blocked/enclosed goal -> nearest reachable cell
        if (target == to) expansions = Astar(from, to, r.Loco, out status, out path);
        else if (target == from) { expansions = 0; status = PathStatus.NotFound; path = Array.Empty<CellCoord>(); }
        else
        {
            expansions = Astar(from, target, r.Loco, out status, out path);
            if (status == PathStatus.Found) status = PathStatus.Partial;
        }
        entries[0] = new Entry { Status = status, Path = path };
        if (CacheEnabled)
        {
            if (_cache.Count >= CacheLimit) _cache.Clear(); // eviction only affects speed, never results or cost
            _cache[key] = new CacheValue { Status = status, Path = path, Expansions = expansions };
        }
        return expansions;
    }

    /// <summary>M13-05: region labels for the locomotion and the current passer (0 = blocked), recomputed when the map changed.</summary>
    private int[] RegionLabels(Locomotion loco) => RegionsFor(loco).Label;

    /// <summary>M13-05: the A* heuristic's factor: the cheapest cell on the map as it is (not the cheapest land type in the rules), so a
    /// map without roads gets the exact octile distance and the search no longer fans out. Still a lower bound, so routes stay optimal.</summary>
    private int MapMinFactor(Locomotion loco) => RegionsFor(loco).MinFactor;

    private Regions RegionsFor(Locomotion loco)
    {
        for (int i = 0; i < _regions.Count; i++)
        {
            Regions e = _regions[i];
            if (e.Loco == loco && e.Passer == _passer && e.Version == _map.Version && e.Epoch == _epoch) return e;
        }
        Regions slot;
        if (_regions.Count < RegionSlots) { slot = new Regions { Label = new int[_w * _h] }; _regions.Add(slot); }
        else { slot = _regions[0]; _regions.RemoveAt(0); _regions.Add(slot); }   // oldest out; affects speed only
        slot.Loco = loco; slot.Passer = _passer; slot.Version = _map.Version; slot.Epoch = _epoch;
        slot.MinFactor = Flood(loco, slot.Label);
        return slot;
    }

    /// <summary>4-connected flood (the search never cuts a blocked corner, so this is exactly 8-direction reachability).</summary>
    private int Flood(Locomotion loco, int[] label)
    {
        int minF = int.MaxValue;
        int w = _w, h = _h, n = w * h;
        Array.Clear(label);
        int[] q = _floodQueue ??= new int[n];
        int next = 0;
        for (int s = 0; s < n; s++)
        {
            if (label[s] != 0) continue;
            int fs = Factor(s % w, s / w, loco);
            if (fs == 0) continue;
            if (fs < minF) minF = fs;
            int id = ++next;
            int head = 0, tail = 0;
            q[tail++] = s; label[s] = id;
            while (head < tail)
            {
                int cur = q[head++];
                int x = cur % w, y = cur / w;
                for (int d = 0; d < 8; d += 2)   // the four orthogonal directions of Dx/Dy
                {
                    int nx = x + Dx[d], ny = y + Dy[d];
                    if (!InMap(nx, ny)) continue;
                    int c = ny * w + nx;
                    if (label[c] != 0) continue;
                    int f = Factor(nx, ny, loco);
                    if (f == 0) continue;
                    if (f < minF) minF = f;
                    label[c] = id; q[tail++] = c;
                }
            }
        }
        return minF == int.MaxValue ? MinFactor(loco) : minF;
    }

    /// <summary>
    /// M13-05: the goal itself when the start can reach it; otherwise the nearest cell (squared distance to the goal) in a region the
    /// start can enter (tie: octile distance to the start, then <see cref="LineKey"/>, then lowest cell index). The start's regions are
    /// its own, or, when the start cell is blocked (never checked), those of the neighbours a first step can enter. With no such
    /// region the goal is returned unchanged (the search then ends at once).
    /// </summary>
    private CellCoord ReachableGoal(CellCoord from, CellCoord to, Locomotion loco)
    {
        if (from == to) return to;
        int[] lab = RegionLabels(loco);
        int w = _w;
        int count = 0;
        int own = lab[from.Y * w + from.X];
        if (own != 0) _startLabels[count++] = own;
        else
            for (int d = 0; d < 8; d++)
            {
                int nx = from.X + Dx[d], ny = from.Y + Dy[d];
                if (Factor(nx, ny, loco) == 0) continue;
                if ((d & 1) == 1 && (Factor(from.X + Dx[d], from.Y, loco) == 0 || Factor(from.X, from.Y + Dy[d], loco) == 0)) continue;
                int l = lab[ny * w + nx];
                bool dup = false;
                for (int k = 0; k < count; k++) if (_startLabels[k] == l) dup = true;
                if (!dup) _startLabels[count++] = l;
            }
        if (count == 0) return to;
        bool Reach(int l)
        {
            if (l == 0) return false;
            for (int k = 0; k < count; k++) if (_startLabels[k] == l) return true;
            return false;
        }
        if (Reach(lab[to.Y * w + to.X])) return to;

        // Nearest by squared straight-line distance to the goal, so a cell sharing an edge with a blocked goal cell beats a diagonal one
        // (an attacker next to the edge of a footprint is in range; one at its corner may not be). Rings are scanned until no farther
        // ring can hold a nearer cell (every cell of ring r is at least r*r away).
        int maxR = Math.Max(_w, _h);
        int best = -1; long bestE = 0, bestD = 0, bestK = 0;
        for (int r = 1; r <= maxR; r++)
        {
            if (best >= 0 && (long)r * r > bestE) break;
            for (int y = to.Y - r; y <= to.Y + r; y++)
            {
                if (y < 0 || y >= _h) continue;
                bool edgeRow = y == to.Y - r || y == to.Y + r;
                int step = edgeRow ? 1 : 2 * r;
                for (int x = to.X - r; x <= to.X + r; x += step)
                {
                    if (x < 0 || x >= _w) continue;
                    int c = y * w + x;
                    if (!Reach(lab[c])) continue;
                    long ee = (long)(x - to.X) * (x - to.X) + (long)(y - to.Y) * (y - to.Y);
                    long dd = Heur(x, y, from, 1);
                    long kk = LineKey(x, y, from.X, from.Y, to.X, to.Y);
                    if (best < 0 || ee < bestE || (ee == bestE && (dd < bestD || (dd == bestD && (kk < bestK || (kk == bestK && c < best))))))
                    { best = c; bestE = ee; bestD = dd; bestK = kk; }
                }
            }
        }
        if (best >= 0) return new CellCoord(best % w, best / w);
        return to;
    }

    private int Astar(CellCoord from, CellCoord to, Locomotion loco, out PathStatus status, out CellCoord[] path)
    {
        int w = _w;
        int start = from.Y * w + from.X, goal = to.Y * w + to.X;
        status = PathStatus.Found; path = Array.Empty<CellCoord>();
        if (start == goal) return 0;

        _stamp++;
        _heap.Clear();
        int minF = MapMinFactor(loco);
        long h0 = Heur(from.X, from.Y, to, minF);   // minF: see MapMinFactor
        _g[start] = 0; _parent[start] = start; _seen[start] = _stamp;
        _heap.Push(h0, h0, start, 0);

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
                _heap.Push(ng + h, h, n, LineKey(nx, ny, from.X, from.Y, to.X, to.Y));
            }
        }

        // Exhausted: goal unreachable.
        if (bestCell == start) { status = PathStatus.NotFound; return expansions; }
        status = PathStatus.Partial;
        path = Rebuild(_parent, start, bestCell, w);
        return expansions;
    }

    /// <summary>
    /// Orientation-free tie-break key for equal-cost cells: distance from the straight line start-goal (cross product,
    /// smaller first), then the side of the line (left first). Rotating or mirroring a whole problem by 180 degrees maps
    /// the key onto itself, so mirrored duels get mirrored routes; a cell-index tie-break would not.
    /// </summary>
    private static long LineKey(int x, int y, int sx, int sy, int gx, int gy)
    {
        long cross = (long)(x - gx) * (sy - gy) - (long)(y - gy) * (sx - gx);
        return cross >= 0 ? cross * 2 : -cross * 2 + 1;
    }

    private static long Heur(int x, int y, CellCoord to, int minFactor)
    {
        int dx = x > to.X ? x - to.X : to.X - x;
        int dy = y > to.Y ? y - to.Y : to.Y - y;
        int mn = dx < dy ? dx : dy;
        return (10L * (dx + dy) - 6L * mn) * minFactor;
    }

    /// <summary>M13-05: octile distance from a cell to a box of cells times the cheapest factor (a lower bound to every start in it).</summary>
    private static long BoxHeur(int x, int y, int minX, int maxX, int minY, int maxY, int minFactor)
    {
        int dx = x < minX ? minX - x : x > maxX ? x - maxX : 0;
        int dy = y < minY ? minY - y : y > maxY ? y - maxY : 0;
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

        int[] lab = RegionLabels(r.Loco);
        int goal = to.Y * w + to.X;
        int region = lab[goal];
        _stamp++;
        int remaining = 0;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (CellCoord f in r.Froms)
        {
            if (!InMap(f.X, f.Y)) continue;
            int ci = f.Y * w + f.X;
            if (lab[ci] != region || _wanted[ci] == _stamp) continue;   // blocked or another region: NotFound, no search
            _wanted[ci] = _stamp; remaining++;
            if (f.X < minX) minX = f.X;
            if (f.X > maxX) maxX = f.X;
            if (f.Y < minY) minY = f.Y;
            if (f.Y > maxY) maxY = f.Y;
        }
        if (remaining == 0) return 0;

        int minF = MapMinFactor(r.Loco);
        _heap.Clear();
        _g[goal] = 0; _seen[goal] = _stamp;
        long h0 = BoxHeur(to.X, to.Y, minX, maxX, minY, maxY, minF);
        _heap.Push(h0, h0, goal);
        int bx2 = minX + maxX, by2 = minY + maxY;   // twice the centre of the starts' box: ties go along the goal-box line (D-063)
        int expansions = 0;
        while (_heap.Count > 0 && remaining > 0)
        {
            int cur = _heap.Pop();
            if (_closed[cur] == _stamp) continue;
            _closed[cur] = _stamp;
            expansions++;
            if (_wanted[cur] == _stamp) remaining--;
            int cx = cur % w, cy = cur / w;
            int enterCur = Factor(cx, cy, r.Loco);
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + Dx[d], ny = cy + Dy[d];
                if (Factor(nx, ny, r.Loco) == 0) continue;
                bool diag = (d & 1) == 1;
                if (diag && (Factor(cx + Dx[d], cy, r.Loco) == 0 || Factor(cx, cy + Dy[d], r.Loco) == 0)) continue;
                int n = ny * w + nx;
                if (_closed[n] == _stamp) continue;
                // The forward move is n -> cur and costs the factor of the cell entered (cur).
                long ng = _g[cur] + (diag ? 14L : 10L) * enterCur;
                if (_seen[n] == _stamp && ng >= _g[n]) continue;
                _g[n] = ng; _seen[n] = _stamp;
                long h = BoxHeur(nx, ny, minX, maxX, minY, maxY, minF);
                _heap.Push(ng + h, h, n, LineKey(2 * nx, 2 * ny, bx2, by2, 2 * to.X, 2 * to.Y));
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

    /// <summary>Follows the settled distance field from a start to the goal; best next cell; ties go to the cell nearest the straight start-goal line (see LineKey), then the lowest index.</summary>
    private CellCoord[] GroupWalk(int start, int goal, Locomotion loco)
    {
        var list = new List<CellCoord>();
        int cur = start;
        int sx = start % _w, sy = start / _w, gx = goal % _w, gy = goal / _w;
        while (cur != goal)
        {
            int cx = cur % _w, cy = cur / _w;
            int bestN = -1; long bestV = 0, bestK = 0;
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
                long k = LineKey(nx, ny, sx, sy, gx, gy);
                if (bestN < 0 || v < bestV || (v == bestV && (k < bestK || (k == bestK && n < bestN)))) { bestN = n; bestV = v; bestK = k; }
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
            w.Write(r.Id);
            // M3-04: bit 7 of the loco byte = a passer byte follows (unset, the bytes are as before). M13-05: bit 5 = waited.
            byte lb = (byte)((byte)r.Loco | (r.Waited ? 0x20 : 0));
            if (r.Passer == NoPasser) w.Write(lb); else { w.Write((byte)(lb | 0x80)); w.Write(r.Passer); }
            w.Write(r.Group); w.Write(r.To.X); w.Write(r.To.Y);
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
        _epoch++;   // M13-05: region labels are recomputed after a load
        int q = r.ReadInt32();
        for (int i = 0; i < q; i++)
        {
            var req = new Req { Id = r.ReadInt32() };
            byte loco = r.ReadByte();   // M3-04: bit 7 = passer follows; M13-05: bit 5 = waited
            req.Loco = (Locomotion)(loco & 0x5F);
            req.Waited = (loco & 0x20) != 0;
            if ((loco & 0x80) != 0) req.Passer = r.ReadByte();
            req.Group = r.ReadBoolean();
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
