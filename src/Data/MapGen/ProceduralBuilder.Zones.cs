// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

// Layout 2 (M12-07, D-183): the zone skeleton comes first. Rust Belt "ruined town": rubble blocks as walls on a jittered street grid,
// then the mains (a shell of rubble with a soft or walled entrance), three lanes (wide boulevard, two medium streets), plazas, a river
// with bridges and a ford, resource fields hugging walls, ruins and last the wave-2 decoration pass. Everything is painted through
// GenGrid, so the result is exactly symmetric under the main diagonal (2 players) or under both diagonals (3 and 4 players).
//
// Frame: start 0 is S0 = (a, b) on the west side of the lower-left, start 1 is its diagonal image S1 = (b, a) on the top side. Four
// players add S0' = (n-1-b, n-1-a) beside S0 and S1' beside S1: teams 1+2 and 3+4 are side by side, the enemy team is across.
internal sealed partial class ProceduralBuilder
{
    private const byte ZMain = 1, ZLane = 2, ZPlaza = 3;

    private readonly bool _zoned;
    private readonly int _players;
    private int _orbitStarts => OrbitStarts.Length;
    private int _sa, _sb;                  // S0 = (a, b)
    private int _mode;                     // 0 soft shell, 1 walled main (one ramp), 2 two gates
    private bool _natural;                 // a natural expansion on this seed
    private int _iw, _ie, _hh;             // main interior: cells west and east of the start, half height
    private int _gapN, _gapNE, _gapE;      // entrance widths
    private int _wL1, _wL2, _wL3, _bridgeW, _t2, _ry, _riverEnd;
    private byte[] _zone = Array.Empty<byte>();
    private int[] _wallDist = Array.Empty<int>();
    private readonly List<string> _zoneNotes = new();

    /// <summary>Main interior half sizes used by the checks: the shell is the ring from <c>MainRadius</c> out (see <see cref="LayoutChecks"/>).</summary>
    public IReadOnlyList<string> ZoneNotes => _zoneNotes;

    /// <summary>Layout 2 builder: <paramref name="players"/> 2 = mirror_diag with two starts, 3 or 4 = four starts (dihedral images).</summary>
    public ProceduralBuilder(BiomeParams biome, int n, int players, ulong seed)
    {
        _b = biome; _n = n; _scale = n * 100 / 96; _decorate = true; _zoned = true; _players = players;
        _g = new GenGrid(n, MapSymmetry.MirrorDiag, biome.BaseLand, players > 2);
        _rng = new SimRandom(seed);
        bool big = n >= 96;
        _sa = Math.Max(9, n * _rng.Next(14, 21) / 100);
        _sb = _sa + n * (players == 2 ? _rng.Next(43, 51) : _rng.Next(38, 43)) / 100;
        int m = _rng.Next(12);
        _mode = m < 8 ? 0 : m < 11 ? 1 : 2;
        _natural = _rng.Next(2) == 0;
        _iw = big ? 8 : 7; _ie = big ? 16 : 13; _hh = big ? 8 : 7;
        _wL1 = _rng.Next(6, 9); _wL2 = big ? _rng.Next(13, 16) : _rng.Next(10, 13); _wL3 = _rng.Next(5, 8);
        _bridgeW = _rng.Next(3, 5);
        _gapN = _rng.Next(4, 7); _gapNE = _rng.Next(4, 7); _gapE = _rng.Next(4, 7);
        if (_mode != 0) { _gapE = _rng.Next(4, 7); _gapNE = _rng.Next(4, 7); }
        _t2 = _sa + _ie - 3 + _rng.Next(4, 9);
        _ry = _sa + _wL1 / 2 + 5;
        _riverEnd = _ry + 5;
        var s0 = new CellCoord(_sa, _sb);
        var s1 = new CellCoord(_sb, _sa);
        if (players == 2) OrbitStarts = new[] { s0, s1 };
        else OrbitStarts = new[] { s0, new CellCoord(n - 1 - _sb, n - 1 - _sa), s1, new CellCoord(n - 1 - _sa, n - 1 - _sb) };
    }

    private MapData BuildZoned(string name, IReadOnlyList<CellCoord> starts)
    {
        int n = _n;
        Reserve();
        _zone = new byte[n * n];
        Blocks();
        CarveOpen();
        Shell();
        Lining();
        River();
        Cleanup();
        Roads2();
        _wallDist = WallDistances();
        FieldsZoned();
        Ruins();
        StartSquares();
        var map = new MapData(name, _b.Theater, n, n, new ushort[n * n], _g.Land, _g.Height, _g.Overlay, _g.Density, starts);
        DecorateProps(map);
        return map;
    }

