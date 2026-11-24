// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// The S43 terrain detail pass (<c>[detail] level = 1</c> in a palette): multi-octave tone noise instead of 8-texel blotches, per-land-type
/// features (pebbles, dead grass, hairline cracks, oil stains, ash drifts, wave crests, cliff strata), soft noise-warped blends between land
/// types, and irregular shore, road-edge and cliff-lip lines. Pure integer maths (plus one IEEE square root), no GPU types, no state: a texel's colour
/// depends only on its own position and the land types in the 3x3 cells around it, so <see cref="TerrainBake.PaintCell"/> stays valid
/// (the renderer already repaints a changed cell and its eight neighbours). All sizes are fractions of a cell, so the same look bakes at 16 or 32 texels per cell.
/// Light comes from the upper left (D-184 sun rule): lit pixels sit north-west, hard one-texel shadows south-east.
/// </summary>
public static class TerrainDetail
{
    private static readonly TerrainPalette.Rgb White = new(255, 255, 255), Black = new(0, 0, 0);
    private static readonly TerrainPalette.Rgb Straw = new(170, 148, 84), Oil = new(22, 24, 44), Sand = new(204, 192, 150), Rust = new(150, 84, 48);

    private static uint H(int a, int b, uint salt) => TerrainBake.Hash3((uint)a, (uint)b, salt);

    /// <summary>Smooth value noise 0..1023 on a lattice <paramref name="px"/> by <paramref name="py"/> texels (smoothstep blended).</summary>
    public static int Vn(int x, int y, int px, int py, uint salt)
    {
        int gx = x / px, gy = y / py;
        int fx = x % px * 256 / px, fy = y % py * 256 / py;
        int wx = fx * fx * (768 - 2 * fx) >> 16, wy = fy * fy * (768 - 2 * fy) >> 16;
        int v00 = (int)(H(gx, gy, salt) & 1023u), v10 = (int)(H(gx + 1, gy, salt) & 1023u);
        int v01 = (int)(H(gx, gy + 1, salt) & 1023u), v11 = (int)(H(gx + 1, gy + 1, salt) & 1023u);
        int top = v00 * (256 - wx) + v10 * wx, bottom = v01 * (256 - wx) + v11 * wx;
        return (top * (256 - wy) + bottom * wy) >> 16;
    }

    /// <summary>Three octaves (about 2.5, 0.9 and 0.3 cells) weighted 5:3:2; 0..1023, centred near 512.</summary>
    public static int Fbm(int x, int y, int s, uint salt) =>
        (Vn(x, y, s * 5 / 2, s * 5 / 2, salt) * 5 + Vn(x, y, s * 7 / 8, s * 7 / 8, salt + 1) * 3 + Vn(x, y, Math.Max(2, s / 3), Math.Max(2, s / 3), salt + 2) * 2) / 10;

    private static TerrainPalette.Rgb Toward(TerrainPalette.LandShades sh, int t)   // t -512..512
        => t < 0 ? TerrainBake.Mix(sh.Base, sh.Dark, -t * 1000 / 512) : TerrainBake.Mix(sh.Base, sh.Light, t * 1000 / 512);

    /// <summary>The textured ground colour of <paramref name="land"/> at texel (<paramref name="tx"/>, <paramref name="ty"/>), before blending with neighbours.
    /// <paramref name="features"/> false leaves out pebbles, tufts, stains and cracks (used under resource fields).</summary>
    public static TerrainPalette.Rgb Ground(TerrainPalette palette, LandType land, int tx, int ty, bool features)
    {
        int s = palette.TexelCells, k = Math.Max(1, s / 16);
        TerrainPalette.LandShades sh = palette.ShadesFor(land);
        int cx = tx / s, cy = ty / s, sx = tx % s, sy = ty % s;
        uint salt = 100u + (uint)land * 10u;

        TerrainPalette.Rgb col;
        switch (land)
        {
            case LandType.Water:
            case LandType.Shallow:
                col = Water(palette, land, sh, tx, ty, s, k, salt);
                break;
            case LandType.Cliff:
                col = Cliff(sh, tx, ty, s, k, salt);
                break;
            case LandType.Ash:
                col = AshGround(sh, tx, ty, s, k, salt);
                break;
            default:
                {
                    int n = Fbm(tx, ty, s, salt);
                    int gain = land == LandType.Road ? 150 : land == LandType.Rock ? 330 : 270;
                    col = Toward(sh, Math.Clamp((n - 512) * gain / 100, -512, 512));
                    uint g = H(tx / k, ty / k, 2);
                    if (g % 5u == 0) col = TerrainBake.Mix(col, (g >> 8 & 1u) != 0 ? sh.Light : sh.Dark, land == LandType.Road ? 260 : 380);

                    // Bump lighting from the upper left on rough ground, rock and rubble.
                    int bump = land is LandType.Rock ? 22 : land == LandType.Rough ? 12 : land == LandType.Road ? 3 : 5;
                    int p2 = Math.Max(3, s * 7 / 8), p3 = Math.Max(2, s / 3);
                    int e = Vn(tx - k, ty - k, p2, p2, salt + 3) + Vn(tx - k, ty - k, p3, p3, salt + 4) / 2
                        - Vn(tx + k, ty + k, p2, p2, salt + 3) - Vn(tx + k, ty + k, p3, p3, salt + 4) / 2;
                    int lift = Math.Clamp(e * bump / 40, -140, 140);
                    col = lift >= 0 ? TerrainBake.Mix(col, White, lift) : TerrainBake.Mix(col, Black, -lift);

                    if (features) col = Features(palette.Style.Detail >= 2, land, sh, col, tx, ty, cx, cy, sx, sy, s, k, salt);
                    break;
                }
        }

        if (features && land == LandType.Ash) col = AshFeatures(sh, col, tx, ty, cx, cy, sx, sy, s, k);
        return col;
    }

