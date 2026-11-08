// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

// Layout 2, the main's surround. A walled main is the old solid three-layer ring with its gates. A soft shell (D-183: about two seeds in three)
// is a loose ring of rubble chunks: broken into pieces of 6 to 14 cells whose thickness changes from one to three layers, with 2 or 3 wide
// approaches (the lane gates, widened, and sometimes a third on a free side), rubble speckle on the thin layers and a few yard walls and rubble
// lots just outside it. The layer two cells off the interior (the one LayoutChecks.Entrances probes) is always solid inside a piece.
internal sealed partial class ProceduralBuilder
{
    /// <summary>Position of a cell along the ring two cells outside the interior (clockwise from its top-left corner), projected from the side it faces.</summary>
    private static int RingPos(int ix0, int iy0, int ix1, int iy1, int x, int y)
    {
        int rx0 = ix0 - 2, rx1 = ix1 + 2, ry0 = iy0 - 2, ry1 = iy1 + 2, w = rx1 - rx0 + 1, h = ry1 - ry0 + 1;
        int dl = ix0 - x, dr = x - ix1, dt = iy0 - y, db = y - iy1, m = Math.Max(Math.Max(dl, dr), Math.Max(dt, db));
        if (m == dt) return Math.Clamp(x, rx0, rx1) - rx0;
        if (m == dr) return w - 1 + Math.Clamp(y, ry0, ry1) - ry0;
        if (m == db) return (w - 1) + (h - 1) + rx1 - Math.Clamp(x, rx0, rx1);
        return 2 * (w - 1) + (h - 1) + ry1 - Math.Clamp(y, ry0, ry1);
    }

