// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

// Layout 2, half-turn skeleton (2 players, Medium and up). Canonical frame: start A = (a, y0) on the west side in the upper half, start B is its half
// turn (n-1-a, n-1-y0). One river runs north to south between them as an S (odd about the centre, so the half turn maps it onto itself), with two
// decks (the pair of the deck at row ye) and a ford at the centre. Three lanes join the bases and all are monotone, so they are about equally long:
//   north-deck lane: A's east gate, row ye east across the deck to column n-1-gx2, down that column to B;
//   ford lane: A's south gate, column gx2 down to the middle, east across the plaza and ford, down the east column to B;
//   south-deck lane: column gx2 down to row n-1-ye (B's row), east across the south deck to B's west gate.
// The finished map is then turned by one of the eight square symmetries (LayoutVariant), so the bases may sit left/right, top/bottom or on a diagonal.
internal sealed partial class ProceduralBuilder
{
    private bool _rot2;
    private int _y0, _ye, _gx2, _hw, _fh, _gapS;
    private int[] _riverO = Array.Empty<int>();   // river centre offset (cells) per row, odd about the middle

    private CellCoord[] InitRot2()
    {
        int n = _n;
        (_iw, _ie, _hh) = LayoutChecks.MainRect(n, 2, true);
        int amp = n >= 112 ? 10 : 7;
        _hw = _skin == SkinMountains ? 2 : 3; _fh = 8;
        int riverWest = n / 2 - amp - _hw - 1;
        int amin = (n * 12 + 99) / 100, amax = Math.Min(riverWest - 5 - _ie - 3, n * 22 / 100);
        if (_sea) amax = Math.Min(amax, _iw + 9);   // the wall must reach the map edge: the sea goes behind the main
        _sa = _rng.Next(amin, Math.Max(amin, amax) + 1);
        int sum = n * _rng.Next(44, 58) / 100;
        int y0Max = n * 38 / 100;
        if (_skin == SkinAsh) y0Max = Math.Min(y0Max, n / 2 - _fh - 2 - (2 + CauseGap + CauseRows) - 3);   // Ash: the causeway sits south of the bridge and must end above the ford
        _y0 = Math.Clamp(sum - _sa, Math.Max(_hh + 11, n * 19 / 100), Math.Max(y0Max, Math.Max(_hh + 11, n * 19 / 100)));
        _ye = _y0 + _rng.Next(-3, 4);
        int mr = _rng.Next(12);
        _mode = _skin == SkinMountains ? (mr < 7 ? 2 : 1) : mr < 8 ? 0 : mr < 10 ? 2 : 1;   // 0 soft shell, 2 walled main with two gates (east and south), 1 walled main with one east ramp
        _wL1 = _rng.Next(6, 9); _wL2 = _rng.Next(13, 17); _bridgeW = _rng.Next(3, 6);
        _gapE = _rng.Next(4, 7); _gapS = _rng.Next(4, 7);
        _gx2 = _sa + _rng.Next(-3, 7);
        _natural = _rng.Next(10) < 4;
        // The river: knots in the top half, mirrored with the sign flipped, then smoothed (odd smoothing keeps it odd).
        var o = new int[n];
        int k1 = n / 6, k2 = n / 3, mid = (n - 1) / 2;
        int sg = _rng.Next(2) == 0 ? 1 : -1;   // three bends: the knots alternate in sign
        int r0 = sg * _rng.Next(amp / 3, amp + 1), r1 = -sg * _rng.Next(amp / 2, amp + 1), r2 = sg * _rng.Next(amp / 3, amp + 1);
        for (int y = 0; y <= mid; y++)
        {
            int v = y <= k1 ? r0 + (r1 - r0) * y / k1
                  : y <= k2 ? r1 + (r2 - r1) * (y - k1) / (k2 - k1)
                  : r2 + (0 - r2) * (y - k2) / Math.Max(1, mid + 1 - k2);
            o[y] = v;
        }
        for (int y = 0; y <= mid; y++) o[n - 1 - y] = -o[y];
        _riverO = new int[n];
        for (int pass = 0; pass < 2; pass++)
        {
            var sm = new int[n];
            for (int y = 0; y < n; y++)
            {
                int Q(int i) => o[Math.Clamp(i, 0, n - 1)];
                sm[y] = (Q(y - 2) + 2 * Q(y - 1) + 3 * Q(y) + 2 * Q(y + 1) + Q(y + 2)) / 9;
            }
            o = sm;
        }
        _riverO = o;
        return new[] { new CellCoord(_sa, _y0), new CellCoord(n - 1 - _sa, n - 1 - _y0) };
    }

