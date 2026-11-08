// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

// Layout 2 for the other biomes (M12-09/10/11): the same zone skeleton (mains, lanes, plazas, river, fields) with each biome's own painting
// (design 16 section 3.3 as trimmed by design 17 and D-183). Block fill replaces the Rust Belt's ruined town; a post pass then turns rubble walls
// into the biome's own obstacles without changing which cells are passable, so the layout checks keep their meaning.
//   Mountains   boulders and scree between the lanes; every large rock mass is a cliff; the natural sits on a plateau (cliff ring, one ramp).
//   Ash Plains  crater fields and burnt groves (tree props that stand where rubble walls were) as soft lanes, ash ground.
//   Glass Coast dune crests with rough flanks as lane walls, lagoons and drowned buildings, and sea behind the mains (rock masses that reach the edge).
internal sealed partial class ProceduralBuilder
{
    private const int SkinRust = 0, SkinMountains = 1, SkinAsh = 2, SkinGlass = 3;
    private int _skin;
    private readonly List<(int X0, int Y0, int X1, int Y1)> _plateaus = new();

    private static int SkinOf(string key) => key switch { "mountains" => SkinMountains, "ash_plains" => SkinAsh, "glass_coast" => SkinGlass, _ => SkinRust };

    /// <summary>Deterministic 0..99 value of a cell's symmetry orbit (so the painting stays symmetric without drawing from the rng).</summary>
    private int OrbitHash(int i, int salt)
    {
        int low = int.MaxValue;
        foreach (int j in _g.Images(i)) low = Math.Min(low, j);
        unchecked { uint h = (uint)(low * 73856093 + salt * 19349663 + 83492791); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (int)(h % 100); }
    }

    private bool IsLowImage(int i)
    {
        foreach (int j in _g.Images(i)) if (j < i) return false;
        return true;
    }

    /// <summary>Block fill of a non-Rust-Belt biome. Returns false when the biome has no special fill (Rust Belt).</summary>
    private bool SkinBlock(int x0, int y0, int x1, int y1, int roll)
    {
        if (_skin == SkinRust) return false;
        int w = x1 - x0 + 1, h = y1 - y0 + 1, m = Math.Min(w, h);
        if (_n < 80 && _players > 2)
        {
            // Small 3-4 player maps are tight: the Rust Belt fill (rubble lots, a few standing walls) keeps the paths between the mains long enough; the post pass still gives the biome its look.
            if (roll < 40) Lot(x0, y0, x1, y1); else if (roll < 43 && m >= 5) RuinedWall(x0, y0, x1, y1);
            return true;
        }
        if (_skin == SkinMountains)
        {
            if (roll < 34 && m >= 4) Blob(x0, y0, x1, y1, LandType.Rock, 3, Math.Min(8, m), true);
            else if (roll < 52) Lot(x0, y0, x1, y1);
        }
        else if (_skin == SkinAsh)
        {
            if (roll < 30 && m >= 6) Blob(x0, y0, x1, y1, LandType.Rough, 5, Math.Min(11, m), false);   // crater
            else if (roll < 44 && m >= 4) Blob(x0, y0, x1, y1, LandType.Rock, 3, Math.Min(6, m), false);   // grove (turned into trees later)
            else if (roll < 54) Lot(x0, y0, x1, y1);
        }
        else
        {
            if (roll < 26 && m >= 5) Dune(x0, y0, x1, y1);
            else if (roll < 42 && m >= 6) Blob(x0, y0, x1, y1, LandType.Shallow, 4, Math.Min(10, m), false);   // lagoon
            else if (roll < 50 && m >= 4) Building(x0, y0, x1, y1);   // drowned suburb
        }
        return true;
    }

    /// <summary>An ellipse of <paramref name="land"/> of width 3..max inside the block rectangle, at a random spot, with an optional rough fringe.</summary>
    private void Blob(int x0, int y0, int x1, int y1, LandType land, int minSize, int maxSize, bool fringe)
    {
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        int bw = _rng.Next(minSize, Math.Max(minSize, maxSize) + 1), bh = Math.Clamp(bw + _rng.Next(-2, 3), 3, Math.Max(3, h));
        bw = Math.Min(bw, w);
        int bx = x0 + _rng.Next(0, w - bw + 1), by = y0 + _rng.Next(0, h - bh + 1);
        for (int y = by; y < by + bh; y++)
            for (int x = bx; x < bx + bw; x++)
            {
                // ellipse test in integers: ((2x - cx) / bw)^2 + ((2y - cy) / bh)^2 <= 1
                long dx = 2 * (x - bx) + 1 - bw, dy = 2 * (y - by) + 1 - bh;
                if (dx * dx * bh * bh + dy * dy * bw * bw > (long)bw * bw * bh * bh) { if (fringe && _rng.Next(100) < 30) _g.SetLand(_g.Idx(x, y), LandType.Rough); continue; }
                _g.SetLand(_g.Idx(x, y), land);
            }
    }

