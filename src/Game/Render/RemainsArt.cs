// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>
/// Procedural placeholder art for the remains of the dead (D-184), drawn in code like <see cref="PropArt"/> and <see cref="PlaceholderArt"/>:
/// no files, no copied art. Building rubble, vehicle wreck, infantry smudge and scorch decal, lit from the upper left with the shadow to the lower
/// right, hard pixels (the scorch edge is dithered, not soft). A faction only changes the palette (<see cref="Tones"/>): the shapes are shared.
/// Real art comes in M10 and replaces this file's output, not the layer that places it.
/// </summary>
public static class RemainsArt
{
    /// <summary>Margin around a building footprint that its rubble canvas covers, on every side, in pixels.</summary>
    public const int RubbleMargin = 8;

    /// <summary>Vehicle wreck frame width.</summary>
    public const int WreckW = 88;

    /// <summary>Vehicle wreck frame height.</summary>
    public const int WreckH = 64;

    /// <summary>Pixel of the wreck frame that sits on the vehicle's ground point (X).</summary>
    public const int WreckOriginX = 44;

    /// <summary>Pixel of the wreck frame that sits on the vehicle's ground point (Y).</summary>
    public const int WreckOriginY = 42;

    /// <summary>Infantry smudge frame width.</summary>
    public const int MarkW = 28;

    /// <summary>Infantry smudge frame height.</summary>
    public const int MarkH = 16;

    /// <summary>Ground ellipses are squashed to this share (permille) of their width: sin 40 degrees, the camera's elevation.</summary>
    public const int GroundSquashPermille = 643;

    private static readonly TerrainPalette.Rgb Black = new(0, 0, 0);
    private static readonly TerrainPalette.Rgb Ash = new(34, 30, 28);

    /// <summary>Dark, mid and light concrete or metal of a faction (<see cref="Rts.Sim.Rules.Faction"/> as an int), from the median colours of its sprites.</summary>
    public static (TerrainPalette.Rgb Dark, TerrainPalette.Rgb Mid, TerrainPalette.Rgb Light) Tones(int faction) => faction switch
    {
        1 => (new(50, 44, 40), new(96, 90, 84), new(150, 140, 126)),     // Directorate: warm grey
        3 => (new(46, 34, 26), new(96, 78, 56), new(180, 138, 68)),      // Left Behind: rust and scrap
        4 => (new(50, 56, 64), new(118, 130, 140), new(170, 180, 188)),  // Expedition: cool light grey
        _ => (new(44, 50, 48), new(88, 98, 86), new(136, 150, 120)),     // Authority (and neutral): grey-green
    };

    private static TerrainPalette.Rgb Sink(TerrainPalette.Rgb c, int permille) => TerrainBake.Mix(c, Black, permille);

    private static TerrainPalette.Rgb Lift(TerrainPalette.Rgb c, int permille) => TerrainBake.Mix(c, new TerrainPalette.Rgb(255, 255, 255), permille);

    private static uint H(uint seed, int i, uint salt) => DecorHash.Mix(0x5245_4D41UL + salt, (int)seed, i, seed >> 7);

    /// <summary>Canvas size of the rubble of a <paramref name="widthCells"/> x <paramref name="heightCells"/> footprint.</summary>
    public static (int W, int H) RubbleSize(int widthCells, int heightCells) =>
        (widthCells * Camera.CellPx + 2 * RubbleMargin, heightCells * Camera.CellPx + 2 * RubbleMargin);