    private MapData BuildRot2(string name, IReadOnlyList<CellCoord> starts)
    {
        int n = _n;
        CarveOpen2();
        Shell2();
        Lining2();
        River2();
        Cleanup();
        Roads3();
        _wallDist = WallDistances();
        FieldsRot2();
        Ruins();
        StartSquares();
        Skin();
        var map = new MapData(name, _b.Theater, n, n, new ushort[n * n], _g.Land, _g.Height, _g.Overlay, _g.Density, starts);
        DecorateProps(map);
        FinishLand();
        return map;
    }

    private void CarveOpen2()
    {
        int n = _n, a = _sa;
        int ix0 = a - _iw, ix1 = a + _ie, iy0 = _y0 - _hh, iy1 = _y0 + _hh;
        Open(ix0, iy0, ix1, iy1, ZMain);
        int yf = n / 2 - 1, ve = n - 1 - _gx2, cx = n / 2;
        // North-deck lane (its image is the south-deck lane).
        Corridor(ix1 + 4, _ye, ve, _ye, _wL1, padA: false);
        // The west column from the south gate to B's row (the ford lane and the south-deck lane share it as far as the middle).
        Corridor(_gx2, iy1 + 4, _gx2, n - 1 - _ye, _wL2, padA: false);
        // The ford boulevard across the middle, and the plaza round the ford.
        Corridor(_gx2, yf, ve, yf, _wL2);
        Open(cx - _rng.Next(11, 16), cx - _rng.Next(7, 11), cx + _rng.Next(10, 15), cx + _rng.Next(6, 10), ZPlaza, false);
        // Natural (about half of the seeds, R4): a walled yard east of the south lane, reached from it.
        if (_natural)
        {
            int px0 = _gx2 + _wL2 / 2 + 3 + _rng.Next(0, 4), py0 = iy1 + 6 + _rng.Next(0, 5);
            int px1 = px0 + 10 + _rng.Next(0, 3), py1 = py0 + 9 + _rng.Next(0, 3);
            Open(px0, py0, px1, py1, ZPlaza, false);
            Corridor(_gx2 + _wL2 / 2, (py0 + py1) / 2, px0, (py0 + py1) / 2, 5, padA: false);
            Ring(px0, py0, px1, py1, true);
            _natRect = (px0, py0, px1, py1);
        }
        // Contested yards next to the north deck on the east bank (images: the west bank beside the south deck).
        int amp = n >= 112 ? 10 : 7, yx0 = cx + amp + _hw + 3 + _rng.Next(0, 5), yx1 = yx0 + 13 + _rng.Next(0, 4);
        int yy1 = _ye - 5 - _rng.Next(0, 3), yy0 = yy1 - 10 - _rng.Next(0, 4);
        if (_skin == SkinAsh)
        {
            // Both banks flare over the causeway rows so that the pair of crossings is one reachable crossing area, 6 cells deep on each bank.
            for (int y = _ye + _bridgeW / 2 + 1; y < CauseTop + CauseRows; y++)
            {
                int xl = n / 2 + _riverO[y] - _hw, xr = n / 2 + _riverO[y] + _hw - 1;
                Open(xl - 6, y, xl - 1, y, ZLane);
                Open(xr + 1, y, xr + 6, y, ZLane);
            }
        }
        Open(yx0, yy0, yx1, yy1, ZPlaza, false);
        Corridor((yx0 + yx1) / 2, yy1 + 1, (yx0 + yx1) / 2, _ye - 1, 5, padA: false, padB: false);
        Ring(yx0, yy0, yx1, yy1);
        _zoneNotes.Add($"rot2 yard {yx0},{yy0}-{yx1},{yy1}");
        _yardRect = (yx0, yy0, yx1, yy1);
    }

    private (int X0, int Y0, int X1, int Y1) _yardRect, _natRect;

    /// <summary>Ash Plains half turn (Q-68): each deck is a pair, the intact bridge and, <see cref="CauseGap"/> water rows south of it, a broken causeway of
    /// <see cref="CauseRows"/> rows (shallows on both edges, a 3-wide road and rubble way through). The half turn copies the pair to the other deck.</summary>
    private const int CauseGap = 2, CauseRows = 5;

