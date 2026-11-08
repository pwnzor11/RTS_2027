// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// Bakes a map's terrain into one RGBA texture, once per map. Pure integer maths with no GPU types, so it is testable
/// and byte-identical for the same map and palette.
/// </summary>
/// <remarks>
/// Algorithm (mirrored exactly by <c>tools/maps/make_rustbelt_m2.py</c>):
/// <list type="bullet">
/// <item>Each cell is <c>TexelCells</c> texels square. Texel (tx, ty) belongs to cell (tx / S, ty / S).</item>
/// <item>Shade: a cell-scale blotch hash(tx>>3, ty>>3, 1) % 8 gives dark (0), light (1) or base (other); one texel in
/// six, by hash(tx, ty, 2), is nudged one shade.</item>
/// <item>Pitchblende: a 2x2-texel chunk where hash(tx>>1, ty>>1, 4) % 1000 is below density * max / 255, drawn in the
/// chunk colour, with a glint where hash(tx, ty, 5) % GlintOneIn is 0.</item>
/// <item>Ashfall: a texel where hash(tx, ty, 3) % 1000 is below density * max / 255 takes the speck colour.</item>
/// <item>Edges: a texel on a cell edge blends 50% toward the neighbour's base colour when the land type differs
/// (left, right, top, then bottom).</item>
/// </list>
/// Optional <see cref="TerrainPalette.TerrainStyle"/> passes (all off by default, so the algorithm above is unchanged): large-scale tone
/// noise, road edge lines, a shore band, cliff lip highlight with a drop shadow, and resource fields drawn as heaps or crystals with a rim.
/// The hash is a 32-bit integer mix (wrapping arithmetic); thresholds are integer, so speckle only grows with density.
/// </remarks>
public static class TerrainBake
{
    /// <summary>A baked texture: RGBA bytes, row-major, four bytes per texel, alpha always 255.</summary>
    /// <param name="Width">Texels across (map width times TexelCells).</param>
    /// <param name="Height">Texels down (map height times TexelCells).</param>
    /// <param name="Rgba">The texel bytes.</param>
    public readonly record struct Result(int Width, int Height, byte[] Rgba);

    /// <summary>Bakes the whole map with the palette of its theater (biome from <paramref name="meta"/>; no meta = header theater).</summary>
    public static Result Bake(MapData map, Rts.Data.MapMeta? meta) => Bake(map, TerrainPalette.ForMap(map, meta));

    /// <summary>Bakes the whole map.</summary>
    public static Result Bake(MapData map, TerrainPalette palette)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(palette);

        int s = palette.TexelCells;
        int w = map.Width, h = map.Height;
        int tw = w * s, th = h * s;
        var rgba = new byte[tw * th * 4];
        TerrainPalette.TerrainStyle style = palette.Style;
        bool fields = style.Ashfall is not null || style.Pitchblende is not null;
        bool edges = style.HasEdgeEffects;

        for (int ty = 0; ty < th; ty++)
        {
            int cy = ty / s, sy = ty % s;
            for (int tx = 0; tx < tw; tx++)
            {
                int cx = tx / s, sx = tx % s;
                int k = cy * w + cx;
                LandType land = map.Land[k];
                OverlayKind overlay = map.Overlay[k];
                int dens = map.Density[k];

                TerrainPalette.Rgb col = Texel(palette, land, overlay, dens, tx, ty, fields);
                if (fields && overlay is OverlayKind.Ashfall or OverlayKind.Pitchblende)
                {
                    TerrainPalette.FieldLook? look = overlay == OverlayKind.Ashfall ? style.Ashfall : style.Pitchblende;
                    if (look is not null) col = FieldTexel(palette, look, map, k, cx, cy, sx, sy, overlay, dens, col, palette.PitchGlint);
                }

                if (sx == 0 && cx > 0 && map.Land[k - 1] != land) col = Average(col, palette.ShadesFor(map.Land[k - 1]).Base);
                if (sx == s - 1 && cx < w - 1 && map.Land[k + 1] != land) col = Average(col, palette.ShadesFor(map.Land[k + 1]).Base);
                if (sy == 0 && cy > 0 && map.Land[k - w] != land) col = Average(col, palette.ShadesFor(map.Land[k - w]).Base);
                if (sy == s - 1 && cy < h - 1 && map.Land[k + w] != land) col = Average(col, palette.ShadesFor(map.Land[k + w]).Base);

                if (edges) col = Decorate(col, map, style, cx, cy, sx, sy, s);

                int o = (ty * tw + tx) * 4;
                rgba[o] = col.R;
                rgba[o + 1] = col.G;
                rgba[o + 2] = col.B;
                rgba[o + 3] = 255;
            }
        }