    /// <summary>
    /// A low heap of broken slabs, rebar and burnt chunks on a dark scorched pad, filling the footprint (plus the margin) and highest at the
    /// middle. The canvas is <see cref="RubbleSize"/>; draw it with its top-left <see cref="RubbleMargin"/> pixels above and left of the footprint.
    /// </summary>
    public static PropCanvas Rubble(int widthCells, int heightCells, int faction, uint seed)
    {
        (int cw, int ch) = RubbleSize(Math.Max(1, widthCells), Math.Max(1, heightCells));
        var c = new PropCanvas(cw, ch);
        var (dark, mid, light) = Tones(faction);
        int cx = cw / 2, cy = ch / 2, rx = cw / 2 - 12, ry = ch / 2 - 12;
        // the pad: a scorched, slightly ragged rectangle that fills the footprint; its rim is dithered
        for (int y = 5; y < ch - 5; y++)
        {
            for (int x = 5; x < cw - 5; x++)
            {
                int edge = Math.Min(Math.Min(x - 5, cw - 6 - x), Math.Min(y - 5, ch - 6 - y));
                int rag = (int)(H(seed, (x / 5) * 64 + y / 5, 9) % 9u);
                if (edge < rag) continue;
                if (edge < 9 && ((x + y) & 1) == 0) continue;
                c.Over(x, y, Ash, edge < 9 ? 105 : 135);
            }
        }

        int count = 9 * widthCells * heightCells + 10;
        var xs = new int[count];
        var ys = new int[count];
        var sz = new int[count];
        var tn = new int[count];
        for (int i = 0; i < count; i++)
        {
            uint h = H(seed, i, 1);
            int x = cx + (int)(h % (uint)(2 * rx + 1)) - rx;
            int y = cy + (int)((h >> 12) % (uint)(2 * ry + 1)) - ry;
            long ex = (long)(x - cx) * 1000 / rx, ey = (long)(y - cy) * 1000 / ry;
            int near = (int)Math.Max(0, 1000 - Math.Max(Math.Abs(ex), Math.Abs(ey)));   // 1000 at the middle, 0 at the rim
            uint g = H(seed, i, 2);
            xs[i] = x;
            ys[i] = y - near / 120;                       // the heap rises toward the middle
            sz[i] = 3 + near / 140 + (int)(g % 4u);
            tn[i] = (int)((g >> 8) % 4u);                 // 0 light, 1 mid, 2 dark, 3 burnt
        }

        int[] order = Enumerable.Range(0, count).OrderBy(i => ys[i]).ThenBy(i => xs[i]).ToArray();
        foreach (int i in order)
        {
            int x = xs[i], y = ys[i], s = sz[i], skew = (int)(H(seed, i, 3) % 5u) - 2, th = 2 + s / 3;
            TerrainPalette.Rgb top = tn[i] switch { 0 => light, 1 => mid, 2 => dark, _ => Sink(mid, 650) };
            TerrainPalette.Rgb front = Sink(top, 330), edge = Sink(dark, 600);
            c.Ellipse(x + 3, y + th + 2, s + 2, Math.Max(2, s / 2), Black, 70);                          // shadow, lower right
            c.Triangle(x - s, y, x - s + skew, y - s / 2 - 1, x + s + skew, y - s / 2 - 1, top);        // top face
            c.Triangle(x - s, y, x + s + skew, y - s / 2 - 1, x + s, y, top);
            c.Triangle(x - s, y, x + s, y, x - s, y + th, front);                                       // front face
            c.Triangle(x + s, y, x + s, y + th, x - s, y + th, front);
            c.Line(x - s, y + th, x + s, y + th, edge, 200);
            if (tn[i] != 3) c.Line(x - s, y - 0, x - s + skew, y - s / 2 - 1, Lift(top, 220), 200);     // lit left edge
        }

        int stubs = 2 + widthCells * heightCells / 2;
        for (int i = 0; i < stubs; i++)   // broken wall stubs standing near the edge: they give the heap a silhouette
        {
            uint h = H(seed, i, 10);
            bool horizontal = (h & 1) == 0;
            int x = horizontal ? cx + (int)((h >> 4) % (uint)(2 * rx + 1)) - rx : cx + ((h & 2) == 0 ? -rx : rx) + (int)((h >> 8) % 7u) - 3;
            int y = horizontal ? cy + ((h & 2) == 0 ? -ry : ry) + (int)((h >> 8) % 7u) - 3 : cy + (int)((h >> 4) % (uint)(2 * ry + 1)) - ry;
            int sw = 6 + (int)((h >> 12) % 5u), sh = 9 + (int)((h >> 16) % 9u);
            c.Ellipse(x + 4, y + 2, sw, 3, Black, 80);
            c.Triangle(x - sw / 2, y, x - sw / 2, y - sh, x + sw / 2, y - sh + (int)((h >> 20) % 5u), light);        // lit face
            c.Triangle(x - sw / 2, y, x + sw / 2, y - sh + (int)((h >> 20) % 5u), x + sw / 2, y, mid);
            c.Triangle(x + sw / 2, y, x + sw / 2, y - sh + (int)((h >> 20) % 5u), x + sw / 2 + 3, y - sh + 2, Sink(mid, 450));  // shaded side
            c.Line(x - sw / 2, y, x + sw / 2, y, Sink(dark, 600), 220);
        }

        int rebar = widthCells * heightCells + 2;
        for (int i = 0; i < rebar; i++)
        {
            uint h = H(seed, i, 4);
            int x = cx + (int)(h % (uint)rx) - rx / 2, y = cy + (int)((h >> 10) % (uint)ry) - ry / 2, len = 10 + (int)((h >> 20) % 9u);
            int lean = (int)((h >> 5) % 5u) - 2;
            c.Line(x, y, x + len, y - len / 2 - 2 + lean, new TerrainPalette.Rgb(58, 40, 30));
            c.Line(x, y - 1, x + len, y - len / 2 - 3 + lean, new TerrainPalette.Rgb(140, 84, 52), 220);
        }

        return c;
    }