    /// <summary>A dune crest: a 2 to 3 thick rock ridge along the longer side of the block, rough flanks on both sides.</summary>
    private void Dune(int x0, int y0, int x1, int y1)
    {
        bool alongX = x1 - x0 >= y1 - y0;
        int len = (alongX ? x1 - x0 : y1 - y0) + 1, span = (alongX ? y1 - y0 : x1 - x0) + 1;
        int thick = _rng.Next(2, 4), u0 = _rng.Next(0, Math.Max(1, len / 4)), u1 = len - 1 - _rng.Next(0, Math.Max(1, len / 4));
        int mid = _rng.Next(1, Math.Max(2, span - thick - 1));
        for (int u = u0; u <= u1; u++)
        {
            int bend = (u - u0) * 3 / Math.Max(1, u1 - u0) - 1;   // a gentle curve across the ridge
            for (int v = -1; v <= thick; v++)
            {
                int vv = Math.Clamp(mid + bend + v, 0, span - 1);
                int x = alongX ? x0 + u : x0 + vv, y = alongX ? y0 + vv : y0 + u;
                bool crest = v >= 0 && v < thick;
                if (!crest && _rng.Next(100) < 35) continue;
                _g.SetLand(_g.Idx(x, y), crest ? LandType.Rock : LandType.Rough);
            }
        }
    }

    /// <summary>The post pass: plateaus (Mountains), groves (Ash Plains), flanks and sea (Glass Coast). Passability is kept the same.</summary>
    private void Skin()
    {
        if (_skin == SkinRust) return;
        int n = _n, total = n * n;
        var comp = new int[total]; var size = new List<int> { 0 }; var touchesEdge = new List<bool> { false }; var nearRes = new List<bool> { false };
        var stack = new Stack<int>();
        int id = 0;
        for (int s = 0; s < total; s++)
        {
            if (comp[s] != 0 || _g.Land[s] != LandType.Rock) continue;
            id++; int count = 0; bool edge = false, res = false;
            comp[s] = id; stack.Push(s);
            while (stack.Count > 0)
            {
                int c = stack.Pop(); count++;
                int x = c % n, y = c / n;
                if (x == 0 || y == 0 || x == n - 1 || y == n - 1) edge = true;
                for (int dy = -1; dy <= 1 && !res; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (_g.In(x + dx, y + dy) && OverlayKinds.IsResource(_g.Overlay[_g.Idx(x + dx, y + dy)])) { res = true; break; }
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (!_g.In(nx, ny)) continue;
                    int j = _g.Idx(nx, ny);
                    if (comp[j] != 0 || _g.Land[j] != LandType.Rock) continue;
                    comp[j] = id; stack.Push(j);
                }
            }
            size.Add(count); touchesEdge.Add(edge); nearRes.Add(res);
        }
        for (int i = 0; i < total; i++)
        {
            int c = comp[i];
            if (c == 0) continue;
            if (_skin == SkinMountains && size[c] >= 40) _g.Land[i] = LandType.Cliff;
            else if (_skin == SkinGlass && touchesEdge[c] && size[c] >= 60) _g.Land[i] = LandType.Water;
            else if (_skin == SkinAsh && size[c] <= 80 && !nearRes[c] && MinStartDistance(i % n, i / n) > 16 && IsLowImage(i))
            {
                int v = OrbitHash(i, 1) % 3;
                foreach (int j in _g.Images(i)) { _g.Land[j] = LandType.Rough; _g.Overlay[j] = OverlayKind.Tree; _g.Density[j] = (byte)v; }
            }
        }
        if (_skin == SkinGlass)
        {
            // Beach: the open cells beside the sea; rough flanks along the dune crests (hash-picked so both images agree).
            var beach = new List<int>(); var flank = new List<int>();
            for (int i = 0; i < total; i++)
            {
                if (_g.Land[i] is not (LandType.Clear or LandType.Rough) || _zone[i] == ZLane || _zone[i] == ZMain || _g.Overlay[i] != OverlayKind.None) continue;
                int x = i % n, y = i / n; bool sea = false, crest = false;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (!_g.In(x + dx, y + dy)) continue;
                        LandType l = _g.Land[_g.Idx(x + dx, y + dy)];
                        sea |= l == LandType.Water; crest |= l == LandType.Rock;
                    }
                if (sea) beach.Add(i); else if (crest && OrbitHash(i, 2) < 55) flank.Add(i);
            }
            foreach (int i in beach) _g.Land[i] = LandType.Coast;
            foreach (int i in flank) _g.Land[i] = LandType.Rough;
        }
        if (_skin == SkinMountains)
            foreach ((int x0, int y0, int x1, int y1) in _plateaus)
            {
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (_g.In(x, y)) _g.SetHeight(_g.Idx(x, y), 1);
                // The ring cells that are not rock are the ramp: kept at height 0 beside the rim so the climb is one cell.
                for (int y = y0 - 1; y <= y1 + 1; y++)
                    for (int x = x0 - 1; x <= x1 + 1; x++)
                    {
                        if (!_g.In(x, y) || (x >= x0 && x <= x1 && y >= y0 && y <= y1)) continue;
                        int i = _g.Idx(x, y);
                        if (_g.Land[i] is LandType.Clear or LandType.Rough or LandType.Road) _g.MarkRamp(i);
                    }
            }
    }

    /// <summary>Last step: Ash Plains ground is ash (the skeleton is built on clear ground); pitchblende pits and roads keep their land.</summary>
    private void FinishLand()
    {
        if (_skin != SkinAsh) return;
        for (int i = 0; i < _g.Land.Length; i++)
            if (_g.Land[i] == LandType.Clear && _g.Overlay[i] != OverlayKind.Pitchblende) _g.Land[i] = LandType.Ash;
    }
}
