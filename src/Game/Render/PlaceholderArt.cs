// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;

namespace Rts.Game.Render;

/// <summary>
/// A placeholder sprite made in code, with the same shape as a sprite sheet: premultiplied BASE and MASK pixels (the
/// MASK is the house paint, greyscale) and a manifest that describes the frames. No GPU is needed to build one, so
/// the proof and the tests use the same pixels the game draws.
/// </summary>
/// <param name="Id">Rules id the placeholder stands in for.</param>
/// <param name="Manifest">Frame layout and states, parsed from generated manifest text.</param>
/// <param name="Columns">Frames per row. The sheet is always a single row, so this equals the frame count.</param>
/// <param name="Width">Sheet width in pixels.</param>
/// <param name="Height">Sheet height in pixels (one frame).</param>
/// <param name="Base">Premultiplied RGBA of everything that is not house paint.</param>
/// <param name="Mask">Premultiplied RGBA greyscale house paint.</param>
public sealed record PlaceholderPixels(string Id, SpriteManifest Manifest, int Columns, int Width, int Height,
    byte[] Base, byte[] Mask);

/// <summary>
/// Makes the placeholder art used when a rules id has no sprite file yet. A building is a shaded box of its footprint
/// with a house-colour stripe along the top and the id's short name in a small pixel font; it has idle, damaged (cracked)
/// and build states (four frames that rise from the bottom). A unit is a shaded disc in house colour with a white
/// facing arrow: vehicles have 16 facings, infantry 8 facings plus a two-frame walk.
/// </summary>
public static class PlaceholderArt
{
    /// <summary>Cell size in pixels at zoom 1 (matches <see cref="Camera.CellPx"/>).</summary>
    public const int CellPx = 64;

    /// <summary>Vehicle frame size at zoom 1. The origin is the centre.</summary>
    public const int VehicleFramePx = 48;

    /// <summary>Infantry frame size at zoom 1. The origin is the centre.</summary>
    public const int InfantryFramePx = 32;

    private const int BuildFrames = 4;
    private const int BuildTicksPerFrame = 10;
    private const int VehicleFacings = 16;
    private const int InfantryFacings = 8;
    private const int WalkFrames = 2;
    private const int WalkTicksPerFrame = 6;

    private static readonly (byte R, byte G, byte B) Outline = (42, 40, 32);
    private static readonly (byte R, byte G, byte B) Label = (30, 28, 22);
    private static readonly (byte R, byte G, byte B) Arrow = (242, 239, 216);
    private static readonly (byte R, byte G, byte B) BodyTop = (156, 154, 124);
    private static readonly (byte R, byte G, byte B) BodyBottom = (92, 90, 72);
    private static readonly (byte R, byte G, byte B) GridLine = (74, 72, 58);

