// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

// The decoration pass of the generator (M12-05, design 16 s2.5, D-180 D1): blocking props in clusters, favouring cliff, water and map
// edges, avoiding roads and starts, yards around neutral ruins, a fill for dead ground, and a flood-fill guard after every cluster.
// Everything is written through GenGrid, so each prop lands on all symmetry images with the same variant byte (density).
internal sealed partial class ProceduralBuilder
{
    private const int PropVariants = 3;
    private const int MinImageGap = 3;       // a prop's symmetry images keep this Chebyshev distance, so images never fuse into a wall
    private const int ClusterTries = 60;
    private const int DeadRounds = 60;
    private const int PropStartKeepOut = 12; // study: 1.8 % of trees within 12 cells of a start, against 13.5 % of the area

    private int[] _edgeScore = Array.Empty<int>();
    private bool[] _nearResource = Array.Empty<bool>();
    private int[] _reachBefore = Array.Empty<int>();

    /// <summary>The blocking prop pass. Budget, clusters with gaps, edge preference, ruin yards, keep-outs, a dead-ground fill, and a
    /// connectivity guard that removes a cluster whose placement cut off any ground the first start could reach (Foot or Tracked).</summary>
    private void DecorateProps(MapData map)
    {
        DecorParams d = _b.Decor;
        if (d.PropPermille <= 0) return;
        int n = _n, total = n * n;
        _nearResource = new bool[total];
        for (int i = 0; i < total; i++)
        {
            if (!OverlayKinds.IsResource(_g.Overlay[i])) continue;
            int x = i % n, y = i / n;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (_g.In(x + dx, y + dy)) _nearResource[_g.Idx(x + dx, y + dy)] = true;
        }

        _edgeScore = new int[total];
        for (int i = 0; i < total; i++) _edgeScore[i] = SeedWeight(i, d);

        var guard = new FlowGuard(map, MapAnalysis.FeatureMask(map, _features));
        int budget = d.PropPermille * total / 1000, placed = 0;

        // Ruin yards first: they are the landmarks. Features come k at a time (one per image); the first of each group is the base ruin.
        for (int f = 0; f + 0 < _features.Count; f += _g.K)
        {
            MapFeature ruin = _features[f];
            (int fw, int fh) = MapAnalysis.FootprintOf(ruin.Type);
            int want = d.YardMin + (d.YardMax > d.YardMin ? _rng.Next(d.YardMax - d.YardMin + 1) : 0);
            int got = 0;
            for (int attempt = 0; attempt < 12 && got < want; attempt++)
            {
                int cx = ruin.Cell.X + _rng.Next(-3, fw + 3), cy = ruin.Cell.Y + _rng.Next(-3, fh + 3);
                if (!_g.In(cx, cy)) continue;
                got += Cluster(d, d.YardKinds, cx, cy, Math.Min(want - got, 1 + _rng.Next(4)), guard);
            }
            placed += got * _g.K;
        }

        for (int attempt = 0; attempt < 4000 && placed < budget; attempt++)
        {
            int x = _rng.Next(n), y = _rng.Next(n);
            if (_rng.Next(2000) >= _edgeScore[_g.Idx(x, y)]) continue;
            int size = _rng.Next(1000) < d.SinglePermille ? 1 : d.ClusterMin + _rng.Next(d.ClusterMax - d.ClusterMin + 1);
            placed += Cluster(d, d.Kinds, x, y, size, guard, size == 1 ? 1 : Math.Min(size, d.ClusterMin)) * _g.K;
        }

        // Dead ground: groves and wreck fields where nothing is near, until under the target (at most twice the budget).
        int cap = budget * 2;
        for (int round = 0; round < DeadRounds * 2 && placed < cap; round++)
        {
            int dead = DeadCells(out int pick);
            if (dead * 1000 <= d.DeadTargetPermille * total || pick < 0) break;
            int size = 2 + _rng.Next(3);   // small groups: a prop clears the 9x9 window around it
            placed += Cluster(d, d.Kinds, pick % n, pick / n, size, guard) * _g.K;
        }
    }

