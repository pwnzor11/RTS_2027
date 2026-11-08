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
            case PropKind.DeadTree: DeadTree(canvas, variant, palette); break;
            case PropKind.WreckHulk: WreckHulk(canvas, variant, palette); break;
            case PropKind.ConcreteBlock: ConcreteBlock(canvas, variant, palette); break;
            case PropKind.BrokenPylon: BrokenPylon(canvas, variant, palette); break;
            case PropKind.Snag: Snag(canvas, variant, palette); break;
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

    // ---- blocking props (M12-04): dark silhouettes, a clear step darker or lighter than the ground (design 16 s2.2) ----

    private static void Quad(PropCanvas c, int x0, int y0, int x1, int y1, int x2, int y2, int x3, int y3, TerrainPalette.Rgb col, int a = 255)
    {
        c.Triangle(x0, y0, x1, y1, x2, y2, col, a);
        c.Triangle(x0, y0, x2, y2, x3, y3, col, a);
    }

    /// <summary>A stripped dead tree: a forked dark trunk with a lit left edge and bare branches.</summary>
    private static void DeadTree(PropCanvas c, int variant, TerrainPalette p)
    {
        var bark = new TerrainPalette.Rgb(44, 36, 32);
        TerrainPalette.Rgb lit = Lift(bark, 380), snow = new(214, 222, 228);
        int lean = (variant - 1) * 3, top = 20 - variant * 4, bx = 32, by = 56;   // taller with the variant
        Shadow(c, bx, by - 2, 12, 5);
        int tx = bx + lean;
        Quad(c, bx - 4, by, tx - 2, top + 18, tx + 2, top + 18, bx + 4, by, bark);
        c.Line(bx - 4, by, tx - 2, top + 18, lit, 230);
        for (int i = 0; i < 4 + variant; i++)
        {
            uint h = H(PropKind.DeadTree, variant, i, 5);
            int fromY = by - 14 - (int)(h % 26u), side = (h & 0x100u) != 0 ? 1 : -1;
            int len = 8 + (int)((h >> 12) % 10u), rise = 5 + (int)((h >> 16) % 8u);
            int sx = bx + lean * (by - fromY) / Math.Max(1, by - top);
            c.Line(sx, fromY, sx + side * len, fromY - rise, bark);
            c.Line(sx + side * len / 2, fromY - rise / 2, sx + side * (len / 2 + 5), fromY - rise / 2 - 6, bark, 230);
            if (variant == 2) c.Line(sx, fromY - 1, sx + side * len, fromY - rise - 1, snow, 170);
        }

        c.Line(tx - 2, top + 18, tx - 1 + lean / 2, top, bark);
        c.Line(tx + 1, top + 18, tx + 6, top + 4, bark);
    }

    /// <summary>A wrecked vehicle hulk: rusted body, dark windows, flat tyres; variant 2 is a tank hull with a bent barrel.</summary>
    private static void WreckHulk(PropCanvas c, int variant, TerrainPalette p)
    {
        var rust = new TerrainPalette.Rgb(122, 66, 40);
        TerrainPalette.Rgb lit = Lift(rust, 260), dark = Sink(rust, 520), black = new(26, 24, 24);
        Shadow(c, 32, 46, 24, 8);
        if (variant == 2)
        {
            var olive = new TerrainPalette.Rgb(78, 82, 58);
            Quad(c, 5, 36, 59, 36, 61, 52, 3, 52, Sink(olive, 300));
            Quad(c, 7, 30, 57, 30, 59, 38, 5, 38, olive);
            c.Triangle(20, 30, 25, 18, 43, 18, olive); c.Triangle(20, 30, 43, 18, 46, 30, Lift(olive, 120));
            c.Line(42, 24, 60, 17, black); c.Line(42, 25, 60, 19, dark, 220);
            for (int i = 0; i < 7; i++) c.Ellipse(10 + i * 7, 53, 4, 3, black, 230);
            return;
        }

        int len = variant == 1 ? 21 : 13;   // a small car, then a truck, then (variant 2) a tank hull
        int x0 = 32 - len, x1 = 32 + len;
        Quad(c, x0, 36, x1, 36, x1 + 2, 48, x0 - 2, 48, dark);
        Quad(c, x0 + 2, 32, x1 - 2, 32, x1, 38, x0, 38, rust);
        Quad(c, x0 + 4, 24, x0 + 16, 24, x0 + 20, 33, x0 + 2, 33, lit);                 // cab roof
        Quad(c, x0 + 6, 26, x0 + 14, 26, x0 + 16, 32, x0 + 5, 32, black);               // window
        if (variant == 1) Quad(c, x0 + 22, 30, x1 - 1, 30, x1 - 1, 36, x0 + 22, 36, Sink(lit, 150));   // flat bed
        c.Ellipse(x0 + 8, 49, 5, 4, black, 240);
        c.Ellipse(x1 - 8, 49, 5, 4, black, 240);
        c.Line(x0 + 4, 36, x1 - 4, 40, dark, 200);                                        // rust seam
    }

    /// <summary>A concrete barrier: a low, wide jersey block with a yellow and black hazard band; variant 0 one block, 1 a longer cracked one,
    /// 2 two blocks end to end.</summary>
    private static void ConcreteBlock(PropCanvas c, int variant, TerrainPalette p)
    {
        var grey = new TerrainPalette.Rgb(168, 166, 158);
        TerrainPalette.Rgb lit = Lift(grey, 220), shade = Sink(grey, 330), stripe = new(222, 176, 34), black = new(34, 32, 30);
        Shadow(c, 32, 50, 26, 6);
        int blocks = variant == 2 ? 2 : 1;
        for (int b = 0; b < blocks; b++)
        {
            int ox = blocks == 2 ? (b == 0 ? -13 : 13) : 0, w = variant == 1 ? 20 : variant == 2 ? 12 : 15, top = 33, bot = 50;
            Quad(c, 32 + ox - w, bot, 32 + ox + w, bot, 32 + ox + w - 4, top, 32 + ox - w + 4, top, grey);            // front face
            Quad(c, 32 + ox + w, bot, 32 + ox + w + 4, bot - 4, 32 + ox + w, top - 4, 32 + ox + w - 4, top, shade);  // end face
            Quad(c, 32 + ox - w + 4, top, 32 + ox + w - 4, top, 32 + ox + w, top - 4, 32 + ox - w + 8, top - 4, lit);   // top
            for (int s = 0; s * 8 < 2 * w - 8; s++)                                                                  // hazard band across the front
            {
                int sx = 32 + ox - w + 5 + s * 8;
                Quad(c, sx, top + 2, sx + 4, top + 2, sx + 2, top + 8, sx - 2, top + 8, s % 2 == 0 ? stripe : black);
            }

            c.Line(32 + ox - w, bot, 32 + ox + w, bot, black, 200);
            if (variant == 1) c.Line(32 + ox - 2, top, 32 + ox + 4, bot, Sink(shade, 400));   // crack
        }
    }

    /// <summary>A broken pylon: a dark lattice tower snapped partway up with a stub of cable.</summary>
    private static void BrokenPylon(PropCanvas c, int variant, TerrainPalette p)
    {
        var steel = new TerrainPalette.Rgb(70, 58, 52);
        TerrainPalette.Rgb lit = Lift(steel, 420);
        int top = 28 - variant * 8;   // a taller stump with the variant
        Shadow(c, 32, 55, 15, 5);
        void Thick(int x0, int y0, int x1, int y1, TerrainPalette.Rgb col) { c.Line(x0, y0, x1, y1, col); c.Line(x0 + 1, y0, x1 + 1, y1, col); }
        Thick(19, 56, 28, top + 6, steel); Thick(43, 56, 34, top + 6, steel);
        c.Line(20, 56, 29, top + 6, lit, 220);
        for (int k = 0; k < 5; k++)
        {
            int y = 54 - k * 8;
            if (y < top + 8) break;
            int half = 12 - k * 2;
            Thick(32 - half, y, 32 + half, y - 6, steel);
            c.Line(32 + half, y, 32 - half, y - 6, steel);
        }

        Thick(28, top + 6, 22, top, steel);                // snapped top
        Thick(34, top + 6, 40 + variant * 2, top + 1, steel);
        c.Line(40 + variant * 2, top + 1, 54, top + 14 + variant * 4, new TerrainPalette.Rgb(20, 18, 18), 230);   // cable stub
    }

    /// <summary>Driftwood and debris: crossed logs and a plank, bleached at the lit end.</summary>
    private static void Snag(PropCanvas c, int variant, TerrainPalette p)
    {
        var wood = new TerrainPalette.Rgb(112, 92, 72);
        TerrainPalette.Rgb lit = Lift(wood, 380), dark = Sink(wood, 450);
        Shadow(c, 32, 46, 20, 7);
        int logs = 3 + variant;
        for (int i = 0; i < logs; i++)
        {
            uint h = H(PropKind.Snag, variant, i, 6);
            int cx = 32 + (int)(h % 14u) - 7, cy = 42 + (int)((h >> 8) % 8u) - 4, len = 11 + (int)((h >> 12) % 9u), rise = (int)((h >> 16) % 11u) - 5;
            c.Line(cx - len, cy + rise, cx + len, cy - rise, dark);
            c.Line(cx - len, cy + rise - 1, cx + len, cy - rise - 1, wood);
            c.Line(cx - len, cy + rise - 2, cx - len / 2, cy + rise / 2 - 2, lit, 220);
            c.Ellipse(cx + len, cy - rise, 2, 2, lit, 230);
        }

        c.Line(22, 36, 30, 28, dark); c.Line(40, 30, 46, 38, dark, 220);   // stubby branches
    }
}