    // 3 x 5 glyphs, one string per row, '1' = ink. A-Z and 0-9.
    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['A'] = new[] { "010", "101", "111", "101", "101" },
        ['B'] = new[] { "110", "101", "110", "101", "110" },
        ['C'] = new[] { "011", "100", "100", "100", "011" },
        ['D'] = new[] { "110", "101", "101", "101", "110" },
        ['E'] = new[] { "111", "100", "110", "100", "111" },
        ['F'] = new[] { "111", "100", "110", "100", "100" },
        ['G'] = new[] { "011", "100", "101", "101", "011" },
        ['H'] = new[] { "101", "101", "111", "101", "101" },
        ['I'] = new[] { "111", "010", "010", "010", "111" },
        ['J'] = new[] { "001", "001", "001", "101", "010" },
        ['K'] = new[] { "101", "101", "110", "101", "101" },
        ['L'] = new[] { "100", "100", "100", "100", "111" },
        ['M'] = new[] { "101", "111", "111", "101", "101" },
        ['N'] = new[] { "110", "101", "101", "101", "101" },
        ['O'] = new[] { "010", "101", "101", "101", "010" },
        ['P'] = new[] { "110", "101", "110", "100", "100" },
        ['Q'] = new[] { "010", "101", "101", "110", "011" },
        ['R'] = new[] { "110", "101", "110", "101", "101" },
        ['S'] = new[] { "011", "100", "010", "001", "110" },
        ['T'] = new[] { "111", "010", "010", "010", "010" },
        ['U'] = new[] { "101", "101", "101", "101", "111" },
        ['V'] = new[] { "101", "101", "101", "101", "010" },
        ['W'] = new[] { "101", "101", "111", "111", "101" },
        ['X'] = new[] { "101", "101", "010", "101", "101" },
        ['Y'] = new[] { "101", "101", "010", "010", "010" },
        ['Z'] = new[] { "111", "001", "010", "100", "111" },
        ['0'] = new[] { "111", "101", "101", "101", "111" },
        ['1'] = new[] { "010", "110", "010", "010", "111" },
        ['2'] = new[] { "110", "001", "010", "100", "111" },
        ['3'] = new[] { "110", "001", "010", "001", "110" },
        ['4'] = new[] { "101", "101", "111", "001", "001" },
        ['5'] = new[] { "111", "100", "110", "001", "110" },
        ['6'] = new[] { "011", "100", "110", "101", "010" },
        ['7'] = new[] { "111", "001", "010", "010", "010" },
        ['8'] = new[] { "111", "101", "111", "101", "111" },
        ['9'] = new[] { "010", "101", "011", "001", "110" },
    };

    /// <summary>
    /// The label drawn on a placeholder: the id's last segment (after the last underscore), upper-cased, letters and
    /// digits only, at most four characters. "auth_refinery" gives "REFI". An empty result gives "?".
    /// </summary>
    public static string ShortName(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        int cut = id.LastIndexOf('_');
        string tail = cut >= 0 ? id[(cut + 1)..] : id;
        var chars = new List<char>(4);
        foreach (char c in tail.ToUpperInvariant())
        {
            if (chars.Count == 4) break;
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9') chars.Add(c);
        }
        return chars.Count == 0 ? "?" : new string(chars.ToArray());
    }

    /// <summary>A building placeholder covering a footprint of <paramref name="footprintW"/> x <paramref name="footprintH"/> cells.</summary>
    /// <param name="id">Rules id, used for the label and the manifest id.</param>
    /// <param name="footprintW">Footprint width in cells, at least 1.</param>
    /// <param name="footprintH">Footprint height in cells, at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">A footprint dimension is below 1.</exception>
    public static PlaceholderPixels Building(string id, int footprintW, int footprintH)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (footprintW < 1) throw new ArgumentOutOfRangeException(nameof(footprintW));
        if (footprintH < 1) throw new ArgumentOutOfRangeException(nameof(footprintH));

        int w = footprintW * CellPx;
        int h = footprintH * CellPx;
        string label = ShortName(id);
        var frames = new List<Canvas>(3 + BuildFrames)
        {
            PaintBuilding(w, h, label, damaged: false, fillFromY: 0),
            PaintBuilding(w, h, label, damaged: true, fillFromY: 0),
        };
        for (int k = 0; k < BuildFrames; k++)
        {
            int filled = h * (k + 1) / BuildFrames;
            frames.Add(PaintBuilding(w, h, label, damaged: false, fillFromY: h - filled));
        }

        string text = $"""
            file = "placeholder"
            frame_w = {w}
            frame_h = {h}
            origin_x = 0
            origin_y = 0

            [[state]]
            name = "idle"
            facings = 1
            frames = 1
            first = 0
            ticks_per_frame = 1

            [[state]]
            name = "damaged"
            facings = 1
            frames = 1
            first = 1
            ticks_per_frame = 1

            [[state]]
            name = "build"
            facings = 1
            frames = {BuildFrames}
            first = 2
            ticks_per_frame = {BuildTicksPerFrame}
            """;
        return Assemble(id, text, w, h, frames);
    }

    /// <summary>
    /// A unit placeholder. Vehicles get 16 facings in a single idle state. Infantry get 8 facings (idle) and an 8 x 2
    /// walk state, laid out facing-major after the idle block.
    /// </summary>
    /// <param name="id">Rules id, used for the manifest id.</param>
    /// <param name="infantry">True for infantry (8 facings with walk), false for vehicles (16 facings).</param>
    public static PlaceholderPixels Unit(string id, bool infantry)
    {
        ArgumentNullException.ThrowIfNull(id);
        int px = infantry ? InfantryFramePx : VehicleFramePx;
        int radius = infantry ? 11 : 20;
        int arrowBlock = infantry ? 1 : 2;
        int facings = infantry ? InfantryFacings : VehicleFacings;
        // M3-08: the id's short name is printed on the disc, so any Directorate or new Authority type without art is recognisable.
        string label = ShortName(id);

        var frames = new List<Canvas>();
        for (int f = 0; f < facings; f++)
        {
            frames.Add(PaintUnit(px, radius, arrowBlock, f, facings, bob: 0, label));
        }
        string text;
        if (infantry)
        {
            for (int f = 0; f < facings; f++)
            {
                for (int k = 0; k < WalkFrames; k++)
                {
                    frames.Add(PaintUnit(px, radius, arrowBlock, f, facings, bob: k == 0 ? 0 : 1, label));
                }
            }
            text = $"""
                file = "placeholder"
                frame_w = {px}
                frame_h = {px}
                origin_x = {px / 2}
                origin_y = {px / 2}

                [[state]]
                name = "idle"
                facings = {InfantryFacings}
                frames = 1
                first = 0
                ticks_per_frame = 1

                [[state]]
                name = "walk"
                facings = {InfantryFacings}
                frames = {WalkFrames}
                first = {InfantryFacings}
                ticks_per_frame = {WalkTicksPerFrame}
                """;
        }
        else
        {
            text = $"""
                file = "placeholder"
                frame_w = {px}
                frame_h = {px}
                origin_x = {px / 2}
                origin_y = {px / 2}

                [[state]]
                name = "idle"
                facings = {VehicleFacings}
                frames = 1
                first = 0
                ticks_per_frame = 1
                """;
        }
        return Assemble(id, text, px, px, frames);
    }

    private static PlaceholderPixels Assemble(string id, string manifestText, int frameW, int frameH, List<Canvas> frames)
    {
        SpriteManifest manifest = SpriteManifest.Parse(manifestText, id, $"placeholder:{id}");
        int width = frameW * frames.Count;
        var sheet = new Canvas(width, frameH);
        for (int i = 0; i < frames.Count; i++)
        {
            sheet.CopyFrom(frames[i], i * frameW);
        }
        sheet.Premultiply();
        return new PlaceholderPixels(id, manifest, frames.Count, width, frameH, sheet.Base, sheet.Mask);
    }

    private static Canvas PaintBuilding(int w, int h, string label, bool damaged, int fillFromY)
    {
        var c = new Canvas(w, h);
        for (int y = fillFromY; y < h; y++)
        {
            float t = h <= 1 ? 0f : y / (float)(h - 1);
            var shade = Lerp(BodyTop, BodyBottom, t);
            if (damaged) shade = Scale(shade, 0.7f);
            for (int x = 0; x < w; x++) c.Set(x, y, shade);
        }

        if (fillFromY == 0)
        {
            // Cell grid so that a multi-cell footprint reads as cells.
            for (int x = CellPx; x < w; x += CellPx)
            {
                for (int y = 0; y < h; y++) c.Set(x, y, GridLine);
            }
            for (int y = CellPx; y < h; y += CellPx)
            {
                for (int x = 0; x < w; x++) c.Set(x, y, GridLine);
            }

            // Light top edge inside the border.
            for (int x = 2; x < w - 2; x++)
            {
                c.Set(x, 2, (196, 194, 162));
                c.Set(x, 3, (196, 194, 162));
            }
            // House-colour stripe, lightest shade = the full house colour.
            for (int y = 6; y < 14 && y < h; y++)
            {
                for (int x = 6; x < w - 6; x++) c.Grey(x, y, 255);
            }
        }

        // Border, drawn last so it wins at the edges of a partly built box.
        for (int y = fillFromY; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (x < 2 || y < 2 || x >= w - 2 || y >= h - 2) c.Set(x, y, Outline);
            }
        }

        if (damaged)
        {
            for (int i = 0; i < h; i++)
            {
                int x = (int)(w * 0.3f + i * 0.25f);
                c.Set(x, i, Label);
                c.Set(x + 1, i, Label);
            }
        }

        if (fillFromY == 0)
        {
            DrawText(c, label, w / 2, h / 2 - 5, 2, Label);
        }
        return c;
    }

    private static Canvas PaintUnit(int px, int radius, int arrowBlock, int facingIndex, int facings, int bob, string label)
    {
        var c = new Canvas(px, px);
        int centre = px / 2;
        int cy = centre + bob;
        for (int y = 0; y < px; y++)
        {
            for (int x = 0; x < px; x++)
            {
                float dx = x + 0.5f - centre;
                float dy = y + 0.5f - cy;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > radius) continue;
                if (d > radius - 2)
                {
                    c.Set(x, y, Outline);
                    continue;
                }
                // Lit from the upper left: lighter toward the top-left of the disc.
                float light = Math.Clamp(0.5f + (-dx - dy) / (4f * radius), 0f, 1f);
                c.Grey(x, y, (byte)(150 + 105 * light));
            }
        }

        double theta = facingIndex * MathF.Tau / facings;
        float ux = (float)Math.Sin(theta);
        float uy = (float)-Math.Cos(theta);
        for (float t = 2f; t <= radius - 3; t += 0.5f)
        {
            int ax = (int)MathF.Round(centre + ux * t);
            int ay = (int)MathF.Round(cy + uy * t);
            for (int oy = 0; oy < arrowBlock; oy++)
            {
                for (int ox = 0; ox < arrowBlock; ox++) c.Set(ax + ox, ay + oy, Arrow);
            }
        }

        // Label last so it stays readable over the arrow: scale 1 on infantry (up to 3 letters), scale 2 on vehicles (4 letters).
        bool small = radius < 15;
        string text = small && label.Length > 3 ? label[..3] : label;
        int scale = small ? 1 : 2;
        DrawText(c, text, centre, cy - (5 * scale) / 2, scale, Label);
        return c;
    }

    private static void DrawText(Canvas c, string text, int centreX, int top, int scale, (byte R, byte G, byte B) colour)
    {
        int advance = 3 * scale + scale;
        int width = text.Length * advance - scale;
        int x0 = centreX - width / 2;
        for (int i = 0; i < text.Length; i++)
        {
            if (!Glyphs.TryGetValue(text[i], out string[]? rows)) continue;
            int gx = x0 + i * advance;
            for (int r = 0; r < rows.Length; r++)
            {
                for (int col = 0; col < 3; col++)
                {
                    if (rows[r][col] != '1') continue;
                    for (int oy = 0; oy < scale; oy++)
                    {
                        for (int ox = 0; ox < scale; ox++)
                        {
                            c.Set(gx + col * scale + ox, top + r * scale + oy, colour);
                        }
                    }
                }
            }
        }
    }

    private static (byte R, byte G, byte B) Lerp((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, float t) =>
        ((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private static (byte R, byte G, byte B) Scale((byte R, byte G, byte B) a, float f) =>
        ((byte)(a.R * f), (byte)(a.G * f), (byte)(a.B * f));

    // Unpremultiplied RGBA canvas used while painting. Every pixel is either fully opaque or fully clear.
    private sealed class Canvas
    {
        public Canvas(int w, int h)
        {
            W = w;
            H = h;
            Base = new byte[w * h * 4];
            Mask = new byte[w * h * 4];
        }

        public int W { get; }

        public int H { get; }

        public byte[] Base { get; }

        public byte[] Mask { get; }

        /// <summary>Paints a non-house pixel and clears the mask there.</summary>
        public void Set(int x, int y, (byte R, byte G, byte B) colour)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
            int i = (y * W + x) * 4;
            Base[i] = colour.R;
            Base[i + 1] = colour.G;
            Base[i + 2] = colour.B;
            Base[i + 3] = 255;
            Mask[i] = Mask[i + 1] = Mask[i + 2] = Mask[i + 3] = 0;
        }

        /// <summary>Paints a house-paint pixel of the given greyscale and clears the base there.</summary>
        public void Grey(int x, int y, byte grey)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
            int i = (y * W + x) * 4;
            Mask[i] = Mask[i + 1] = Mask[i + 2] = grey;
            Mask[i + 3] = 255;
            Base[i] = Base[i + 1] = Base[i + 2] = Base[i + 3] = 0;
        }

        /// <summary>Copies another canvas into this one, starting at column <paramref name="dx"/>.</summary>
        public void CopyFrom(Canvas src, int dx)
        {
            for (int y = 0; y < src.H; y++)
            {
                int from = y * src.W * 4;
                int to = (y * W + dx) * 4;
                Array.Copy(src.Base, from, Base, to, src.W * 4);
                Array.Copy(src.Mask, from, Mask, to, src.W * 4);
            }
        }

        /// <summary>Premultiplies both layers, the form the GPU blend and <see cref="HouseSplit.Compose"/> expect.</summary>
        public void Premultiply()
        {
            HouseSplit.Premultiply(Base);
            HouseSplit.Premultiply(Mask);
        }
    }
}