    /// <summary>Seed weight of a cell in per mille: neutral 1000, scaled up beside cliffs and water and near the map edge, down near roads.</summary>
    private int SeedWeight(int i, DecorParams d)
    {
        int x = i % _n, y = i / _n;
        long w = 1000;
        bool cliff = false, water = false, road = false;
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                if (!_g.In(x + dx, y + dy)) continue;
                LandType l = _g.Land[_g.Idx(x + dx, y + dy)];
                cliff |= l is LandType.Cliff or LandType.Rock;
                water |= l is LandType.Water or LandType.Shallow;
                road |= l == LandType.Road;
            }
        if (cliff) w = w * d.CliffPermille / 1000;
        if (water) w = w * d.WaterPermille / 1000;
        if (road) w = w * d.RoadPermille / 1000;
        if (Math.Min(Math.Min(x, y), Math.Min(_n - 1 - x, _n - 1 - y)) < 5) w = w * d.MapEdgePermille / 1000;
        return (int)Math.Min(w, 4000);
    }

    /// <summary>Whether a prop may stand on cell <paramref name="i"/> (every image must pass).</summary>
    private bool PropOk(int i)
    {
        ReadOnlySpan<int> imgs = _g.Images(i);
        foreach (int j in imgs)
        {
            if (_g.Overlay[j] != OverlayKind.None || _g.Reserved[j] || _g.Height[j] != 0 || _g.River[j] || _g.Feature[j] || _g.Ramp[j] || _nearResource[j]) return false;
            LandType l = _g.Land[j];
            if (l != _g.Base && l != LandType.Rough) return false;
            if (NearFeature(j) || NearStart(j)) return false;
        }
        for (int a = 0; a < imgs.Length; a++)
            for (int b = a + 1; b < imgs.Length; b++)
                if (Cheb(imgs[a] % _n, imgs[a] / _n, imgs[b] % _n, imgs[b] / _n) < MinImageGap) return false;
        return true;
    }

    private bool NearFeature(int j)
    {
        int x = j % _n, y = j / _n;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (_g.In(x + dx, y + dy) && _g.Feature[_g.Idx(x + dx, y + dy)]) return true;
        return false;
    }

    private bool NearStart(int j)
    {
        int x = j % _n, y = j / _n;
        foreach (CellCoord s in OrbitStarts)
            if (Cheb(x, y, s.X, s.Y) <= PropStartKeepOut - (_scale < 100 ? 3 : 0)) return !CliffNearby(x, y);
        return false;
    }

    // The study allows props at a cliff edge inside the start surroundings (the only trees within 12 cells of a start).
    private bool CliffNearby(int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (_g.In(x + dx, y + dy) && _g.Land[_g.Idx(x + dx, y + dy)] is LandType.Cliff or LandType.Rock && !_g.Reserved[_g.Idx(x, y)]) return true;
        return false;
    }

    private OverlayKind PickKind(IReadOnlyList<PropWeight> kinds)
    {
        int sum = 0;
        foreach (PropWeight k in kinds) sum += k.Weight;
        int r = _rng.Next(sum);
        foreach (PropWeight k in kinds) { if (r < k.Weight) return k.Kind; r -= k.Weight; }
        return kinds[0].Kind;
    }

    /// <summary>Grows one cluster of up to <paramref name="size"/> cells from the seed (or the nearest valid cell), one kind per cluster,
    /// with random holes. Keeps it only if the guard says no ground was cut off. Returns the cells placed in the fundamental view (one per
    /// image group; the caller scales by the image count).</summary>
    private int Cluster(DecorParams d, IReadOnlyList<PropWeight> kinds, int sx, int sy, int size, FlowGuard guard, int minKeep = 1)
    {
        OverlayKind kind = PickKind(kinds);
        var cells = new List<int>();
        var frontier = new List<int>();
        int seed = -1;
        for (int t = 0; t < ClusterTries && seed < 0; t++)
        {
            int x = sx + (t == 0 ? 0 : _rng.Next(-2, 3)), y = sy + (t == 0 ? 0 : _rng.Next(-2, 3));
            if (_g.In(x, y) && PropOk(_g.Idx(x, y))) seed = _g.Idx(x, y);
        }
        if (seed < 0) return 0;
        frontier.Add(seed);
        var taken = new HashSet<int>();   // membership only; never iterated
        int guardTries = size * 6;
        while (cells.Count < size && frontier.Count > 0 && guardTries-- > 0)
        {
            int pick = _rng.Next(frontier.Count);
            int c = frontier[pick];
            frontier.RemoveAt(pick);
            if (!taken.Add(c)) continue;
            int x = c % _n, y = c / _n;
            bool hole = cells.Count > 0 && _rng.Next(1000) < d.GapPermille;
            if (!hole && PropOk(c) && OutsideClusterImages(c, cells)) cells.Add(c);
            for (int dir = 0; dir < 8; dir++)
            {
                int nx = x + (dir is 1 or 2 or 3 ? 1 : dir is 5 or 6 or 7 ? -1 : 0), ny = y + (dir is 3 or 4 or 5 ? 1 : dir is 7 or 0 or 1 ? -1 : 0);
                if (_g.In(nx, ny) && !taken.Contains(_g.Idx(nx, ny))) frontier.Add(_g.Idx(nx, ny));
            }
        }

        if (cells.Count < minKeep) return 0;   // a grove that came out as two stray cells is confetti: drop it
        foreach (int c in cells)
        {
            byte variant = (byte)_rng.Next(PropVariants);   // one draw per cell: the same variant on every image keeps the map symmetric
            foreach (int j in _g.Images(c)) { _g.Overlay[j] = kind; _g.Density[j] = variant; }
        }
        if (guard.Cuts(cells, _g)) // this cluster would wall something in: take it back
        {
            foreach (int c in cells)
                foreach (int j in _g.Images(c)) { _g.Overlay[j] = OverlayKind.None; _g.Density[j] = 0; }
            return 0;
        }
        guard.Commit();
        return cells.Count;
    }

    // A cell of this cluster must not touch an image of another cell of it (keeps the images from joining across a symmetry axis).
    private bool OutsideClusterImages(int c, List<int> cells)
    {
        foreach (int other in cells)
            foreach (int j in _g.Images(other))
            {
                if (j == other) continue;
                if (Cheb(c % _n, c / _n, j % _n, j / _n) < 2) return false;
            }
        foreach (int j in _g.Images(c))
        {
            if (j == c) continue;
            foreach (int other in cells)
                if (Cheb(j % _n, j / _n, other % _n, other / _n) < 2) return false;
        }
        return true;
    }

    /// <summary>Counts cells with nothing (no prop, resource, ruin, or land other than the base) within 4 cells; returns a random one as
    /// <paramref name="pick"/> (or -1). Uses a 2D prefix sum.</summary>
    private int DeadCells(out int pick)
    {
        int n = _n;
        var sum = new int[(n + 1) * (n + 1)];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                bool something = _g.Land[i] != _g.Base || _g.Overlay[i] != OverlayKind.None || _g.Feature[i] || _g.Height[i] != 0;
                sum[(y + 1) * (n + 1) + x + 1] = sum[y * (n + 1) + x + 1] + sum[(y + 1) * (n + 1) + x] - sum[y * (n + 1) + x] + (something ? 1 : 0);
            }
        int dead = 0, chosen = -1;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int x0 = Math.Max(0, x - 4), y0 = Math.Max(0, y - 4), x1 = Math.Min(n, x + 5), y1 = Math.Min(n, y + 5);
                int s = sum[y1 * (n + 1) + x1] - sum[y0 * (n + 1) + x1] - sum[y1 * (n + 1) + x0] + sum[y0 * (n + 1) + x0];
                if (s != 0) continue;
                dead++;
                if (_rng.Next(dead) == 0) chosen = y * n + x;   // reservoir pick
            }
        pick = chosen;
        return dead;
    }

    /// <summary>Remembers which cells the first start reaches (Foot and Tracked) before a cluster, so a cluster that makes any
    /// of that ground unreachable (beyond its own cells) is detected. Uses the shared map arrays (props block ground units).</summary>
    private sealed class FlowGuard
    {
        private readonly MapData _map;
        private int[] _foot;
        private int[] _tracked;
        private int _footCount, _trackedCount;

        private readonly bool[]? _blocked;

        public FlowGuard(MapData map, bool[]? blocked)
        {
            _map = map;
            _blocked = blocked;
            _foot = MapAnalysis.Distances(map, map.StartPositions[0], Locomotion.Foot, null, blocked);
            _tracked = MapAnalysis.Distances(map, map.StartPositions[0], Locomotion.Tracked, null, blocked);
            _footCount = Count(_foot);
            _trackedCount = Count(_tracked);
        }

        private static int Count(int[] d) { int c = 0; foreach (int v in d) if (v != MapAnalysis.Unreachable) c++; return c; }

        private int _newFoot, _newTracked;
        private int[]? _nf, _nt;

        public bool Cuts(List<int> cells, GenGrid g)
        {
            int lostFoot = 0, lostTracked = 0;
            foreach (int c in cells)
                foreach (int j in g.Images(c))
                {
                    if (_foot[j] != MapAnalysis.Unreachable) lostFoot++;
                    if (_tracked[j] != MapAnalysis.Unreachable) lostTracked++;
                }
            _nf = MapAnalysis.Distances(_map, _map.StartPositions[0], Locomotion.Foot, null, _blocked);
            _nt = MapAnalysis.Distances(_map, _map.StartPositions[0], Locomotion.Tracked, null, _blocked);
            _newFoot = Count(_nf); _newTracked = Count(_nt);
            // Images that were reachable only through the first start's component count once; a cluster cutting off a pocket loses more.
            return _newFoot != _footCount - lostFoot || _newTracked != _trackedCount - lostTracked
                || _newFoot == 0;
        }

        public void Commit()
        {
            _foot = _nf!; _tracked = _nt!;
            _footCount = _newFoot; _trackedCount = _newTracked;
        }
    }
}