    private static TerrainPalette.Rgb Features(bool calm, LandType land, TerrainPalette.LandShades sh, TerrainPalette.Rgb col, int tx, int ty, int cx, int cy, int sx, int sy, int s, int k, uint salt)
    {
        // Pebbles and rubble: a lit north-west edge, a body, a hard one-texel shadow to the south-east.
        int chance = land switch { LandType.Clear => 200, LandType.Rough => 420, LandType.Rock => 160, LandType.Road => 50, _ => 0 };
        if (calm) chance /= 2;
        int count = land == LandType.Rough ? 4 : 3;
        for (int i = 0; i < count && chance > 0; i++)
        {
            uint h = H(cx * 7 + i, cy, 30u + (uint)i);
            if (h % 1000u >= (uint)chance) continue;
            int w = k * (land == LandType.Rough ? 2 + (int)(h >> 10 & 1u) : 2), ht = k * (1 + (int)(h >> 11 & 1u));
            int px = 1 + (int)(h >> 12 & 0xFFu) % Math.Max(1, s - w - 3), py = 1 + (int)(h >> 20 & 0xFFu) % Math.Max(1, s - ht - 3);
            int dx = sx - px, dy = sy - py;
            if (dx >= 0 && dx < w && dy >= 0 && dy < ht)
            {
                if (dx < k || dy < k) return TerrainBake.Mix(sh.Light, White, 260);
                return TerrainBake.Mix(sh.Dark, new TerrainPalette.Rgb(104, 100, 98), 450);
            }

            if ((dx >= w && dx < w + k && dy >= k && dy <= ht) || (dy >= ht && dy < ht + k && dx >= k && dx <= w)) return TerrainBake.Mix(sh.Dark, Black, 560);
        }

        // Dead grass: three straw-coloured blades.
        if (!calm && land is LandType.Clear or LandType.Rough)
        {
            uint h = H(cx, cy, 40u);
            if (h % 1000u < (land == LandType.Clear ? 170u : 90u))
            {
                int bx = 2 * k + (int)(h >> 10 & 0x7Fu) % Math.Max(1, s - 8 * k), by = 5 * k + (int)(h >> 17 & 0x7Fu) % Math.Max(1, s - 6 * k);
                for (int b = 0; b < 3; b++)
                {
                    int len = k * (2 + (int)(H(cx * 3 + b, cy, 41u) & 1u)) + k, x0 = bx + 2 * k * b;
                    int lean = b == 1 ? k : 0;
                    int dx = sx - x0, dy = by - sy;
                    if (dx >= 0 && dx < k && dy >= 0 && dy < len)
                        return TerrainBake.Mix(col, Straw, dy >= len - k ? 700 : 500);
                    if (lean > 0 && dy == len && dx >= 0 && dx < k * 2 - 0) return TerrainBake.Mix(col, Straw, 700);
                }
            }
        }

        // Hairline cracks: thin ridges of a low-frequency noise, only inside a mask so they come in patches.
        int cp = Math.Max(6, s * 5 / 4);
        if (!calm && (land != LandType.Rough || sx % 3 != 0))
        {
            int mask = Vn(tx, ty, s * 3, s * 3, salt + 5);
            int maskMin = land is LandType.Road ? 640 : land == LandType.Rock ? 520 : 700;
            if (mask > maskMin)
            {
                int ridge = Math.Abs(Vn(tx, ty, cp, cp, salt + 6) - 512);
                if (ridge < 440 / cp) col = TerrainBake.Mix(col, Black, land == LandType.Rock ? 380 : 250);
            }
        }

        // Oil / soot stain: a dithered dark pool.
        if (land is LandType.Clear or LandType.Road or LandType.Rough)
        {
            uint h = H(cx, cy, 60u);
            if (h % 1000u < 30u)
            {
                int ox = s * 3 / 8 + (int)(h >> 10 & 0xFu) * s / 64, oy = s * 3 / 8 + (int)(h >> 14 & 0xFu) * s / 64;
                int r = s * (3 + (int)(h >> 18 & 3u)) / 16, dx = sx - ox, dy = sy - oy;
                int d2 = dx * dx * 4 / 3 + dy * dy * 4;   // squashed: ground ellipses are wider than tall
                int cov = 1000 - d2 * 1000 / (r * r);
                cov += (int)(H(tx / k, ty / k, 61u) % 400u) - 200;
                if (cov > 600) return TerrainBake.Mix(col, Oil, 620);
                if (cov > 300) return TerrainBake.Mix(col, Oil, 340);
                if (cov > 80 && ((tx + ty) / k & 1) == 0) return TerrainBake.Mix(col, Oil, 220);
            }
        }

        return col;
    }

