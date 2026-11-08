// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>
/// Procedural placeholder art for the land-linked sprites (M12-02), drawn in code like <see cref="PlaceholderArt"/>: no copied art, no
/// files. Real art (M10) replaces it through <see cref="IPropAtlas"/>. Every frame is <see cref="FramePx"/> square (one zoom-1 cell),
/// anchored at its centre, lit from the north-west with a soft ground shadow to the south-east, and coloured from the theater palette so
/// a prop is a clear step lighter than the ground it stands on.
/// </summary>
public static class PropArt
{
    /// <summary>Frame size in pixels (a cell at zoom 1).</summary>
    public const int FramePx = 64;

    /// <summary>Variants drawn per kind: 0 small, 1 medium, 2 large.</summary>
    public const int Variants = 3;

    private static readonly int[] Cos = { 1024, 724, 0, -724, -1024, -724, 0, 724 };
    private static readonly int[] Sin = { 0, 724, 1024, 724, 0, -724, -1024, -724 };
    private static readonly TerrainPalette.Rgb Black = new(0, 0, 0);
    private static readonly TerrainPalette.Rgb White = new(255, 255, 255);

    /// <summary>Draws one frame: premultiplied RGBA, <see cref="FramePx"/> x <see cref="FramePx"/>.</summary>
    public static byte[] Render(PropKind kind, int variant, TerrainPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        variant = Math.Clamp(variant, 0, Variants - 1);
        var canvas = new PropCanvas(FramePx, FramePx);
        switch (kind)
        {
            case PropKind.Boulder: Boulder(canvas, variant, palette); break;
            case PropKind.Rubble: Rubble(canvas, variant, palette); break;
            case PropKind.GlassCrest: GlassCrest(canvas, variant, palette); break;
            case PropKind.IceSlab: IceSlab(canvas, variant, palette); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return canvas.Rgba;
    }

    private static uint H(PropKind kind, int variant, int i, uint salt) => DecorHash.Mix(0x5052_4F50UL + (ulong)kind, variant, i, salt);

    private static TerrainPalette.Rgb Lift(TerrainPalette.Rgb c, int permille) => TerrainBake.Mix(c, White, permille);

    private static TerrainPalette.Rgb Sink(TerrainPalette.Rgb c, int permille) => TerrainBake.Mix(c, Black, permille);

    private static void Shadow(PropCanvas c, int cx, int cy, int rx, int ry) => c.Ellipse(cx + 3, cy + 3, rx, Math.Max(2, ry * 2 / 3), Black, 85);

    /// <summary>A faceted boulder: an eight-sided blob in triangular facets, a lighter cap and a dark outline.</summary>
    private static void Boulder(PropCanvas c, int variant, TerrainPalette p)
    {
        TerrainPalette.LandShades rock = p.ShadesFor(Rts.Sim.Rules.LandType.Rock);
        TerrainPalette.Rgb lit = Lift(rock.Light, 380), mid = Lift(rock.Base, 230), dark = rock.Dark, edge = Sink(rock.Dark, 600);
        int rx = 13 + variant * 6, ry = 10 + variant * 4, cx = 32, cy = 34;
        Shadow(c, cx, cy + ry / 2, rx, ry);
        var vx = new int[8];
        var vy = new int[8];
        for (int i = 0; i < 8; i++)
        {
            int j = 85 + (int)(H(PropKind.Boulder, variant, i, 1) % 31u);   // 85..115 %
            vx[i] = cx + Cos[i] * rx * j / 102400;
            vy[i] = cy + Sin[i] * ry * j / 102400;
        }

        for (int i = 0; i < 8; i++)
        {
            int n = (i + 1) & 7;
            int mx = vx[i] + vx[n] - 2 * cx, my = vy[i] + vy[n] - 2 * cy;      // direction of the facet's outer edge
            int light = -(mx + my);                                             // lit from the north-west
            c.Triangle(cx, cy, vx[i], vy[i], vx[n], vy[n], light > rx / 2 ? lit : light < -rx / 2 ? dark : mid);
        }

        // cap: a smaller, lighter polygon offset to the light
        for (int i = 0; i < 8; i++)
        {
            int n = (i + 1) & 7;
            int ax = cx - 2 + (vx[i] - cx) * 5 / 10, ay = cy - 3 + (vy[i] - cy) * 5 / 10;
            int bx = cx - 2 + (vx[n] - cx) * 5 / 10, by = cy - 3 + (vy[n] - cy) * 5 / 10;
            int light = -((ax + bx - 2 * cx) + (ay + by - 2 * cy));
            c.Triangle(cx - 2, cy - 3, ax, ay, bx, by, light > 0 ? Lift(lit, 120) : lit);
        }

        for (int i = 0; i < 8; i++) c.Line(vx[i], vy[i], vx[(i + 1) & 7], vy[(i + 1) & 7], edge, 200);
    }

    /// <summary>Broken brick and concrete: angular chunks in the rough-ground colours, a rebar stick or two.</summary>
    private static void Rubble(PropCanvas c, int variant, TerrainPalette p)
    {
        TerrainPalette.LandShades rough = p.ShadesFor(Rts.Sim.Rules.LandType.Rough);
        TerrainPalette.Rgb[] tone = { Lift(rough.Light, 320), Lift(rough.Base, 200), Sink(rough.Dark, 150) };
        TerrainPalette.Rgb edge = Sink(rough.Dark, 650);
        int chunks = 5 + variant * 2, spread = 8 + variant * 3;
        Shadow(c, 32, 36, spread + 6, 9 + variant * 2);
        for (int i = 0; i < chunks; i++)
        {
            uint h = H(PropKind.Rubble, variant, i, 2);
            int cx = 32 + (int)(h % (uint)(2 * spread + 1)) - spread;
            int cy = 36 + (int)((h >> 8) % 11u) - 5 - variant;
            int sz = 3 + (int)((h >> 16) % 4u) + variant;
            int skew = (int)((h >> 20) % 5u) - 2;
            TerrainPalette.Rgb top = tone[(int)((h >> 24) % 3u)];
            c.Triangle(cx - sz, cy + sz / 2, cx + skew, cy - sz, cx + sz, cy + sz / 2, top);                 // lit face
            c.Triangle(cx + sz, cy + sz / 2, cx + skew, cy - sz, cx + sz + 2, cy - sz / 2, Sink(top, 350));  // shaded side
            c.Line(cx - sz, cy + sz / 2, cx + sz, cy + sz / 2, edge, 190);
        }

        for (int i = 0; i < 1 + variant; i++)   // rebar
        {
            uint h = H(PropKind.Rubble, variant, 40 + i, 3);
            int x = 24 + (int)(h % 14u), y = 30 + (int)((h >> 8) % 6u), len = 9 + (int)((h >> 16) % 7u);
            c.Line(x, y, x + len, y - len / 2 - 2, new TerrainPalette.Rgb(58, 40, 30));
            c.Line(x, y - 1, x + len, y - len / 2 - 3, new TerrainPalette.Rgb(140, 84, 52), 220);
        }
    }

    /// <summary>A cluster of fused-glass spikes: a lit left face, a dark right face and a bright edge.</summary>
    private static void GlassCrest(PropCanvas c, int variant, TerrainPalette p)
    {
        TerrainPalette.LandShades rock = p.ShadesFor(Rts.Sim.Rules.LandType.Rock);
        var glass = new TerrainPalette.Rgb(120, 220, 205);
        TerrainPalette.Rgb lit = TerrainBake.Mix(Lift(rock.Light, 250), glass, 550), shade = TerrainBake.Mix(rock.Dark, glass, 250), spark = new(235, 255, 248);
        int spikes = 3 + variant;
        Shadow(c, 32, 44, 9 + spikes * 3, 6 + variant);
        for (int i = 0; i < spikes; i++)
        {
            uint h = H(PropKind.GlassCrest, variant, i, 4);
            int bx = 32 + (i - spikes / 2) * 7 + (int)(h % 5u) - 2, by = 46 + (int)((h >> 8) % 4u) - 1;
            int height = 14 + (int)((h >> 12) % 10u) + variant * 5, half = 3 + (int)((h >> 16) % 3u), lean = (int)((h >> 20) % 7u) - 3;
            int tipx = bx + lean, tipy = by - height;
            c.Triangle(bx - half, by, tipx, tipy, bx, by + 1, lit, 235);
            c.Triangle(bx, by + 1, tipx, tipy, bx + half, by, shade, 235);
            c.Line(bx - half, by, tipx, tipy, spark, 230);
        }
    }

    /// <summary>A tilted slab of ice: a pale top face, a bluer front edge and a hairline crack.</summary>
    private static void IceSlab(PropCanvas c, int variant, TerrainPalette p)
    {
        TerrainPalette.Rgb foam = p.Style.Shore ?? new TerrainPalette.Rgb(205, 232, 242);
        TerrainPalette.Rgb top = Lift(foam, 250), face = TerrainBake.Mix(foam, new TerrainPalette.Rgb(70, 120, 170), 550), edge = TerrainBake.Mix(foam, new TerrainPalette.Rgb(40, 80, 130), 700);
        int w = 12 + variant * 4, hgt = 7 + variant * 2, skew = 4 + variant, cx = 32, cy = 34, depth = 4 + variant;
        c.Ellipse(cx + 2, cy + depth + 2, w + 4, hgt / 2 + 3, Black, 70);
        // top face: a parallelogram leaning to the right
        c.Triangle(cx - w, cy + hgt / 2, cx - w + skew, cy - hgt / 2, cx + w + skew, cy - hgt / 2, top);
        c.Triangle(cx - w, cy + hgt / 2, cx + w + skew, cy - hgt / 2, cx + w, cy + hgt / 2, top);
        // front edge
        c.Triangle(cx - w, cy + hgt / 2, cx + w, cy + hgt / 2, cx - w, cy + hgt / 2 + depth, face);
        c.Triangle(cx + w, cy + hgt / 2, cx + w, cy + hgt / 2 + depth, cx - w, cy + hgt / 2 + depth, face);
        // right side
        c.Triangle(cx + w, cy + hgt / 2, cx + w + skew, cy - hgt / 2, cx + w, cy + hgt / 2 + depth, edge);
        c.Line(cx - w, cy + hgt / 2, cx + w, cy + hgt / 2, White, 200);
        c.Line(cx - w + skew, cy - hgt / 2, cx + w + skew, cy - hgt / 2, White, 160);
        c.Line(cx - 2, cy - hgt / 2 + 1, cx + 1 + skew / 2, cy + hgt / 2 - 1, edge, 150);   // crack
    }
}