    // ---- primitives -----------------------------------------------------------------------------

    private void Fill(int x0, int y0, int x1, int y1, LandType land)
    {
        for (int y = Math.Max(0, y0); y <= Math.Min(_n - 1, y1); y++)
            for (int x = Math.Max(0, x0); x <= Math.Min(_n - 1, x1); x++) _g.SetLand(_g.Idx(x, y), land);
    }

    private void ZoneRect(int x0, int y0, int x1, int y1, byte z, bool overwrite)
    {
        for (int y = Math.Max(0, y0); y <= Math.Min(_n - 1, y1); y++)
            for (int x = Math.Max(0, x0); x <= Math.Min(_n - 1, x1); x++)
            {
                int i = _g.Idx(x, y);
                foreach (int j in _g.Images(i)) if (overwrite || _zone[j] == 0) _zone[j] = z;
            }
    }

    /// <summary>Opens a rectangle (inclusive) to clear ground and tags it.</summary>
    private void Open(int x0, int y0, int x1, int y1, byte z, bool overwriteZone = true)
    {
        Fill(x0, y0, x1, y1, LandType.Clear);
        ZoneRect(x0, y0, x1, y1, z, overwriteZone || z == ZLane);
    }

    /// <summary>An axis-aligned corridor of width <paramref name="w"/> along the line from (x0,y0) to (x1,y1); the ends are padded by half a width so corners close.</summary>
    private void Corridor(int x0, int y0, int x1, int y1, int w, bool padA = true, bool padB = true)
    {
        int lo = (w - 1) / 2, hi = w / 2;
        if (x0 == x1)
        {
            int ya = y0 <= y1 ? y0 - (padA ? lo : 0) : y1 - (padB ? lo : 0);
            int yb = y0 <= y1 ? y1 + (padB ? hi : 0) : y0 + (padA ? hi : 0);
            Open(x0 - lo, ya, x0 + hi, yb, ZLane);
        }
        else
        {
            int xa = x0 <= x1 ? x0 - (padA ? lo : 0) : x1 - (padB ? lo : 0);
            int xb = x0 <= x1 ? x1 + (padB ? hi : 0) : x0 + (padA ? hi : 0);
            Open(xa, y0 - lo, xb, y0 + hi, ZLane);
        }
    }

    // ---- rubble blocks on a jittered street grid ------------------------------------------------

    private void Blocks()
    {
        int n = _n;
        var lo = new List<int>(); var wd = new List<int>();
        int pos = _rng.Next(3, 7);
        while (true)
        {
            int w = _rng.Next(10) < 7 ? 3 : _rng.Next(4, 6);
            if (pos + w + 6 > n / 2) break;
            lo.Add(pos); wd.Add(w);
            pos += w + _rng.Next(7, 13);
        }
        var streets = new List<(int Lo, int Hi)>();
        for (int k = 0; k < lo.Count; k++) streets.Add((lo[k], lo[k] + wd[k] - 1));
        for (int k = lo.Count - 1; k >= 0; k--) streets.Add((n - 1 - (lo[k] + wd[k] - 1), n - 1 - lo[k]));
        var segs = new List<(int Lo, int Hi)>();
        int from = 0;
        foreach ((int sl, int sh) in streets)
        {
            if (sl - 1 >= from) segs.Add((from, sl - 1));
            from = sh + 1;
        }
        if (from <= n - 1) segs.Add((from, n - 1));
        foreach ((int yl, int yh) in segs)
            foreach ((int xl, int xh) in segs) Block(xl, yl, xh, yh, 0);
    }