    /// <summary>
    /// A burnt-out hull: charred top, front and side faces in the faction's metal, a bent barrel, a ring where the turret sat, a row of tread
    /// stubs and a dark ground shadow. Frame <see cref="WreckW"/> x <see cref="WreckH"/>, ground point at (<see cref="WreckOriginX"/>, <see cref="WreckOriginY"/>).
    /// A seed bit mirrors it.
    /// </summary>
    public static PropCanvas Wreck(int faction, uint seed)
    {
        var c = new PropCanvas(WreckW, WreckH);
        var (dark, mid, light) = Tones(faction);
        TerrainPalette.Rgb top = Sink(light, 640), front = Sink(mid, 700), side = Sink(dark, 300), char1 = new(18, 15, 14), rust = new(120, 70, 40);
        c.Ellipse(46, 48, 36, 12, Black, 100);
        c.Ellipse(44, 46, 30, 9, Ash, 90);
        int x0 = 18, x1 = 60, y0 = 34, y1 = 46, dx = 12, dy = 8;
        c.Triangle(x0, y0, x1, y0, x0, y1, front);                          // front face
        c.Triangle(x1, y0, x1, y1, x0, y1, front);
        c.Triangle(x1, y0, x1 + dx, y0 - dy, x1, y1, side);                 // right side
        c.Triangle(x1 + dx, y0 - dy, x1 + dx, y1 - dy, x1, y1, side);
        c.Triangle(x0 + dx, y0 - dy, x1 + dx, y0 - dy, x0, y0, top);        // top face
        c.Triangle(x1 + dx, y0 - dy, x1, y0, x0, y0, top);
        c.Line(x0, y0, x0 + dx, y0 - dy, Lift(top, 250), 220);              // lit edges
        c.Line(x0, y0, x1, y0, Lift(top, 180), 200);
        for (int i = 0; i < 9; i++) c.Ellipse(x0 + 3 + i * 5, y1 + 1, 2, 2, Sink(dark, 500), 255);   // tread stubs
        uint h = H(seed, 0, 5);
        int side1 = (h & 1) == 0 ? -1 : 1;
        c.Ellipse(40 + side1 * 4, 24, 10, 5, Sink(mid, 760), 255);               // the ring the turret left
        c.Ellipse(40 + side1 * 4, 23, 7, 3, char1, 210);
        c.Line(34 + side1 * 4, 26, 12 + side1 * 4, 31 + (int)((h >> 4) % 4u), Sink(light, 500));   // bent barrel
        c.Line(34 + side1 * 4, 25, 12 + side1 * 4, 30 + (int)((h >> 4) % 4u), Lift(Sink(light, 500), 200), 200);
        for (int i = 0; i < 5; i++)                                         // soot and rust patches on the top and front
        {
            uint g = H(seed, i, 6);
            int px = 22 + (int)(g % 40u), py = 24 + (int)((g >> 8) % 20u);
            if ((g & 0x10000) != 0) c.Ellipse(px, py, 2 + (int)((g >> 12) % 4u), 1 + (int)((g >> 14) % 2u), char1, 150);
            else c.Ellipse(px, py, 2, 1, rust, 170);
        }

        return (seed & 2) != 0 ? Mirror(c) : c;
    }