    private static TerrainPalette.Rgb AshGround(TerrainPalette.LandShades sh, int tx, int ty, int s, int k, uint salt)
    {
        // Wind-combed drifts: long horizontal streaks over a soft base, with thin lighter crest lines.
        int n = Fbm(tx, ty, s, salt);
        int streak = Vn(tx, ty, s * 4, Math.Max(2, s * 3 / 8), salt + 7);
        int t = (n - 512) * 220 / 100 + (streak - 512) * 150 / 100;
        TerrainPalette.Rgb col = Toward(sh, Math.Clamp(t, -512, 512));
        int py = Math.Max(3, s / 2);
        int crest = Math.Abs(Vn(tx, ty, s * 3, py, salt + 8) - 512);
        if (crest < 40) col = TerrainBake.Mix(col, sh.Light, 650);
        uint g = H(tx / k, ty / k, 2);
        if (g % 6u == 0) col = TerrainBake.Mix(col, (g >> 8 & 1u) != 0 ? sh.Light : sh.Dark, 420);
        return col;
    }

    private static TerrainPalette.Rgb AshFeatures(TerrainPalette.LandShades sh, TerrainPalette.Rgb col, int tx, int ty, int cx, int cy, int sx, int sy, int s, int k)
    {
        uint g = H(tx / k, ty / k, 77u) % 400u;
        if (g == 0) return TerrainBake.Mix(sh.Dark, Black, 550);                       // cinder
        if (g == 1) return TerrainBake.Mix(sh.Light, White, 230);                      // pale ash flake
        uint h = H(cx, cy, 78u);
        if (h % 1000u < 150u)   // a charred twig: short dark diagonal with a lit twin
        {
            int px = 2 * k + (int)(h >> 10 & 0xFu) % Math.Max(1, s - 6 * k), py = 2 * k + (int)(h >> 16 & 0xFu) % Math.Max(1, s - 6 * k);
            int dx = sx - px, dy = sy - py;
            if (dy >= 0 && dy < 3 * k && dx >= dy && dx < dy + k) return TerrainBake.Mix(sh.Dark, Black, 700);
            if (dy >= 0 && dy < 3 * k && dx == dy - k) return TerrainBake.Mix(sh.Light, White, 150);
        }

        return col;
    }

    private static TerrainPalette.Rgb Cliff(TerrainPalette.LandShades sh, int tx, int ty, int s, int k, uint salt)
    {
        // Vertical striations over horizontal strata; a warm grey comes through the near-black face.
        TerrainPalette.Rgb warm = TerrainBake.Mix(sh.Light, new TerrainPalette.Rgb(78, 70, 58), 550);
        int v = Vn(tx, ty, Math.Max(2, s / 4), s * 2, salt);
        int strata = Vn(tx, ty, s * 3, Math.Max(2, s / 3), salt + 1);
        int t = (v - 380) * 420 / 640;
        TerrainPalette.Rgb col = TerrainBake.Mix(sh.Base, warm, Math.Clamp(t, 0, 520));
        if (Math.Abs(strata - 512) < 46) col = TerrainBake.Mix(col, Black, 500);
        uint g = H(tx / k, ty / k, 2);
        if (g % 7u == 0) col = TerrainBake.Mix(col, warm, 420);
        return col;
    }