    /// <summary>First row of the broken causeway south of the bridge at row ye (Ash Plains only).</summary>
    private int CauseTop => _ye + _bridgeW / 2 + 1 + CauseGap;

    private void Shell2()
    {
        int n = _n, a = _sa;
        int ix0 = a - _iw, ix1 = a + _ie, iy0 = _y0 - _hh, iy1 = _y0 + _hh;
        int ox0 = ix0 - 3, ox1 = ix1 + 3, oy0 = iy0 - 3, oy1 = iy1 + 3;
        if (ox0 <= 6) ox0 = 0; if (oy0 <= 6) oy0 = 0;
        if (_mode == 0)
            SoftShell(ix0, iy0, ix1, iy1, new List<(int X, int Y, int W)> { (ix1 + 2, _ye, _gapE), (_gx2, iy1 + 2, _gapS) }, true);
        else
        for (int y = oy0; y <= oy1; y++)
            for (int x = ox0; x <= ox1; x++)
            {
                if (x >= ix0 && x <= ix1 && y >= iy0 && y <= iy1) continue;
                bool outer = x == ix0 - 3 || x == ix1 + 3 || y == iy0 - 3 || y == iy1 + 3;
                _g.SetLand(_g.Idx(x, y), outer && _rng.Next(100) < 30 ? LandType.Rough : LandType.Rock);
                foreach (int j in _g.Images(_g.Idx(x, y))) _zone[j] = 0;
            }
        Open(ix1 + 1, _ye - (_gapE - 1) / 2, ix1 + 3, _ye + _gapE / 2, ZLane);
        if (_mode != 1)
        {
            Open(_gx2 - (_gapS - 1) / 2, iy1 + 1, _gx2 + _gapS / 2, iy1 + 3, ZLane);
            return;
        }
        // Walled main: the single east ramp; the south lanes start from a service street down the east face and along the south face.
        int sx = ix1 + 6;
        Corridor(sx, _ye, sx, iy1 + 6, 5, padA: false);
        Corridor(sx, iy1 + 6, _gx2, iy1 + 6, 5);
    }

    private void Lining2()
    {
        int n = _n, a = _sa, ix1 = a + _ie, iy1 = _y0 + _hh;
        int yf = n / 2 - 1, ve = n - 1 - _gx2;
        int cx = n / 2;
        LineSegment(ix1 + 6, _ye, ve - 8, _ye, _wL1, 5, true);
        LineSegment(_gx2, iy1 + 8, _gx2, n - 1 - _ye - 6, _wL2, 55);
        LineSegment(_gx2 + 12, yf, cx - 18, yf, _wL2, 60);
    }

    /// <summary>The S-shaped river with its two decks (rows around ye and the half-turn row) and the ford at the middle.</summary>
    private void River2()
    {
        int n = _n;
        var deckRow = new bool[n];
        int lo = (_bridgeW - 1) / 2, hi = _bridgeW / 2;
        for (int y = _ye - lo; y <= _ye + hi; y++) if (y >= 0 && y < n) { deckRow[y] = true; deckRow[n - 1 - y] = true; }
        var causeRow = new int[n];   // Ash Plains: 1..CauseRows on the rows of the broken causeway (and its half-turn image), else 0
        if (_skin == SkinAsh)
            for (int k = 0; k < CauseRows; k++) { int y = CauseTop + k; causeRow[y] = k + 1; causeRow[n - 1 - y] = k + 1; }
        for (int y = 0; y < n; y++)
        {
            int c2 = 2 * _riverO[y], y2 = Math.Abs(2 * y - (n - 1));
            for (int x = 0; x < n; x++)
            {
                int d = Math.Abs(2 * x - (n - 1) - c2);
                if (d > 2 * _hw - 1) continue;
                int i = _g.Idx(x, y);
                if (deckRow[y])
                {
                    PaintDeck(i, x, y);
                    continue;
                }
                if (causeRow[y] != 0)
                {
                    int k = causeRow[y];
                    bool edge = k == 1 || k == CauseRows;   // the outer row on each side is flooded
                    _g.SetLand(i, edge ? LandType.Shallow : OrbitHash(i, 6) < 40 ? LandType.Rough : LandType.Road);
                    foreach (int j in _g.Images(i)) _g.River[j] = false;
                    continue;
                }
                if (y2 <= 2 * (_fh - 2) - 1)
                {
                    _g.SetLand(i, LandType.Clear);   // the ford: gravel across the whole river (vehicles cross here)
                    foreach (int j in _g.Images(i)) _g.River[j] = false;
                    continue;
                }
                if (y2 <= 2 * _fh - 1) { _g.SetLand(i, _skin == SkinMountains ? LandType.Cliff : LandType.Shallow); _g.MarkRiver(i); continue; }
                _g.SetLand(i, _skin == SkinMountains ? LandType.Cliff : d == 2 * _hw - 1 ? LandType.Shallow : LandType.Water);   // Mountains: a ridge instead of a river
                _g.MarkRiver(i);
            }
        }
    }

