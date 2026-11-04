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

                TerrainPalette.Rgb col = Texel(palette, land, overlay, dens, tx, ty);

                if (sx == 0 && cx > 0 && map.Land[k - 1] != land) col = Average(col, palette.ShadesFor(map.Land[k - 1]).Base);
                if (sx == s - 1 && cx < w - 1 && map.Land[k + 1] != land) col = Average(col, palette.ShadesFor(map.Land[k + 1]).Base);
                if (sy == 0 && cy > 0 && map.Land[k - w] != land) col = Average(col, palette.ShadesFor(map.Land[k - w]).Base);
                if (sy == s - 1 && cy < h - 1 && map.Land[k + w] != land) col = Average(col, palette.ShadesFor(map.Land[k + w]).Base);

                int o = (ty * tw + tx) * 4;
                rgba[o] = col.R;
                rgba[o + 1] = col.G;
                rgba[o + 2] = col.B;
                rgba[o + 3] = 255;
            }
        }

        return new Result(tw, th, rgba);
    }

    private static TerrainPalette.Rgb Texel(TerrainPalette palette, LandType land, OverlayKind overlay, int dens, int tx, int ty)
    {
        TerrainPalette.Rgb col;
        bool pitched = false;
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
        int c = (int)(Hash3((uint)(tx >> 3), (uint)(ty >> 3), 1) % 8u) switch
        {
            0 => -1,
            1 => 1,
            _ => 0,
        };
        uint f = Hash3((uint)tx, (uint)ty, 2);
        if (f % 6u == 0) c += (f >> 8 & 1u) != 0 ? 1 : -1;
        c = Math.Clamp(c, -1, 1);

        TerrainPalette.LandShades shades = palette.ShadesFor(land);
        return c < 0 ? shades.Dark : c > 0 ? shades.Light : shades.Base;
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