    private static TerrainPalette.Rgb Water(TerrainPalette palette, LandType land, TerrainPalette.LandShades sh, int tx, int ty, int s, int k, uint salt)
    {
        int a = Vn(tx, ty, s * 2, Math.Max(2, s / 2), salt);
        int b = Vn(tx, ty, s, Math.Max(2, s / 3), salt + 1);
        int big = Vn(tx, ty, s * 4, s * 4, salt + 2);
        int t = (a - 512) * 160 / 100 + (b - 512) * 110 / 100 + (big - 512) * 140 / 100;
        TerrainPalette.Rgb col = Toward(sh, Math.Clamp(t, -512, 512));
        int crest = Vn(tx, ty, s * 2, Math.Max(3, s * 5 / 16), salt + 3);
        if (crest > 905 && H(tx / k, ty / k, 81u) % 3u != 0) col = TerrainBake.Mix(col, land == LandType.Shallow ? White : sh.Light, land == LandType.Shallow ? 420 : 700);
        if (land == LandType.Shallow)
        {
            uint g = H(tx / k, ty / k, 82u) % 90u;
            if (g == 0) col = TerrainBake.Mix(col, Sand, 330);   // the bottom shows through
        }

        return col;
    }

    private static bool SoftPair(LandType a, LandType b)
    {
        if (a == b || a == LandType.Cliff || b == LandType.Cliff) return false;
        bool wa = a is LandType.Water or LandType.Shallow, wb = b is LandType.Water or LandType.Shallow;
        return wa == wb;
    }

    /// <summary>
    /// Soft transition: toward each differing neighbour among the eight around the cell, mix in that land's textured ground, up to 50 % at the
    /// boundary and fading over about three eighths of a cell, with the distance warped by noise so the border is organic, not a straight line.
    /// </summary>
    public static TerrainPalette.Rgb Blend(TerrainPalette palette, CellGrid map, LandType land, TerrainPalette.Rgb col, int cx, int cy, int tx, int ty)
    {
        int s = palette.TexelCells, w = map.Width, h = map.Height;
        int sx = tx % s, sy = ty % s;
        if (land == LandType.Cliff) return col;
        // Quick reject: only texels within half a cell of an edge can be affected.
        int reach = s / 2;
        if (sx >= reach && sx < s - reach && sy >= reach && sy < s - reach) return col;

        int warpN = Vn(tx, ty, Math.Max(3, s * 5 / 8), Math.Max(3, s * 5 / 8), 90u) - 512;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = cx + ox, ny = cy + oy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                LandType nl = map.Land[ny * w + nx];
                if (!SoftPair(land, nl)) continue;
                int ddx = ox < 0 ? sx + 1 : ox > 0 ? s - sx : 0, ddy = oy < 0 ? sy + 1 : oy > 0 ? s - sy : 0;
                int d16 = (int)Math.Sqrt((double)(ddx * ddx + ddy * ddy) * 256.0) - 8;   // texels * 16, 0 at the boundary
                bool road = land == LandType.Road || nl == LandType.Road;
                bool water = land is LandType.Water or LandType.Shallow;
                int bw = (road ? s / 4 : water ? s / 2 : s * 3 / 8) * 16;
                d16 += warpN * bw / 1700;
                if (d16 >= bw) continue;
                int wgt = (road ? 360 : 500) * (bw - Math.Max(0, d16)) / bw;
                if (wgt <= 0) continue;
                // Rough-edged dither: a few texels flip a notch either way.
                uint j = H(tx, ty, 91u);
                if (j % 5u == 0) wgt = wgt * ((j >> 8 & 1u) != 0 ? 3 : 1) / 2;
                col = TerrainBake.Mix(col, Ground(palette, nl, tx, ty, false), Math.Min(wgt, 560));
            }