    /// <summary>A small dark smudge with a few scuffs (no gore): <see cref="MarkW"/> x <see cref="MarkH"/>, centred.</summary>
    public static PropCanvas Mark(uint seed)
    {
        var c = new PropCanvas(MarkW, MarkH);
        var soot = new TerrainPalette.Rgb(30, 26, 24);
        c.Ellipse(14, 8, 11, 5, soot, 70);
        c.Ellipse(14, 8, 8, 3, soot, 95);
        for (int i = 0; i < 7; i++)
        {
            uint h = H(seed, i, 7);
            int x = 5 + (int)(h % 18u), y = 5 + (int)((h >> 8) % 7u);
            if ((x + y) % 2 == 0) c.Over(x, y, soot, 160);
        }

        return c;
    }

    /// <summary>Scorch canvas size for a ground radius of <paramref name="radius"/> pixels, squashed to <paramref name="squashPermille"/> of its width (default: the camera elevation).</summary>
    public static (int W, int H) ScorchSize(int radius, int squashPermille = GroundSquashPermille) => (2 * radius + 4, 2 * (radius * squashPermille / 1000) + 4);

    /// <summary>
    /// A dark scorch decal: a solid core, a dithered rim and an uneven edge, squashed to a ground ellipse. Hard pixels only (alpha 0 or the
    /// layer's fixed steps): the same look as the dithered ground shadows.
    /// </summary>
    public static PropCanvas Scorch(int radius, int squashPermille, uint seed)
    {
        radius = Math.Max(4, radius);
        (int w, int h) = ScorchSize(radius, squashPermille);
        var c = new PropCanvas(w, h);
        int cx = w / 2, cy = h / 2, ry = Math.Max(1, radius * squashPermille / 1000);
        var soot = new TerrainPalette.Rgb(22, 18, 16);
        var edge = new int[16];
        for (int i = 0; i < edge.Length; i++) edge[i] = 800 + (int)(H(seed, i, 8) % 300u);   // 80..110 % of the radius, per direction
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int dx = x - cx, dy = (y - cy) * radius / Math.Max(1, ry);
                long d2 = (long)dx * dx + (long)dy * dy;
                int dir = (int)((Math.Atan2(dy, dx) + Math.PI) * 16 / (2 * Math.PI)) & 15;
                long limit = (long)radius * edge[dir] / 1000;
                if (d2 > limit * limit) continue;
                long perm = d2 * 1000 / Math.Max(1, limit * limit);   // 0 at the middle .. 1000 at the edge
                int alpha;
                if (perm < 280) alpha = 235;
                else if (perm < 560) alpha = 185;
                else if (perm < 780) alpha = ((x + y) & 1) == 0 ? 150 : 0;
                else alpha = ((x & 1) == 0 && (y & 1) == 0) ? 120 : 0;
                if (alpha > 0) c.Over(x, y, soot, alpha);
            }
        }

        return c;
    }

    private static PropCanvas Mirror(PropCanvas src)
    {
        var m = new PropCanvas(src.Width, src.Height);
        for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++)
                Buffer.BlockCopy(src.Rgba, (y * src.Width + x) * 4, m.Rgba, (y * src.Width + (src.Width - 1 - x)) * 4, 4);
        return m;
    }
}