    /// <summary>Paints the soft ring round the interior; <paramref name="gates"/> are the lane exits (cell on the ring, width). Returns nothing; the caller opens the gates as lanes afterwards.</summary>
    private void SoftShell(int ix0, int iy0, int ix1, int iy1, List<(int X, int Y, int W)> gates, bool thirdGap)
    {
        int n = _n;
        int ox0 = ix0 - 3, ox1 = ix1 + 3, oy0 = iy0 - 3, oy1 = iy1 + 3;
        bool westEdge = ox0 <= 6, northEdge = oy0 <= 6, eastEdge = ox1 >= n - 7, southEdge = oy1 >= n - 7;
        if (westEdge) ox0 = 0; if (northEdge) oy0 = 0; if (eastEdge) ox1 = n - 1; if (southEdge) oy1 = n - 1;
        int rw = ix1 - ix0 + 5, rh = iy1 - iy0 + 5, perim = 2 * (rw - 1) + 2 * (rh - 1);
        // Gaps: (centre, cells before, cells after), in ring positions.
        var gaps = new List<(int C, int Lo, int Hi)>();
        foreach ((int gx, int gy, int gw) in gates)
        {
            int extra = _players > 2 ? _rng.Next(0, 2) : _rng.Next(2, 6);
            int lo = (gw - 1) / 2 + _rng.Next(0, extra + 1), hi = gw / 2 + extra - (lo - (gw - 1) / 2);
            gaps.Add((RingPos(ix0, iy0, ix1, iy1, gx, gy), lo, hi));
        }
        if (thirdGap && gates.Count == 2)
        {
            // A third approach on a free side (not along the map edge), 5 to 9 wide.
            int side = !northEdge ? 0 : !westEdge ? 3 : -1;
            if (side >= 0 && _rng.Next(100) < 70)
            {
                int w = _rng.Next(5, 10);
                int x = side == 0 ? _rng.Next(ix0 + 2, ix1 - 6) : ix0 - 2, y = side == 0 ? iy0 - 2 : _rng.Next(iy0 + 2, iy1 - 2);
                gaps.Add((RingPos(ix0, iy0, ix1, iy1, x, y), (w - 1) / 2, w / 2));
            }
        }
        bool InGap(int p)
        {
            foreach ((int c, int lo, int hi) in gaps)
            {
                int d = ((p - c) % perim + perim) % perim;
                if (d > perim / 2) d -= perim;
                if (d >= -lo && d <= hi) return true;
            }
            return false;
        }
        // Pieces of 6 to 14 cells, each with a thickness pattern (layers 1..3; layer 2 is always in it).
        var mask = new int[perim];
        for (int p = 0, len; p < perim; p += len)
        {
            len = _rng.Next(6, 15);
            int r = _rng.Next(100), m = r < 15 ? 0b010 : r < 35 ? 0b011 : r < 55 ? 0b110 : 0b111;
            for (int q = p; q < Math.Min(perim, p + len); q++) mask[q] = m;
        }
        for (int y = oy0; y <= oy1; y++)
            for (int x = ox0; x <= ox1; x++)
            {
                if (x >= ix0 && x <= ix1 && y >= iy0 && y <= iy1) continue;
                int dl = ix0 - x, dr = x - ix1, dt = iy0 - y, db = y - iy1, layer = Math.Max(Math.Max(dl, dr), Math.Max(dt, db));
                int i = _g.Idx(x, y);
                if (layer > 3)
                {
                    _g.SetLand(i, _players > 2 && n < 96 ? LandType.Rough : LandType.Rock);   // the filled strip between the ring and the map edge
                }
                else
                {
                    int p = RingPos(ix0, iy0, ix1, iy1, x, y) % perim;
                    bool corner = (dl > 0 || dr > 0) && (dt > 0 || db > 0);
                    if (InGap(p)) _g.SetLand(i, LandType.Clear);
                    else if ((mask[p] & (1 << (layer - 1))) != 0 && !(corner && layer == 3 && _rng.Next(2) == 0)) _g.SetLand(i, LandType.Rock);
                    else _g.SetLand(i, _rng.Next(100) < 30 ? LandType.Rough : LandType.Clear);
                }
                foreach (int j in _g.Images(i)) _zone[j] = 0;
            }
        // Yard walls and rubble lots just outside the ring (never near a lane, a gate or the main).
        int pieces = _rng.Next(3, 7);
        for (int k = 0; k < pieces; k++)
        {
            int w = _rng.Next(4, 9), h = _rng.Next(4, 8);
            int side = _rng.Next(4);
            int x0 = side == 2 ? ix0 - 5 - w - _rng.Next(0, 3) : side == 3 ? ix1 + 6 + _rng.Next(0, 4) : _rng.Next(ix0 - 2, ix1 - w + 3);
            int y0 = side == 0 ? iy0 - 5 - h - _rng.Next(0, 3) : side == 1 ? iy1 + 6 + _rng.Next(0, 4) : _rng.Next(iy0 - 2, iy1 - h + 3);
            if (!QuietRect(x0, y0, x0 + w - 1, y0 + h - 1, 3)) continue;
            if (_rng.Next(2) == 0) RuinedWall(x0, y0, x0 + w - 1, y0 + h - 1); else Lot(x0, y0, x0 + w - 1, y0 + h - 1);
        }
    }

    /// <summary>True when the rectangle and a margin around it are inside the map and hold untouched ground (no lane, main, plaza, rock or water).</summary>
    private bool QuietRect(int x0, int y0, int x1, int y1, int margin)
    {
        for (int y = y0 - margin; y <= y1 + margin; y++)
            for (int x = x0 - margin; x <= x1 + margin; x++)
            {
                if (!_g.In(x, y)) { if (x >= x0 && x <= x1 && y >= y0 && y <= y1) return false; continue; }
                int i = _g.Idx(x, y);
                if (_zone[i] != 0) return false;
                if (x >= x0 && x <= x1 && y >= y0 && y <= y1 && _g.Land[i] is not (LandType.Clear or LandType.Rough)) return false;
            }
        return true;
    }
}