        return col;
    }

    private static int LandAt(CellGrid map, int cx, int cy) =>
        cx < 0 || cy < 0 || cx >= map.Width || cy >= map.Height ? -1 : (int)map.Land[cy * map.Width + cx];

    private static int Dist(int nW, int dW, int nE, int dE, int nN, int dN, int nS, int dS, Func<int, bool> m)
    {
        int best = int.MaxValue;
        if (nW != int.MinValue && m(nW)) best = Math.Min(best, dW);
        if (nE != int.MinValue && m(nE)) best = Math.Min(best, dE);
        if (nN != int.MinValue && m(nN)) best = Math.Min(best, dN);
        if (nS != int.MinValue && m(nS)) best = Math.Min(best, dS);
        return best;
    }

    /// <summary>Broken road edge, foamy shore with a wet bank, ragged cliff lip, hard dithered cliff shadow.</summary>
    public static TerrainPalette.Rgb Decorate(TerrainPalette.Rgb col, CellGrid map, TerrainPalette.TerrainStyle st, int cx, int cy, int sx, int sy, int s)
    {
        int k = Math.Max(1, s / 16);
        int tx = cx * s + sx, ty = cy * s + sy;
        LandType land = map.Land[cy * map.Width + cx];
        int nW = LandAt(map, cx - 1, cy), nE = LandAt(map, cx + 1, cy), nN = LandAt(map, cx, cy - 1), nS = LandAt(map, cx, cy + 1);
        int dW = sx, dE = s - 1 - sx, dN = sy, dS = s - 1 - sy;
        uint jit = H(tx / k, ty / k, 95u);

        if (land == LandType.Road && st.RoadEdge is { } line)
        {
            int d = Dist(nW, dW, nE, dE, nN, dN, nS, dS, n => n >= 0 && n != (int)LandType.Road);
            if (d < st.RoadEdgeTexels * k) { if (jit % 7u != 0) col = TerrainBake.Mix(col, line, jit % 3u == 0 ? 600 : 850); }
            else if (d < (st.RoadEdgeTexels + 1) * k && jit % 4u == 0) col = TerrainBake.Mix(col, line, 300);   // crumbled kerb
        }
        else if (land is LandType.Water or LandType.Shallow && st.Shore is { } foam)
        {
            int d = Dist(nW, dW, nE, dE, nN, dN, nS, dS, n => n >= 0 && n != (int)LandType.Water && n != (int)LandType.Shallow);
            int wob = (Vn(tx, ty, Math.Max(3, s / 2), Math.Max(3, s / 2), 96u) - 512) * 2 * k / 1023;   // -k..k
            int band = st.ShoreTexels * k + wob;
            if (d < band) col = TerrainBake.Mix(col, foam, 820 * (band - d) / Math.Max(1, band));
            else if (d < band + 2 * k && jit % 5u == 0) col = TerrainBake.Mix(col, foam, 380);   // spray flecks
        }
        else if (land == LandType.Cliff && st.CliffTop is { } top)
        {
            int lip = Dist(nW, dW, int.MinValue, 0, nN, dN, int.MinValue, 0, n => n >= 0 && n != (int)LandType.Cliff);
            int face = Dist(int.MinValue, 0, nE, dE, int.MinValue, 0, nS, dS, n => n >= 0 && n != (int)LandType.Cliff);
            int rag = (int)(H(tx / k, ty / k, 97u) % 3u == 0 ? k : 0);
            if (lip < st.CliffTopTexels * k + rag)
                col = TerrainBake.Mix(col, jit % 6u == 0 ? TerrainBake.Mix(top, Black, 350) : top, 900);
            else if (lip < (st.CliffTopTexels + 2) * k && jit % 3u == 0) col = TerrainBake.Mix(col, top, 260);   // light catches the broken edge
            else if (face < st.CliffTopTexels * k + rag) col = TerrainBake.Mix(col, Black, 480);
        }

        if (land != LandType.Cliff && st.CliffShadowPermille > 0)
        {
            int n = st.CliffShadowTexels * k, perm = 0;
            if (nN == (int)LandType.Cliff && sy < n) perm = st.CliffShadowPermille * (n - sy) / n;
            if (nW == (int)LandType.Cliff && sx < n) perm = Math.Max(perm, st.CliffShadowPermille * (n - sx) / (2 * n));
            // Hard shadow (sun rule): solid near the foot, a 50 % checker in the outer half, nothing between.
            if (perm > 0)
            {
                bool outer = Math.Max(sy, sx) >= n / 2 && (nN != (int)LandType.Cliff || sy >= n / 2) && (nW != (int)LandType.Cliff || sx >= n / 2);
                if (!outer || ((tx / k + ty / k) & 1) == 0) col = TerrainBake.Mix(col, Black, Math.Min(perm + 80, 620));
            }
        }

        if (land is not (LandType.Water or LandType.Shallow or LandType.Cliff) && st.Shore is not null)
        {
            // Wet bank: ground right beside water is darker and a little blue.
            int d = Dist(nW, dW, nE, dE, nN, dN, nS, dS, n => n == (int)LandType.Water || n == (int)LandType.Shallow);
            int wet = 3 * k + (Vn(tx, ty, Math.Max(3, s / 2), Math.Max(3, s / 2), 98u) - 512) * k / 512;
            if (d < wet) col = TerrainBake.Mix(col, new TerrainPalette.Rgb(44, 48, 56), 300 * (wet - d) / wet);
        }

        return col;
    }
}