    private void Block(int x0, int y0, int x1, int y1, int depth)
    {
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        if (depth < 3 && (w > 13 || h > 13))
        {
            // Big blocks get a 3-wide alley through them.
            bool splitX = w >= h;
            int len = splitX ? w : h;
            int cut = _rng.Next(5, Math.Max(6, len - 7));
            if (splitX) { Block(x0, y0, x0 + cut - 1, y1, depth + 1); Block(x0 + cut + 3, y0, x1, y1, depth + 1); }
            else { Block(x0, y0, x1, y0 + cut - 1, depth + 1); Block(x0, y0 + cut + 3, x1, y1, depth + 1); }
            return;
        }
        if (!Canonical(x0, y0, x1, y1)) return;
        int roll = _rng.Next(100);
        if (w < 3 || h < 3) return;
        if (roll < 9 && Math.Min(w, h) >= 5)
        {
            // Rubble block: a rock core with chipped corners and a rough spill.
            int t0 = _rng.Next(0, 2), t1 = _rng.Next(0, 2), t2 = _rng.Next(0, 2), t3 = _rng.Next(0, 2);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    bool core = x >= x0 + t0 && x <= x1 - t1 && y >= y0 + t2 && y <= y1 - t3;
                    bool corner = (x == x0 + t0 || x == x1 - t1) && (y == y0 + t2 || y == y1 - t3);
                    if (core && !(corner && _rng.Next(3) == 0)) _g.SetLand(_g.Idx(x, y), LandType.Rock);
                    else if (_rng.Next(100) < 30) _g.SetLand(_g.Idx(x, y), LandType.Rough);
                }
        }
        else if (roll < 62)
        {
            // A rubble field: an irregular patch of rough ground (slow, passable), sometimes with a rock heap in it.
            double dummy = 0; _ = dummy;
            int cx2 = x0 + x1, cy2 = y0 + y1;   // doubled centre
            int rx = Math.Max(2, w * _rng.Next(40, 56) / 100), ry = Math.Max(2, h * _rng.Next(40, 56) / 100);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int ex = (2 * x - cx2) * 100 / (2 * rx), ey = (2 * y - cy2) * 100 / (2 * ry);
                    if (ex * ex + ey * ey <= 10000 + _rng.Next(-2200, 2200)) _g.SetLand(_g.Idx(x, y), LandType.Rough);
                }
            if (Math.Min(w, h) >= 6 && _rng.Next(3) == 0)
            {
                int cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, hx = Math.Max(1, w / 6), hy = Math.Max(1, h / 6);
                for (int y = cy - hy; y <= cy + hy; y++)
                    for (int x = cx - hx; x <= cx + hx; x++) _g.SetLand(_g.Idx(x, y), LandType.Rock);
            }
        }
        else if (roll < 74)
        {
            // A rubble heap in an open lot.
            int cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, r = Math.Min(w, h) >= 8 ? 2 : 1;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (Math.Abs(dx) + Math.Abs(dy) <= r + 1) _g.SetLand(_g.Idx(cx + dx, cy + dy), LandType.Rough);
        }
    }

    /// <summary>True when the block rectangle holds the lowest cell index of its own images (so a block is painted once per symmetry orbit).</summary>
    private bool Canonical(int x0, int y0, int x1, int y1)
    {
        int own = int.MaxValue, any = int.MaxValue;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = _g.Idx(x, y);
                own = Math.Min(own, i);
                foreach (int j in _g.Images(i)) any = Math.Min(any, j);
            }
        return own == any;
    }

    // ---- mains, lanes and plazas ----------------------------------------------------------------

    private void CarveOpen()
    {
        int a = _sa, b = _sb, n = _n;
        // Main interior.
        Open(a - _iw, b - _hh, a + _ie, b + _hh, ZMain);
        int portY = b - _hh - 4, portE = a + _ie + 4, neX = a + _ie - 3;
        // Lane 1: the north flank, hugging the west edge to the north-west corner (medium, with a narrow bridge).
        Corridor(a, portY, a, a, _wL1, padA: false);
        // Lane 2: the wide boulevard to the middle.
        Corridor(neX, portY, neX, _t2, _wL2, padA: false);
        Corridor(neX, _t2, _t2, _t2, _wL2);
        // Lane 3: the equator, along the start's own row to the diagonal corner (b, b).
        Corridor(portE, b, b, b, _wL3, padA: false);
        // North-west courtyard, inside the river's corner.
        int box = _ry - 3;
        Open(3, 3, box, box, ZPlaza, false);
        // Natural plaza beside the east gap (south of the equator in 2 players, north of it in 4).
        int ny0 = _players == 2 ? b + 9 : b - 19, ny1 = _players == 2 ? b + 19 : b - 9;
        Open(portE, ny0, portE + 11, ny1, ZPlaza, false);
        // The natural is a pocket off the apron: a 5-wide mouth from the equator keeps the lane itself walled.
        Corridor(portE + 3, _players == 2 ? b + 2 : b - 2, portE + 3, _players == 2 ? ny0 + 1 : ny1 - 1, 5, padA: false, padB: false);
        // South-east yards for the contested pair, joined to the equator corner by a short street each.
        int px0 = b + 7, px1 = Math.Min(n - 4, b + 20);
        Open(px0, b - 8, px1, b + 3, ZPlaza, false);
        Corridor(b, b - 3, px0, b - 3, 5);
        // Centre plaza on the diagonal where the boulevard turns.
        int cx = Math.Max(_t2 + 3, n / 2 - 1);
        Open(cx - 5, cx - 5, cx + 5, cx + 5, ZPlaza, false);

    }

    private void Shell()
    {
        int a = _sa, b = _sb, n = _n;
        int ix0 = a - _iw, ix1 = a + _ie, iy0 = b - _hh, iy1 = b + _hh;
        int ox0 = ix0 - 3, ox1 = ix1 + 3, oy0 = iy0 - 3, oy1 = iy1 + 3;
        if (ox0 <= 6) ox0 = 0; if (oy0 <= 6) oy0 = 0; if (ox1 >= n - 7) ox1 = n - 1; if (oy1 >= n - 7) oy1 = n - 1;
        for (int y = oy0; y <= oy1; y++)
            for (int x = ox0; x <= ox1; x++)
            {
                if (x >= ix0 && x <= ix1 && y >= iy0 && y <= iy1) continue;
                bool outer = x == ix0 - 3 || x == ix1 + 3 || y == iy0 - 3 || y == iy1 + 3;
                _g.SetLand(_g.Idx(x, y), outer && _rng.Next(100) < 30 ? LandType.Rough : LandType.Rock);
                foreach (int j in _g.Images(_g.Idx(x, y))) _zone[j] = 0;
            }
        // Entrances through the three wall layers.
        int hiN = _gapN / 2, loN = (_gapN - 1) / 2;
        if (_mode == 0) Open(a - loN, iy0 - 3, a + hiN, iy0 - 1, ZLane);
        if (_mode != 1) Open(a + _ie - 3 - (_gapNE - 1) / 2, iy0 - 3, a + _ie - 3 + _gapNE / 2, iy0 - 1, ZLane);
        Open(ix1 + 1, b - (_gapE - 1) / 2, ix1 + 3, b + _gapE / 2, ZLane);
        if (_mode == 0) return;
        // Walled main: the north lanes reach the single ramp through a service street along the north face of the wall.
        int portY = iy0 - 4, portE = ix1 + 4;
        Corridor(portE + 2, b, portE + 2, portY - 2, 5, padA: false);
        Corridor(portE + 2, portY - 2, a, portY - 2, 5);
    }

    // ---- lane linings ---------------------------------------------------------------------------

    /// <summary>Rubble walls along the two medium lanes so that they read as streets: chunks 9 to 16 long, 3 thick, with 3 to 5 wide
    /// breaks between them. Only cells that are still untouched ground are walled.</summary>
    private void Lining()
    {
        int a = _sa, b = _sb;
        int portY = b - _hh - 4, portE = a + _ie + 4;
        int neX = a + _ie - 3;
        LineSegment(a, portY - 1, a, a + 2, _wL1);          // lane 1, vertical
        LineSegment(portE + 1, b, b - 2, b, _wL3);          // lane 3, horizontal
        LineSegment(neX, portY - 1, neX, _t2 + 1, _wL2, 30);    // lane 2 arms
        LineSegment(neX + 1, _t2, _t2 - 1, _t2, _wL2, 30);
    }

    private void LineSegment(int x0, int y0, int x1, int y1, int w, int skipPct = 0)
    {
        bool vertical = x0 == x1;
        int lo = (w - 1) / 2, hi = w / 2;
        int from = vertical ? Math.Min(y0, y1) : Math.Min(x0, x1), to = vertical ? Math.Max(y0, y1) : Math.Max(x0, x1);
        for (int side = 0; side < 2; side++)
        {
            int t = from + _rng.Next(0, 6);
            while (t < to)
            {
                int len = _rng.Next(10, 19);
                bool skip = _rng.Next(100) < skipPct;
                for (int u = t; !skip && u < Math.Min(to, t + len); u++)
                    for (int k = 1; k <= 3; k++)
                    {
                        int off = side == 0 ? -(lo + k) : hi + k;
                        int x = vertical ? x0 + off : u, y = vertical ? u : y0 + off;
                        if (!_g.In(x, y)) continue;
                        int i = _g.Idx(x, y);
                        if (_g.All(i, j => _zone[j] == 0 && _g.Land[j] is LandType.Clear or LandType.Rough or LandType.Rock))
                            _g.SetLand(i, k == 3 && _rng.Next(100) < 35 ? LandType.Rough : LandType.Rock);
                    }
                t += len + _rng.Next(4, 7);
            }
        }
    }

    // ---- the river ------------------------------------------------------------------------------

    private void River()
    {
        int n = _n, ry = _ry, end = _riverEnd;
        var off = new int[end + 1];
        int cur = 0;
        for (int x = 0; x <= end; x++)
        {
            if (x % 3 == 0 && _rng.Next(5) == 0) cur = Math.Clamp(cur + (_rng.Next(2) == 0 ? -1 : 1), -1, 1);
            off[x] = cur;
        }
        int ford0 = end - 6, ford1 = end - 2;
        var deck = new List<int>();
        int a = _sa;
        for (int x = 0; x <= end; x++)
        {
            int yc = ry + off[x];
            for (int dy = -2; dy <= 2; dy++)
            {
                int y = yc + dy;
                if (y < 0 || y >= n) continue;
                int i = _g.Idx(x, y);
                bool rim = Math.Abs(dy) == 2;
                bool ford = x >= ford0 && x <= ford1;
                // The lane-1 bridge keeps its width; the rest of the lane is water.
                bool onDeck = x >= a - (_bridgeW - 1) / 2 && x <= a + _bridgeW / 2;
                if (onDeck) { _g.SetLand(i, LandType.Road); continue; }
                _g.SetLand(i, rim || ford ? LandType.Shallow : LandType.Water);
                _g.MarkRiver(i);
            }
        }
        _ = deck;
    }

    // ---- clean-up -------------------------------------------------------------------------------

    private void Cleanup()
    {
        int n = _n, total = n * n;
        // Rock specks (under 9 cells) become open ground.
        var comp = new int[total];
        var stack = new Stack<int>();
        var cells = new List<int>();
        int id = 0;
        for (int s = 0; s < total; s++)
        {
            if (comp[s] != 0 || _g.Land[s] != LandType.Rock) continue;
            id++; cells.Clear();
            comp[s] = id; stack.Push(s);
            while (stack.Count > 0)
            {
                int c = stack.Pop(); cells.Add(c);
                int x = c % n, y = c / n;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (!_g.In(nx, ny)) continue;
                    int j = _g.Idx(nx, ny);
                    if (comp[j] != 0 || _g.Land[j] != LandType.Rock) continue;
                    comp[j] = id; stack.Push(j);
                }
            }
            if (cells.Count < 9) foreach (int c in cells) _g.SetLand(c, LandType.Clear);
        }
        // Gaps one or two cells wide between rock (alleys narrower than 3) are sealed.
        for (int pass = 0; pass < 2; pass++)
            for (int dir = 0; dir < 2; dir++)
                for (int u = 0; u < n; u++)
                    for (int v = 1; v < n - 1; v++)
                    {
                        int x = dir == 0 ? v : u, y = dir == 0 ? u : v;
                        if (!_g.In(x, y)) continue;
                        int i = _g.Idx(x, y);
                        if (_g.Land[i] == LandType.Rock) continue;
                        int run = 0;
                        int px = x, py = y;
                        while (run < 3 && _g.In(px, py) && _g.Land[_g.Idx(px, py)] is LandType.Clear or LandType.Rough && _zone[_g.Idx(px, py)] != ZLane)
                        { run++; if (dir == 0) px++; else py++; }
                        if (run > 2 || !_g.In(px, py) || _g.Land[_g.Idx(px, py)] != LandType.Rock) continue;
                        int bx = dir == 0 ? x - 1 : x, by = dir == 0 ? y : y - 1;
                        if (!_g.In(bx, by) || _g.Land[_g.Idx(bx, by)] != LandType.Rock) continue;
                        for (int k = 0; k < run; k++)
                            _g.SetLand(dir == 0 ? _g.Idx(x + k, y) : _g.Idx(x, y + k), LandType.Rock);
                    }
    }

    // ---- roads ----------------------------------------------------------------------------------

    private void Roads2()
    {
        int a = _sa, b = _sb;
        int portE = a + _ie + 4, neX = a + _ie - 3;
        RoadLine(portE, b, b, b);
        RoadLine(neX, b - _hh - 4, neX, _t2);
        RoadLine(neX, _t2, _t2, _t2);

    }

    private void RoadLine(int x0, int y0, int x1, int y1)
    {
        int xa = Math.Min(x0, x1), xb = Math.Max(x0, x1), ya = Math.Min(y0, y1), yb = Math.Max(y0, y1);
        for (int y = ya; y <= yb + (x0 == x1 ? 0 : 1); y++)
            for (int x = xa; x <= xb + (y0 == y1 ? 0 : 1); x++)
            {
                if (!_g.In(x, y)) continue;
                int i = _g.Idx(x, y);
                if (_g.Land[i] == LandType.Clear && _g.Overlay[i] == OverlayKind.None && !_g.River[i]) _g.SetLand(i, LandType.Road);
            }
    }

    // ---- resource fields ------------------------------------------------------------------------

    /// <summary>Chebyshev distance of every cell to the nearest blocking terrain or the map edge (8-way multi-source BFS).</summary>
    private int[] WallDistances()
    {
        int n = _n, total = n * n;
        var d = new int[total];
        var q = new int[total];
        int head = 0, tail = 0;
        for (int i = 0; i < total; i++)
        {
            LandType l = _g.Land[i];
            if (l is LandType.Rock or LandType.Water or LandType.Cliff) { d[i] = 0; q[tail++] = i; }
            else d[i] = int.MaxValue;
        }
        for (int e = 0; e < n; e++)
            foreach (int i in new[] { e, (n - 1) * n + e, e * n, e * n + n - 1 })
                if (d[i] == int.MaxValue) { d[i] = 1; q[tail++] = i; }
        while (head < tail)
        {
            int i = q[head++];
            int x = i % n, y = i / n;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (!_g.In(nx, ny)) continue;
                    int j = _g.Idx(nx, ny);
                    if (d[j] <= d[i] + 1) continue;
                    d[j] = d[i] + 1; q[tail++] = j;
                }
        }
        return d;
    }

    private bool NearLane(int x, int y, int r)
    {
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (_g.In(x + dx, y + dy) && _zone[_g.Idx(x + dx, y + dy)] == ZLane) return true;
        return false;
    }

    /// <summary>Grows an elongated field of about <paramref name="target"/> cells inside the rectangle, hugging walls (cells 1..dcap from
    /// blocking terrain), away from lanes. Returns the cells (first image only) or an empty list.</summary>
    private List<int> GrowField(int x0, int y0, int x1, int y1, int target, int dcap, int minStart, byte zone)
    {
        var best = new List<int>();
        for (int round = 0; round < 3 && best.Count < target * 7 / 10; round++)
        {
            int cap = dcap + round;
            var cand = new List<int>();
            for (int y = Math.Max(1, y0); y <= Math.Min(_n - 2, y1); y++)
                for (int x = Math.Max(1, x0); x <= Math.Min(_n - 2, x1); x++)
                {
                    int i = _g.Idx(x, y);
                    if (_zone[i] != zone || _g.Land[i] != LandType.Clear || _g.Overlay[i] != OverlayKind.None || _g.Core[i] || _g.River[i]) continue;
                    if (_wallDist[i] < 1 || _wallDist[i] > cap) continue;
                    if (NearLane(x, y, 2) || MinStartDistance(x, y) < minStart) continue;
                    bool imagesOk = true;
                    foreach (int j in _g.Images(i)) if (_g.Overlay[j] != OverlayKind.None || _g.Land[j] != LandType.Clear) { imagesOk = false; break; }
                    if (!imagesOk) continue;
                    cand.Add(i);
                }
            if (Environment.GetEnvironmentVariable("MAPGEN_DEBUG") == "1")
            {
                int zc = 0, lc = 0, wc = 0;
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) { int i = _g.Idx(x, y); if (_zone[i] == zone) zc++; if (_g.Land[i] == LandType.Clear) lc++; if (_wallDist[i] >= 1 && _wallDist[i] <= cap) wc++; }
                Console.Error.WriteLine($"grow rect {x0},{y0}-{x1},{y1} cap {cap}: cand {cand.Count} zone {zc} clear {lc} wall {wc}");
            }
            if (cand.Count == 0) continue;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var chosen = new List<int>();
                var inSet = new HashSet<int>(cand);   // membership only; never iterated
                var frontier = new List<int> { cand[_rng.Next(cand.Count)] };
                while (chosen.Count < target && frontier.Count > 0)
                {
                    int pick = _rng.Next(frontier.Count);
                    int c = frontier[pick];
                    frontier.RemoveAt(pick);
                    if (!inSet.Remove(c)) continue;
                    // Cells whose images overlap the field itself would double up; keep the first image only.
                    bool clash = false;
                    foreach (int j in _g.Images(c)) if (j != c && chosen.Contains(j)) { clash = true; break; }
                    if (clash) continue;
                    chosen.Add(c);
                    int x = c % _n, y = c / _n;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if ((dx != 0 || dy != 0) && _g.In(x + dx, y + dy) && inSet.Contains(_g.Idx(x + dx, y + dy))) frontier.Add(_g.Idx(x + dx, y + dy));
                }
                if (chosen.Count > best.Count) best = chosen;
                if (best.Count >= target) break;
            }
        }
        return best;
    }

    /// <summary>An elongated band (3 to 6 thick, along the longer side of the rectangle, ragged ends) for plazas that have no wall to
    /// hug. Cells are chosen one by one, so a lane or a rock inside the rectangle only trims the band. Returns the cells (first image only).</summary>
    private List<int> StripField(int x0, int y0, int x1, int y1, int target, byte zone, int minStart)
    {
        bool alongX = x1 - x0 >= y1 - y0;
        for (int thick = 3; thick <= 6; thick++)
        {
            int span = (alongX ? y1 - y0 : x1 - x0) + 1;
            if (thick > span) break;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int off = _rng.Next(0, span - thick + 1);
                var ranked = new List<(int Key, int Cell)>();
                int mid = alongX ? (x0 + x1) / 2 : (y0 + y1) / 2;
                for (int v = 0; v < thick; v++)
                    for (int u = 0; u <= (alongX ? x1 - x0 : y1 - y0); u++)
                    {
                        int x = alongX ? x0 + u : x0 + off + v, y = alongX ? y0 + off + v : y0 + u;
                        if (!_g.In(x, y)) continue;
                        int i = _g.Idx(x, y);
                        if (_zone[i] != zone || _g.Land[i] != LandType.Clear || _g.Overlay[i] != OverlayKind.None || _g.Core[i] || _g.River[i] || MinStartDistance(x, y) < minStart) continue;
                        bool ok = true;
                        foreach (int j in _g.Images(i)) if (j != i && _g.Overlay[j] != OverlayKind.None) { ok = false; break; }
                        if (!ok) continue;
                        ranked.Add((Math.Abs((alongX ? x : y) - mid) * 8 + _rng.Next(8), i));
                    }
                ranked.Sort((p, q) => p.Key != q.Key ? p.Key.CompareTo(q.Key) : p.Cell.CompareTo(q.Cell));
                var cells = new List<int>();
                foreach ((int key, int cell) in ranked)
                {
                    if (cells.Count >= target) break;
                    bool clash = false;
                    foreach (int j in _g.Images(cell)) if (j != cell && cells.Contains(j)) { clash = true; break; }
                    if (!clash) cells.Add(cell);
                }
                if (cells.Count >= target * 7 / 10) return cells;
            }
        }
        return new List<int>();
    }

    private List<int> MakeField(int x0, int y0, int x1, int y1, int target, int dcap, int minStart, byte zone)
    {
        List<int> f = GrowField(x0, y0, x1, y1, target, dcap, minStart, zone);
        if (f.Count < target * 7 / 10) f = StripField(x0, y0, x1, y1, target, zone, minStart);
        if (f.Count == 0 && Environment.GetEnvironmentVariable("MAPGEN_DEBUG") == "1") Console.Error.WriteLine($"field failed rect {x0},{y0}-{x1},{y1} target {target} zone {zone}");
        return Largest(f);
    }

    /// <summary>The largest 8-connected piece of a cell list (stray cells would become one-cell fields).</summary>
    private List<int> Largest(List<int> cells)
    {
        var seen = new bool[cells.Count];
        List<int> best = new();
        for (int s = 0; s < cells.Count; s++)
        {
            if (seen[s]) continue;
            var comp = new List<int>(); var stack = new Stack<int>();
            seen[s] = true; stack.Push(s);
            while (stack.Count > 0)
            {
                int c = stack.Pop(); comp.Add(cells[c]);
                for (int o = 0; o < cells.Count; o++)
                    if (!seen[o] && Math.Abs(cells[o] % _n - cells[c] % _n) <= 1 && Math.Abs(cells[o] / _n - cells[c] / _n) <= 1) { seen[o] = true; stack.Push(o); }
            }
            if (comp.Count > best.Count) best = comp;
        }
        return best;
    }

    private void PlaceField(List<int> cells, int pitchCells)
    {
        for (int k = 0; k < cells.Count; k++)
        {
            bool pitch = k >= cells.Count - pitchCells;
            _g.SetResource(cells[k], pitch ? LandType.Clear : LandType.Ash, pitch ? OverlayKind.Pitchblende : OverlayKind.Ashfall, 1);
        }
    }

    private void FieldsZoned()
    {
        int a = _sa, b = _sb, n = _n;
        bool gems = _rng.Next(2) == 0;
        int fs = Math.Max(80, Math.Min(100, _scale));   // field sizes shrink on Small maps (percent)
        // Main: three strips along the walls.
        int ix0 = a - _iw, ix1 = a + _ie, iy0 = b - _hh, iy1 = b + _hh;
        var f1 = MakeField(a - 6, b + 2, ix1, iy1, _rng.Next(48, 60) * fs / 100, 2, 5, ZMain);
        PlaceField(f1, gems ? 0 : 0);
        var f2 = MakeField(a + 2, iy0, ix1, b - 2, _rng.Next(36, 46) * fs / 100, 2, 5, ZMain);
        PlaceField(f2, gems ? 8 : 0);
        var f3 = MakeField(a - 8, iy0, a + 6, iy1, _rng.Next(24, 34) * fs / 100, 2, 6, ZMain);
        PlaceField(f3, 0);
        // Natural.
        int portE = a + _ie + 4;
        if (_natural)
        {
            int ny0 = _players == 2 ? b + 9 : b - 19, ny1 = _players == 2 ? b + 19 : b - 9;
            var nat = MakeField(portE, ny0, portE + 11, ny1, _rng.Next(45, 66) * fs / 100, 3, 8, ZPlaza);
            PlaceField(nat, 0);
        }
        // Contested: the north-west courtyard, the centre, and the south-east pair.
        int box = _ry - 3;
        PlaceField(MakeField(6, 6, box, box, _rng.Next(30, 40), 3, 8, ZPlaza), 0);
        int cx = Math.Max(_t2 + 3, n / 2 - 1);
        PlaceField(MakeField(cx - 1, cx - 1, cx + 5, cx + 5, _rng.Next(30, 38), 6, 8, ZPlaza), 6);
        PlaceField(MakeField(b + 7, b - 8, Math.Min(n - 4, b + 20), b + 3, _rng.Next(36, 48), 3, 8, ZPlaza), 0);
        ScaleDensity();
    }

    /// <summary>Sets the density of every resource cell so that the money on the map per start matches layout 1
    /// (<see cref="LayoutChecks.MoneyTarget"/>). Cells of one symmetry orbit share a value; each value is chosen from what is left to
    /// spend, so the total is hit within about one cell's worth. Pitchblende counts double (it pays double).</summary>
    private void ScaleDensity()
    {
        int total = _n * _n;
        long weight = 0;
        for (int i = 0; i < total; i++)
            if (OverlayKinds.IsResource(_g.Overlay[i])) weight += _g.Overlay[i] == OverlayKind.Pitchblende ? 2 : 1;
        if (weight == 0) return;
        long left = LayoutChecks.MoneyTarget(_n, _players) * _orbitStarts;
        long leftWeight = weight;
        for (int i = 0; i < total; i++)
        {
            if (!OverlayKinds.IsResource(_g.Overlay[i])) continue;
            int low = int.MaxValue;
            foreach (int j in _g.Images(i)) low = Math.Min(low, j);
            if (low != i) continue;
            var seen = new List<int>();
            foreach (int j in _g.Images(i)) if (!seen.Contains(j)) seen.Add(j);
            int w = (_g.Overlay[i] == OverlayKind.Pitchblende ? 2 : 1) * seen.Count;
            int d = (int)Math.Clamp((left + leftWeight / 2) / leftWeight, 1, 12);
            left -= (long)d * w; leftWeight -= w;
            foreach (int j in seen) _g.Density[j] = (byte)d;
        }
    }
}