        return new Result(tw, th, rgba);
    }

    private static TerrainPalette.Rgb Texel(TerrainPalette palette, LandType land, OverlayKind overlay, int dens, int tx, int ty, bool fields)
    {
        TerrainPalette.Rgb col;
        bool pitched = false;
        if (fields && ((overlay == OverlayKind.Ashfall && palette.Style.Ashfall is not null)
            || (overlay == OverlayKind.Pitchblende && palette.Style.Pitchblende is not null)))
        {
            return Shade(palette, land, tx, ty);   // the field look paints the cell
        }

        if (overlay == OverlayKind.Pitchblende)
        {
            int thr = dens * palette.PitchMaxPermille / 255;
            if ((int)(Hash3((uint)(tx >> 1), (uint)(ty >> 1), 4) % 1000u) < thr)
            {
                pitched = true;
                col = Hash3((uint)tx, (uint)ty, 5) % (uint)palette.GlintOneIn == 0 ? palette.PitchGlint : palette.PitchChunk;
            }
            else col = Shade(palette, land, tx, ty);
        }
        else col = Shade(palette, land, tx, ty);

        if (!pitched && overlay == OverlayKind.Ashfall
            && (int)(Hash3((uint)tx, (uint)ty, 3) % 1000u) < dens * palette.AshfallMaxPermille / 255)
        {
            col = palette.AshfallSpeck;
        }

        return col;
    }

    private static TerrainPalette.Rgb Shade(TerrainPalette palette, LandType land, int tx, int ty)
    {
        TerrainPalette.TerrainStyle st = palette.Style;
        int c = (int)(Hash3((uint)(tx >> st.BlotchShift), (uint)(ty >> st.BlotchShift), 1) % 8u) switch
        {
            0 => -1,
            1 => 1,
            _ => 0,
        };
        uint f = Hash3((uint)tx, (uint)ty, 2);
        if (f % 6u == 0) c += (f >> 8 & 1u) != 0 ? 1 : -1;
        c = Math.Clamp(c, -1, 1);

        TerrainPalette.LandShades shades = palette.ShadesFor(land);
        TerrainPalette.Rgb col = c < 0 ? shades.Dark : c > 0 ? shades.Light : shades.Base;
        if (st.ToneAmp > 0 && land is not (LandType.Water or LandType.Shallow or LandType.Cliff or LandType.Road))
        {
            int off = Tone(tx, ty, st.ToneCells * palette.TexelCells, st.ToneAmp);
            col = new TerrainPalette.Rgb(Clamp(col.R + off), Clamp(col.G + off), Clamp(col.B + off));
        }

        return col;
    }

    /// <summary>
    /// Smooth large-scale tone: integer value noise on a lattice <paramref name="period"/> texels apart, blended with a smoothstep,
    /// returning an offset in -<paramref name="amp"/>..+<paramref name="amp"/> RGB levels. Pure and deterministic.
    /// </summary>
    public static int Tone(int tx, int ty, int period, int amp)
    {
        if (amp <= 0 || period <= 0) return 0;
        int gx = tx / period, gy = ty / period;
        int fx = (tx % period) * 256 / period, fy = (ty % period) * 256 / period;
        int wx = fx * fx * (768 - 2 * fx) >> 16, wy = fy * fy * (768 - 2 * fy) >> 16;   // 0..256
        int v00 = Lattice(gx, gy, amp), v10 = Lattice(gx + 1, gy, amp), v01 = Lattice(gx, gy + 1, amp), v11 = Lattice(gx + 1, gy + 1, amp);
        int top = v00 * (256 - wx) + v10 * wx;
        int bottom = v01 * (256 - wx) + v11 * wx;
        int v = top * (256 - wy) + bottom * wy;   // scaled by 65536
        return (v + (v >= 0 ? 32768 : -32768)) / 65536;
    }

    private static int Lattice(int gx, int gy, int amp) => (int)(Hash3((uint)gx, (uint)gy, 9) % (uint)(2 * amp + 1)) - amp;

    private static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);

    /// <summary>Mixes <paramref name="b"/> into <paramref name="a"/> by <paramref name="permille"/> (0 = a, 1000 = b).</summary>
    public static TerrainPalette.Rgb Mix(TerrainPalette.Rgb a, TerrainPalette.Rgb b, int permille)
    {
        permille = Math.Clamp(permille, 0, 1000);
        return new TerrainPalette.Rgb(
            (byte)((a.R * (1000 - permille) + b.R * permille) / 1000),
            (byte)((a.G * (1000 - permille) + b.G * permille) / 1000),
            (byte)((a.B * (1000 - permille) + b.B * permille) / 1000));
    }

    private static int LandAt(MapData map, int cx, int cy) =>
        cx < 0 || cy < 0 || cx >= map.Width || cy >= map.Height ? -1 : (int)map.Land[cy * map.Width + cx];

    /// <summary>Road edge lines, shore band, cliff lip highlight and drop shadow for one texel.</summary>
    private static TerrainPalette.Rgb Decorate(TerrainPalette.Rgb col, MapData map, TerrainPalette.TerrainStyle st, int cx, int cy, int sx, int sy, int s)
    {
        LandType land = map.Land[cy * map.Width + cx];
        int nW = LandAt(map, cx - 1, cy), nE = LandAt(map, cx + 1, cy), nN = LandAt(map, cx, cy - 1), nS = LandAt(map, cx, cy + 1);
        int dW = sx, dE = s - 1 - sx, dN = sy, dS = s - 1 - sy;

        if (land == LandType.Road && st.RoadEdge is { } line)
        {
            int d = EdgeDistance(nW, dW, nE, dE, nN, dN, nS, dS, n => n >= 0 && n != (int)LandType.Road);
            if (d < st.RoadEdgeTexels) col = Mix(col, line, 850);
        }
        else if (land is LandType.Water or LandType.Shallow && st.Shore is { } foam)
        {
            int d = EdgeDistance(nW, dW, nE, dE, nN, dN, nS, dS, n => n >= 0 && n != (int)LandType.Water && n != (int)LandType.Shallow);
            if (d < st.ShoreTexels) col = Mix(col, foam, 800 * (st.ShoreTexels - d) / st.ShoreTexels);
        }
        else if (land == LandType.Cliff && st.CliffTop is { } top)
        {
            int lip = EdgeDistance(nW, dW, int.MinValue, 0, nN, dN, int.MinValue, 0, n => n >= 0 && n != (int)LandType.Cliff);
            int face = EdgeDistance(int.MinValue, 0, nE, dE, int.MinValue, 0, nS, dS, n => n >= 0 && n != (int)LandType.Cliff);
            if (lip < st.CliffTopTexels) col = Mix(col, top, 900);
            else if (face < st.CliffTopTexels) col = Mix(col, new TerrainPalette.Rgb(0, 0, 0), 450);
        }

        if (land != LandType.Cliff && st.CliffShadowPermille > 0)
        {
            int n = st.CliffShadowTexels, perm = 0;
            if (nN == (int)LandType.Cliff && sy < n) perm = st.CliffShadowPermille * (n - sy) / n;
            if (nW == (int)LandType.Cliff && sx < n) perm = Math.Max(perm, st.CliffShadowPermille * (n - sx) / (2 * n));
            if (perm > 0) col = Mix(col, new TerrainPalette.Rgb(0, 0, 0), perm);
        }

        return col;
    }

    private static int EdgeDistance(int nW, int dW, int nE, int dE, int nN, int dN, int nS, int dS, Func<int, bool> match)
    {
        int best = int.MaxValue;
        if (nW != int.MinValue && match(nW)) best = Math.Min(best, dW);
        if (nE != int.MinValue && match(nE)) best = Math.Min(best, dE);
        if (nN != int.MinValue && match(nN)) best = Math.Min(best, dN);
        if (nS != int.MinValue && match(nS)) best = Math.Min(best, dS);
        return best;
    }

    /// <summary>
    /// One texel of a resource cell drawn as a deposit: ground tinted toward the rim colour (the "bed"), a rim where the field meets other
    /// ground, and 1..<c>FieldMaxHeaps</c> heaps (ashfall: round slag, lit north-west) or crystals (pitchblende: tall diamonds with a lit
    /// and a shaded facet) whose count and size grow with density. A heap casts a one-texel contact shadow.
    /// </summary>
    private static TerrainPalette.Rgb FieldTexel(TerrainPalette palette, TerrainPalette.FieldLook look, MapData map, int k, int cx, int cy,
        int sx, int sy, OverlayKind overlay, int dens, TerrainPalette.Rgb ground, TerrainPalette.Rgb glint)
    {
        TerrainPalette.TerrainStyle st = palette.Style;
        int s = palette.TexelCells, w = map.Width, h = map.Height;
        TerrainPalette.Rgb col = ground;
        if (st.FieldBedPermille > 0) col = Mix(col, look.Rim, st.FieldBedPermille + dens * 150 / 255);

        bool crystal = overlay == OverlayKind.Pitchblende;
        int rMin = Math.Max(1, s / 8), rMax = Math.Max(rMin, crystal ? s / 5 : s / 4);
        int r0 = rMin + (rMax - rMin) * dens / 255;
        int count = 1 + (st.FieldMaxHeaps - 1) * dens / 255;
        for (int i = 0; i < count; i++)
        {
            uint hv = Hash3((uint)(cx * 8 + i), (uint)cy, (uint)(crystal ? 12 : 11));
            int r = Math.Max(1, r0 - (int)((hv >> 20) & 1u));
            int hh = crystal ? r + r / 2 : r;        // half height
            int mx = r + 1, my = hh + 1;
            int px = mx + (int)(hv % (uint)Math.Max(1, s - 2 * mx));
            int py = my + (int)((hv >> 8) % (uint)Math.Max(1, s - 2 * my));
            int dx = sx - px, dy = sy - py;
            if (crystal)
            {
                int q = Math.Abs(dx) * hh + Math.Abs(dy) * r;       // diamond: half-width r, half-height hh
                if (q <= r * hh)
                {
                    col = dx < 0 ? look.Light : dx > 0 ? look.Shade : look.Body;
                    if (dx == 0 && dy <= -hh + 1 && dens >= 128) col = glint;
                }
                else if (q <= r * hh + Math.Max(r, hh)) col = Mix(col, look.Rim, 550);
            }
            else
            {
                int d2 = dx * dx + dy * dy;
                if (d2 <= r * r)
                {
                    int t = (dx + dy) * 3;
                    col = t < -r ? look.Light : t > r ? look.Shade : look.Body;
                }
                else if (d2 <= (r + 1) * (r + 1)) col = Mix(col, look.Rim, 550);
            }
        }

        int rim = st.FieldRimTexels;
        if (rim > 0)
        {
            bool edge = (sx < rim && cx > 0 && map.Overlay[k - 1] != overlay)
                || (sx >= s - rim && cx < w - 1 && map.Overlay[k + 1] != overlay)
                || (sy < rim && cy > 0 && map.Overlay[k - w] != overlay)
                || (sy >= s - rim && cy < h - 1 && map.Overlay[k + w] != overlay);
            if (edge) col = Mix(col, look.Rim, 750);
        }

        return col;
    }

    private static TerrainPalette.Rgb Average(TerrainPalette.Rgb a, TerrainPalette.Rgb b) =>
        new((byte)((a.R + b.R) / 2), (byte)((a.G + b.G) / 2), (byte)((a.B + b.B) / 2));

    private static uint Hash3(uint a, uint b, uint salt)
    {
        unchecked
        {
            uint h = a * 0x9E3779B1u + b * 0x85EBCA77u + salt * 0xC2B2AE3Du + 0x27D4EB2Fu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }
}