    private void Roads3()
    {
        int n = _n, a = _sa, ix1 = a + _ie, iy1 = _y0 + _hh;
        RoadLine(ix1 + 4, _ye, n / 2 - 8, _ye);
        RoadLine(_gx2, iy1 + 4, _gx2, n / 2 - 1);
    }

    private void FieldsRot2()
    {
        int a = _sa, n = _n;
        bool gems = _rng.Next(2) == 0;
        int fs = Math.Max(80, Math.Min(100, _scale));
        int ix0 = a - _iw, ix1 = a + _ie, iy0 = _y0 - _hh, iy1 = _y0 + _hh;
        // Main: two bands (south and north), an L along the west and south walls plus a north band, or a thin west crescent plus two bands (per seed).
        int shape = _rng.Next(3);
        if (shape == 0)
        {
            PlaceField(Band(ix0 + 2, _y0 + 4, ix1 - 2, iy1 - 1, _rng.Next(56, 72) * fs / 100, ZMain, 4), 0);
            PlaceField(Band(ix0 + 2, iy0 + 1, ix1 - 2, _y0 - 4, _rng.Next(44, 60) * fs / 100, ZMain, 4), gems ? 8 : 0);
        }
        else if (shape == 1)
        {
            var l = Band(ix0 + 1, _y0 + 1, ix0 + 4, iy1 - 5, _rng.Next(22, 28) * fs / 100, ZMain, 4);
            foreach (int c in Band(ix0 + 1, iy1 - 4, ix1 - 3, iy1 - 1, _rng.Next(36, 46) * fs / 100, ZMain, 4)) if (!l.Contains(c)) l.Add(c);
            PlaceField(l, 0);
            PlaceField(Band(ix0 + 6, iy0 + 1, ix1 - 2, iy0 + 4, _rng.Next(40, 52) * fs / 100, ZMain, 4), gems ? 8 : 0);
        }
        else
        {
            PlaceField(Band(ix0 + 1, iy0 + 2, ix0 + 4, iy1 - 2, _rng.Next(40, 50) * fs / 100, ZMain, 4), 0);
            PlaceField(Band(ix0 + 6, iy1 - 4, ix1 - 2, iy1 - 1, _rng.Next(32, 40) * fs / 100, ZMain, 4), gems ? 8 : 0);
            PlaceField(Band(ix0 + 6, iy0 + 1, ix1 - 2, iy0 + 4, _rng.Next(24, 30) * fs / 100, ZMain, 4), 0);
        }
        if (_natural) PlaceField(MakeField(_natRect.X0, _natRect.Y0, _natRect.X1, _natRect.Y1, _rng.Next(36, 48) * fs / 100, ZPlaza, 8), 0);
        // Contested: the yard by the north deck (image: by the south deck) and the gravel bar of the ford.
        (int x0, int y0, int x1, int y1) = _yardRect;
        PlaceField(MakeField(x0, y0, x1, y1, _rng.Next(20, 25), ZPlaza, 8), 0);
        PlaceField(Bar2(), 3);
        ScaleDensity();
    }

    /// <summary>The ford's gravel bar: its clear cells, one per symmetry pair.</summary>
    private List<int> Bar2()
    {
        int n = _n;
        var cells = new List<int>();
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = _g.Idx(x, y);
                int y2 = Math.Abs(2 * y - (n - 1)), d = Math.Abs(2 * x - (n - 1) - 2 * _riverO[y]);
                if (y2 > 2 * (_fh - 4) - 1 || d > 2 * (_hw - 1) - 1 || _g.Land[i] != LandType.Clear) continue;
                bool dup = false;
                foreach (int j in _g.Images(i)) if (j < i) dup = true;
                if (!dup) cells.Add(i);
            }
        return Largest(cells);
    }
}
