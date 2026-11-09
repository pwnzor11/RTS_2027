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
    private const byte ZMain = 1, ZLane = 2, ZPlaza = 3, AnyZone = 255;
    private int Hw => _n >= 96 ? 3 : 2;   // half width of the river in cells of x - y (the outer ring is shallow)
    private int _buildPct, _wallPct, _lotPct;   // percent of street-grid blocks that hold a ruined building, two standing walls, a rubble lot (the rest stays open)
    private int _gapLo, _gapHi, _offLo, _offHi;   // street pitch (gap between streets) and the offset of the first street

    private readonly bool _zoned;
    private readonly int _players;
    private int _orbitStarts => OrbitStarts.Length;
    private int _sa, _sb;                  // S0 = (a, b)
    private int _mode;                     // 0 soft shell, 1 walled main (one ramp), 2 two gates
    private bool _natural;                 // a natural expansion on this seed
    private int _iw, _ie, _hh;             // main interior: cells west and east of the start, half height
    private int _gapN, _gapNE, _gapE;      // entrance widths
    private int _wL1, _wL2, _wL3, _bridgeW, _bridgeW2, _t2, _l1x;   // lane widths, the two deck widths, the ford corner, lane 1's column
    private byte[] _zone = Array.Empty<byte>();
    private int[] _wallDist = Array.Empty<int>();
    private readonly List<string> _zoneNotes = new();

    /// <summary>Main interior half sizes used by the checks: the shell is the ring from <c>MainRadius</c> out (see <see cref="LayoutChecks"/>).</summary>
    public IReadOnlyList<string> ZoneNotes => _zoneNotes;

    /// <summary>Layout 2 builder: <paramref name="players"/> 2 = mirror_diag with two starts, 3 or 4 = four starts (dihedral images).</summary>
    public ProceduralBuilder(BiomeParams biome, int n, int players, ulong seed) : this(biome, n, players, seed, false) { }

    /// <summary>As above; with <paramref name="rot2"/> (2 players, Medium and up) the half-turn skeleton with an S-shaped river is built instead.</summary>
    public ProceduralBuilder(BiomeParams biome, int n, int players, ulong seed, bool rot2, bool sea = false)
    {
        _sea = sea;
        _b = biome; _n = n; _scale = n * 100 / 96; _decorate = true; _zoned = true; _players = players; _rot2 = rot2;
        _g = new GenGrid(n, rot2 ? MapSymmetry.Rot2 : MapSymmetry.MirrorDiag, LandType.Clear, players > 2);   // built on clear ground; Ash Plains turns it to ash at the end
        _skin = SkinOf(biome.Key);
        _rng = new SimRandom(seed);
        bool big = n >= 96;
        // Street grid and block fill profile (per seed): pitch 10..20, ruins / yards / plazas.
        int pitch = _rng.Next(3);
        (_gapLo, _gapHi) = pitch == 0 ? (7, 11) : pitch == 1 ? (10, 14) : (13, 17);
        (_offLo, _offHi) = (_rng.Next(2, 6), 0); _offHi = _offLo + _rng.Next(2, 8);
        int fill = _rng.Next(3);
        (_buildPct, _wallPct, _lotPct) = !big ? (0, 3, 40) : _players > 2 ? (2, 5, 36) : fill == 0 ? (5, 6, 26) : fill == 1 ? (2, 4, 40) : (3, 5, 14);
        if (rot2) { OrbitStarts = InitRot2(); return; }
        _sa = Math.Max(9, n * (players > 2 ? _rng.Next(14, 21) : _rng.Next(13, 23)) / 100);
        if (_sea) _sa = Math.Min(_sa, Math.Max(9, LayoutChecks.MainRect(n, players, false).West + 9));   // the wall must reach the map edge: the sea goes behind the main
        _sb = players == 2 ? _sa + n * _rng.Next(47, 59) / 100 : n - 1 - _sa - n * (n >= 112 ? _rng.Next(16, 19) : n < 80 ? _rng.Next(29, 33) : _rng.Next(22, 25)) / 100;
        int m = _rng.Next(12);
        _mode = m < 8 ? 0 : m < 11 ? 1 : 2;
        _natural = players == 2 && _rng.Next(2) == 0;
        _iw = big ? 8 : 7; _ie = big ? 16 : 13; _hh = big ? 8 : 7;
        if (players > 2) { _iw = 6; _ie = big ? 11 : 9; _hh = big ? 8 : 7; }
        _wL1 = _rng.Next(6, 9); _wL2 = players > 2 ? _rng.Next(12, 15) : big ? _rng.Next(18, 21) : _rng.Next(13, 16); _wL3 = _rng.Next(5, 8);
        _bridgeW = _rng.Next(3, 5); _bridgeW2 = _rng.Next(4, 6);
        _gapN = _rng.Next(4, 7); _gapNE = _rng.Next(4, 7); _gapE = _rng.Next(4, 7);
        if (_mode != 0) { _gapE = _rng.Next(4, 7); _gapNE = _rng.Next(4, 7); }
        _t2 = (n - 1) / 2 + (players > 2 ? _rng.Next(-2, 3) : _rng.Next(-6, 7));
        _l1x = players > 2 ? _sa - 4 : _sa;
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
        if (_rot2) return BuildRot2(name, starts);
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
        Skin();
        var map = new MapData(name, _b.Theater, n, n, new ushort[n * n], _g.Land, _g.Height, _g.Overlay, _g.Density, starts);
        DecorateProps(map);
        FinishLand();
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
        List<(int Lo, int Hi)> xs = Segments(Streets());
        List<(int Lo, int Hi)> ys = _rot2 ? Segments(Streets()) : xs;   // the diagonal mirror needs the same grid on both axes; the half turn does not
        foreach ((int yl, int yh) in ys)
            foreach ((int xl, int xh) in xs) Block(xl, yl, xh, yh, 0);
    }

    /// <summary>Street positions of one axis, mirrored about the middle (jittered pitch and offset per seed).</summary>
    private List<(int Lo, int Hi)> Streets()
    {
        int n = _n;
        var lo = new List<int>(); var wd = new List<int>();
        int pos = _rng.Next(_offLo, _offHi + 1);
        while (true)
        {
            int w = _rng.Next(10) < 7 ? 3 : _rng.Next(4, 6);
            if (pos + w + 6 > n / 2) break;
            lo.Add(pos); wd.Add(w);
            pos += w + _rng.Next(_gapLo, _gapHi + 1);
        }
        var streets = new List<(int Lo, int Hi)>();
        for (int k = 0; k < lo.Count; k++) streets.Add((lo[k], lo[k] + wd[k] - 1));
        for (int k = lo.Count - 1; k >= 0; k--) streets.Add((n - 1 - (lo[k] + wd[k] - 1), n - 1 - lo[k]));
        return streets;
    }

    private List<(int Lo, int Hi)> Segments(List<(int Lo, int Hi)> streets)
    {
        int n = _n;
        var segs = new List<(int Lo, int Hi)>();
        int from = 0;
        foreach ((int sl, int sh) in streets)
        {
            if (sl - 1 >= from) segs.Add((from, sl - 1));
            from = sh + 1;
        }
        if (from <= n - 1) segs.Add((from, n - 1));
        return segs;
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
        if (SkinBlock(x0, y0, x1, y1, roll)) return;
        if (roll < _buildPct && Math.Min(w, h) >= 4) Building(x0, y0, x1, y1);
        else if (roll < _buildPct + _lotPct) Lot(x0, y0, x1, y1);
        else if (roll < _buildPct + _lotPct + _wallPct && Math.Min(w, h) >= 5) RuinedWall(x0, y0, x1, y1);
    }

    /// <summary>A ruined building: a rubble footprint flush with the street on one or two sides, chipped corners, sometimes an L.</summary>
    private void Building(int x0, int y0, int x1, int y1)
    {
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        int rw = Math.Min(9, Math.Max(3, w - _rng.Next(0, 3))), rh = Math.Min(9, Math.Max(3, h - _rng.Next(0, 3)));
        int fx0 = _rng.Next(2) == 0 ? x0 : x1 - rw + 1, fy0 = _rng.Next(2) == 0 ? y0 : y1 - rh + 1;
        int fx1 = fx0 + rw - 1, fy1 = fy0 + rh - 1;
        int nw = rw >= 6 && rh >= 6 && _rng.Next(2) == 0 ? rw / 3 : 0, nh = nw == 0 ? 0 : rh / 3;
        int cornerX = _rng.Next(2) == 0 ? fx0 : fx1 - nw + 1, cornerY = _rng.Next(2) == 0 ? fy0 : fy1 - nh + 1;
        for (int y = fy0 - 1; y <= fy1 + 1; y++)
            for (int x = fx0 - 1; x <= fx1 + 1; x++)
            {
                if (x < x0 || x > x1 || y < y0 || y > y1 || !_g.In(x, y)) continue;
                bool inside = x >= fx0 && x <= fx1 && y >= fy0 && y <= fy1;
                bool notch = nw > 0 && x >= cornerX && x < cornerX + nw && y >= cornerY && y < cornerY + nh;
                bool corner = (x == fx0 || x == fx1) && (y == fy0 || y == fy1);
                if (inside && !notch && !(corner && _rng.Next(3) == 0)) _g.SetLand(_g.Idx(x, y), LandType.Rock);
                else if ((inside && notch) || (!inside && _rng.Next(100) < 22)) _g.SetLand(_g.Idx(x, y), LandType.Rough);
            }
    }

    /// <summary>A rubble lot: a rectangle of rough ground with a ragged edge.</summary>
    private void Lot(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                bool edge = x == x0 || x == x1 || y == y0 || y == y1;
                if (!edge || _rng.Next(100) < 55) _g.SetLand(_g.Idx(x, y), LandType.Rough);
            }
    }

    /// <summary>Two standing walls of a ruined house (an L along two sides of the block, 2 thick), the rest open.</summary>
    private void RuinedWall(int x0, int y0, int x1, int y1)
    {
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        bool top = _rng.Next(2) == 0, left = _rng.Next(2) == 0;
        int lenX = Math.Max(3, w * _rng.Next(65, 101) / 100), lenY = Math.Max(3, h * _rng.Next(65, 101) / 100);
        for (int k = 0; k < lenX; k++)
            for (int t = 0; t < 2; t++)
            {
                int x = left ? x0 + k : x1 - k, y = top ? y0 + t : y1 - t;
                _g.SetLand(_g.Idx(x, y), LandType.Rock);
            }
        for (int k = 0; k < lenY; k++)
            for (int t = 0; t < 2; t++)
            {
                int x = left ? x0 + t : x1 - t, y = top ? y0 + k : y1 - k;
                _g.SetLand(_g.Idx(x, y), LandType.Rock);
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
        Open(a - _iw, b - _hh, a + _ie, b + _hh, ZMain);
        int portY = b - _hh - 4, portE = a + _ie + 4, neX = a + _ie - 3;
        // Lane 1: the north flank up the west side to the north-west deck (medium, with a narrow deck).
        Corridor(_l1x, portY, _l1x, _l1x, _wL1, padA: false);
        // Lane 2: the wide boulevard to the ford at the centre of the river.
        Corridor(neX, portY, neX, _t2, 7, padA: false);
        Corridor(neX, _t2, _t2, _t2, _wL2, padA: false);
        // Lane 3: the equator along the start's own row to the south-east deck at (b, b).
        Corridor(portE, b, b, b, _wL3, padA: false);
        // The ford plaza at the centre of the river.
        int cx = _t2;
        int ph = _players > 2 ? 8 : 11;
        Open(cx - ph, cx - ph, cx + ph, cx + ph, ZPlaza, false);
        if (_players > 2) return;
        // Natural pocket beside the east gap, south of the equator (2 players only: with more players the mains are too close).
        int ny0 = _players == 2 ? b + 9 : b - 19, ny1 = _players == 2 ? b + 19 : b - 9;
        Open(portE, ny0, portE + 11, ny1, ZPlaza, false);
        Corridor(portE + 3, _players == 2 ? b + 2 : b - 2, portE + 3, _players == 2 ? ny0 + 1 : ny1 - 1, 5, padA: false, padB: false);
        Ring(portE, ny0, portE + 11, ny1, true);
        // South-east yards beyond the deck, one on each side of the river, for the contested pair.
        int px0 = b + 7, px1 = Math.Min(n - 4, b + 20);
        Open(px0, b - 8, px1, b + 3, ZPlaza, false);
        Corridor(b, b - 3, px0, b - 3, 5);
        Ring(px0, b - 8, px1, b + 3);
        // North-west yards beside the first deck (Medium and up): between lane 1 and the map edge, one on each side of the river.
        if (n >= 96)
        {
            int top = _l1x - (_wL1 - 1) / 2, yy1 = top - 4;
            Open(a + 4, 2, a + 16, yy1, ZPlaza, false);
            Corridor(a + 10, top - 1, a + 10, yy1, 5, padA: false, padB: false);
            Ring(a + 4, 2, a + 16, yy1);
        }
    }

    /// <summary>A one-cell rubble wall round a rectangle (ruined courtyard wall); cells already tagged as lane, main or plaza stay open.</summary>
    private void Ring(int x0, int y0, int x1, int y1, bool plateau = false)
    {
        if (plateau && _skin == SkinMountains) _plateaus.Add((x0, y0, x1, y1));
        if (_n < 96) return;   // Small maps have no room (or rock budget) for courtyard walls
        // One 4-wide gap in each of two sides picked by the rng; the rest of the wall stands.
        int gapSide1 = _rng.Next(4), gapSide2 = (gapSide1 + 1 + _rng.Next(3)) % 4;
        int g1 = _rng.Next(3, 9), g2 = _rng.Next(3, 9);
        for (int y = y0 - 1; y <= y1 + 1; y++)
            for (int x = x0 - 1; x <= x1 + 1; x++)
            {
                if (!_g.In(x, y) || (x >= x0 && x <= x1 && y >= y0 && y <= y1)) continue;
                int side = y < y0 ? 0 : y > y1 ? 1 : x < x0 ? 2 : 3;
                int along = side < 2 ? x - x0 : y - y0;
                bool corner = (x < x0 || x > x1) && (y < y0 || y > y1);
                if (!corner && ((side == gapSide1 && along >= g1 && along < g1 + 4) || (side == gapSide2 && along >= g2 && along < g2 + 4))) continue;
                int i = _g.Idx(x, y);
                if (!_g.All(i, j => _zone[j] == 0)) continue;
                _g.SetLand(i, LandType.Rock);
            }
    }

    private void Shell()
    {
        int a = _sa, b = _sb, n = _n;
        int ix0 = a - _iw, ix1 = a + _ie, iy0 = b - _hh, iy1 = b + _hh;
        int ox0 = ix0 - 3, ox1 = ix1 + 3, oy0 = iy0 - 3, oy1 = iy1 + 3;
        if (ox0 <= 6) ox0 = 0; if (oy0 <= 6) oy0 = 0; if (ox1 >= n - 7) ox1 = n - 1; if (oy1 >= n - 7) oy1 = n - 1;
        if (_mode == 0 && _players == 2)   // 3-4 players keep the solid ring with three wide gates (their mains are too close for a leaky shell)
            SoftShell(ix0, iy0, ix1, iy1, new List<(int X, int Y, int W)> { (_l1x, iy0 - 2, _gapN), (a + _ie - 3, iy0 - 2, _gapNE), (ix1 + 2, b, _gapE) }, false);
        else
        for (int y = oy0; y <= oy1; y++)
            for (int x = ox0; x <= ox1; x++)
            {
                if (x >= ix0 && x <= ix1 && y >= iy0 && y <= iy1) continue;
                bool outer = x == ix0 - 3 || x == ix1 + 3 || y == iy0 - 3 || y == iy1 + 3;
                bool beyond = x < ix0 - 3 || x > ix1 + 3 || y < iy0 - 3 || y > iy1 + 3;   // the filled strip between the wall and the map edge
                _g.SetLand(_g.Idx(x, y), (outer && (_players > 2 || _rng.Next(100) < 30)) || (beyond && _players > 2 && n < 96) ? LandType.Rough : LandType.Rock);
                foreach (int j in _g.Images(_g.Idx(x, y))) _zone[j] = 0;
            }
        // Entrances through the three wall layers.
        int hiN = _gapN / 2, loN = (_gapN - 1) / 2;
        if (_mode == 0) Open(_l1x - loN, iy0 - 3, _l1x + hiN, iy0 - 1, ZLane);
        if (_mode != 1) Open(a + _ie - 3 - (_gapNE - 1) / 2, iy0 - 3, a + _ie - 3 + _gapNE / 2, iy0 - 1, ZLane);
        Open(ix1 + 1, b - (_gapE - 1) / 2, ix1 + 3, b + _gapE / 2, ZLane);
        if (_mode == 0) return;
        // Walled main: the north lanes reach the single ramp through a service street along the north face of the wall.
        int portY = iy0 - 4, portE = ix1 + 4;
        Corridor(portE + 2, b, portE + 2, portY - 2, 5, padA: false);
        Corridor(portE + 2, portY - 2, _l1x, portY - 2, 5);
    }

    // ---- lane linings ---------------------------------------------------------------------------

    /// <summary>Rubble walls along the two medium lanes so that they read as streets: chunks 9 to 16 long, 3 thick, with 3 to 5 wide
    /// breaks between them. Only cells that are still untouched ground are walled.</summary>
    private void Lining()
    {
        int a = _sa, b = _sb;
        int portY = b - _hh - 4, portE = a + _ie + 4;
        int neX = a + _ie - 3;
        int sk = _players > 2 ? 25 : 0;
        LineSegment(_l1x, portY - 1, _l1x, _l1x + 2, _wL1, sk);          // lane 1, vertical
        LineSegment(portE + 1, b, b - 2, b, _wL3, sk);          // lane 3, horizontal
        LineSegment(neX, portY - 1, neX, _t2 + 1, _wL2, _players > 2 ? 70 : 30);    // lane 2 arms
        LineSegment(neX + 1, _t2, _t2 - 1, _t2, _wL2, _players > 2 ? 70 : 30);
    }

    private void LineSegment(int x0, int y0, int x1, int y1, int w, int skipPct = 0, bool dense = false)
    {
        bool vertical = x0 == x1;
        int lo = (w - 1) / 2, hi = w / 2;
        int from = vertical ? Math.Min(y0, y1) : Math.Min(x0, x1), to = vertical ? Math.Max(y0, y1) : Math.Max(x0, x1);
        for (int side = 0; side < 2; side++)
        {
            int t = from + _rng.Next(0, 6);
            while (t < to)
            {
                int len = dense ? _rng.Next(12, 22) : _rng.Next(8, 15);
                bool skip = _rng.Next(100) < skipPct;
                for (int u = t; !skip && u < Math.Min(to, t + len); u++)
                    for (int k = 1; k <= 2; k++)
                    {
                        int off = side == 0 ? -(lo + k) : hi + k;
                        int x = vertical ? x0 + off : u, y = vertical ? u : y0 + off;
                        if (!_g.In(x, y)) continue;
                        int i = _g.Idx(x, y);
                        if (_g.All(i, j => _zone[j] == 0 && _g.Land[j] is LandType.Clear or LandType.Rough or LandType.Rock))
                            _g.SetLand(i, k == 2 && _rng.Next(100) < 35 ? LandType.Rough : LandType.Rock);
                    }
                t += len + (dense ? _rng.Next(3, 6) : _rng.Next(5, 10));
            }
        }
    }

    // ---- the river ------------------------------------------------------------------------------

    private void River()
    {
        int n = _n, c0 = n - 1;
        int fh = _players > 2 ? 6 : n >= 96 ? 10 : 7;   // ford: half length along the river, in steps of x + y
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int d = x - y;
                if (d < -Hw || d > Hw) continue;
                int i = _g.Idx(x, y);
                bool deck = false;
                foreach (int j in _g.Images(i)) deck |= InDeck(j % n, j / n);
                if (deck) { _g.SetLand(i, LandType.Road); foreach (int j in _g.Images(i)) _g.River[j] = false; continue; }
                int v = Math.Abs(x + y - c0);
                if (v <= 2 * fh)
                {
                    if (v <= (_players > 2 ? 4 : 9)) { _g.SetLand(i, LandType.Clear); foreach (int j in _g.Images(i)) _g.River[j] = false; }   // the gravel bar in the middle of the ford
                    else { _g.SetLand(i, LandType.Shallow); _g.MarkRiver(i); }
                    continue;
                }
                _g.SetLand(i, Math.Abs(d) == Hw ? LandType.Shallow : LandType.Water);
                _g.MarkRiver(i);
            }
    }

    /// <summary>True when (x, y) lies on one of the two decks: a strip across the river along the other diagonal (|x + y - 2c| below the
    /// deck width), self-symmetric under the diagonal mirror, so a deck of width w is exactly w cells wide at its middle.</summary>
    private bool InDeck(int x, int y) => Math.Abs(x + y - 2 * _l1x) <= _bridgeW - 1 || Math.Abs(x + y - 2 * _sb) <= _bridgeW2 - 1;

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
                        if ((zone == AnyZone ? _zone[i] == ZMain : _zone[i] != zone) || _g.Land[i] is not (LandType.Clear or LandType.Road) || _g.Overlay[i] != OverlayKind.None || _g.Core[i] || _g.River[i] || MinStartDistance(x, y) < minStart) continue;
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

    private List<int> MakeField(int x0, int y0, int x1, int y1, int target, byte zone, int minStart) =>
        Largest(StripField(x0, y0, x1, y1, target, zone, minStart));

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
        int fs = Math.Max(80, Math.Min(100, _scale)) * (_players > 2 ? (_n < 80 ? 19 : 13) : 10) / 10;   // field sizes shrink on Small maps (percent)
        int ix0 = a - _iw, ix1 = a + _ie, iy0 = b - _hh, iy1 = b + _hh;
        // Main: a thick band along the south wall and one along the north wall.
        PlaceField(Band(ix0 + 2, iy1 - _hh + (_n < 80 ? 3 : 4), ix1 - 2, iy1 - 1, _rng.Next(56, 72) * fs / 100, ZMain, 4), 0);
        PlaceField(Band(ix0 + 2, iy0 + 1, ix1 - 2, iy0 + _hh - (_n < 80 ? 3 : 4), _rng.Next(44, 60) * fs / 100, ZMain, 4), gems ? (_n < 80 ? 4 : 8) : 0);
        if (_n < 80 && _players > 2) PlaceField(Band(ix0 + 1, iy0 + 1, ix0 + 3, iy1 - 1, _rng.Next(14, 20), ZMain, 3), 0);
        // Natural.
        int portE = a + _ie + 4;
        if (_natural)
        {
            int ny0 = _players == 2 ? b + 9 : b - 19, ny1 = _players == 2 ? b + 19 : b - 9;
            PlaceField(MakeField(portE, ny0, portE + 11, ny1, _rng.Next(45, 66) * fs / 100, ZPlaza, 8), 0);
        }
        // Contested: the south-east yards (one on each side of the river) and the gravel bar at the ford.
        if (n >= 96 && _players == 2) PlaceField(MakeField(a + 4, 2, a + 16, _l1x - (_wL1 - 1) / 2 - 4, _rng.Next(36, 48), ZPlaza, 8), 0);
        if (_players == 2) PlaceField(MakeField(b + 7, b - 8, Math.Min(n - 4, b + 20), b + 3, _rng.Next(36, 48), ZPlaza, 8), 0);
        PlaceField(Bar(), 6);
        ScaleDensity();
    }

    /// <summary>The centre field: the gravel bar of the ford and the banks beside it, an elongated piece along the river (self-symmetric, so it exists once).</summary>
    private List<int> Bar()
    {
        int n = _n, c0 = n - 1;
        var cells = new List<int>();
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int d = x - y, v = x + y - c0;
                if (Math.Abs(d) > Hw + 1 || Math.Abs(v) > (_players > 2 ? 6 : 7)) continue;
                int i = _g.Idx(x, y);
                if (_g.Land[i] is not (LandType.Clear or LandType.Road) || _g.River[i] || _zone[i] == ZMain) continue;
                cells.Add(i);
            }
        var own = new List<int>();
        foreach (int i in cells)
        {
            bool clash = false;
            foreach (int j in _g.Images(i)) if (j < i && cells.Contains(j)) { clash = true; break; }
            if (!clash) own.Add(i);
        }
        return Largest(own);
    }

    /// <summary>A thick band (3 or more cells) in a rectangle along its longer side, with ragged ends; about <paramref name="target"/> cells.</summary>
    private List<int> Band(int x0, int y0, int x1, int y1, int target, byte zone, int minStart)
    {
        bool alongX = x1 - x0 >= y1 - y0;
        int thick = alongX ? y1 - y0 + 1 : x1 - x0 + 1;
        int len = alongX ? x1 - x0 + 1 : y1 - y0 + 1;
        int want = Math.Min(len, (target + thick - 1) / thick);
        int off = len > want ? _rng.Next(0, len - want + 1) : 0;
        var cells = new List<int>();
        for (int u = off; u < off + want; u++)
            for (int v = 0; v < thick; v++)
            {
                int x = alongX ? x0 + u : x0 + v, y = alongX ? y0 + v : y0 + u;
                if (!_g.In(x, y)) continue;
                int i = _g.Idx(x, y);
                if ((zone == AnyZone ? _zone[i] == ZMain : _zone[i] != zone) || _g.Land[i] is not (LandType.Clear or LandType.Road) || _g.Overlay[i] != OverlayKind.None || _g.Core[i] || _g.River[i] || MinStartDistance(x, y) < minStart) continue;
                bool edgeEnd = (u == off || u == off + want - 1) && (v == 0 || v == thick - 1);
                if (edgeEnd && _rng.Next(2) == 0) continue;   // chipped corner
                bool ok = true;
                foreach (int j in _g.Images(i)) if (j != i && (_g.Overlay[j] != OverlayKind.None || cells.Contains(j))) { ok = false; break; }
                if (ok) cells.Add(i);
            }
        return Largest(cells);
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
