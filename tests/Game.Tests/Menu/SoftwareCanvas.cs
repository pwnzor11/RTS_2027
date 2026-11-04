// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.IO.Compression;
using System.Text;
using Microsoft.Xna.Framework;
using Rts.Game.Menu;
using Rts.Game.Sprites;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Tests.Menu;

/// <summary>
/// A CPU renderer for <see cref="DrawOp"/> lists, used to make proof screenshots without a GPU. It follows the same rules as
/// <see cref="DrawOpRenderer"/> (same colour maths, same glow, vignette and grain curves, linear sprite sampling), so a picture made
/// here shows what the window shows, apart from tiny filtering differences.
/// </summary>
internal sealed class SoftwareCanvas
{
    private readonly int _w;
    private readonly int _h;
    private readonly float[] _px;   // r, g, b in 0..255 (the canvas is opaque)
    private readonly SpriteLibrary? _sprites;
    private readonly Dictionary<(string, uint), Sheet?> _sheets = new();
    private readonly Dictionary<string, (byte[] Rgba, int W, int H)> _fontPng = new();

    public SoftwareCanvas(int width, int height, SpriteLibrary? sprites)
    {
        _w = width;
        _h = height;
        _px = new float[width * height * 3];
        _sprites = sprites;
    }

    public void Draw(IReadOnlyList<DrawOp> ops)
    {
        foreach (DrawOp op in ops)
        {
            switch (op.Kind)
            {
                case DrawOpKind.Rect: Rect(op); break;
                case DrawOpKind.Glow: Glow(op); break;
                case DrawOpKind.Sprite: Sprite(op); break;
                case DrawOpKind.Vignette: Vignette(op.Amount); break;
                case DrawOpKind.Grain: Grain(op); break;
                case DrawOpKind.Text: Text(op); break;
            }
        }
    }

    public byte[] ToPng()
    {
        var rgba = new byte[_w * _h * 4];
        for (int i = 0; i < _w * _h; i++)
        {
            rgba[i * 4] = (byte)Math.Clamp(_px[i * 3] + 0.5f, 0, 255);
            rgba[i * 4 + 1] = (byte)Math.Clamp(_px[i * 3 + 1] + 0.5f, 0, 255);
            rgba[i * 4 + 2] = (byte)Math.Clamp(_px[i * 3 + 2] + 0.5f, 0, 255);
            rgba[i * 4 + 3] = 255;
        }
        return Png.Encode(rgba, _w, _h);
    }

    /// <summary>Mean colour of a rectangle, for assertions.</summary>
    public (float R, float G, float B) Mean(int x, int y, int w, int h)
    {
        double r = 0, g = 0, b = 0;
        int n = 0;
        for (int yy = Math.Max(0, y); yy < Math.Min(_h, y + h); yy++)
        for (int xx = Math.Max(0, x); xx < Math.Min(_w, x + w); xx++)
        {
            int i = (yy * _w + xx) * 3;
            r += _px[i]; g += _px[i + 1]; b += _px[i + 2];
            n++;
        }
        return n == 0 ? (0, 0, 0) : ((float)(r / n), (float)(g / n), (float)(b / n));
    }

    private void Blend(int x, int y, float r, float g, float b, float a)
    {
        // r, g, b are premultiplied (0..255 scale); a is 0..1.
        int i = (y * _w + x) * 3;
        float k = 1f - a;
        _px[i] = r + _px[i] * k;
        _px[i + 1] = g + _px[i + 1] * k;
        _px[i + 2] = b + _px[i + 2] * k;
    }

    private void Rect(DrawOp op)
    {
        int x0 = (int)MathF.Round(op.X), y0 = (int)MathF.Round(op.Y);
        int x1 = (int)MathF.Round(op.X + op.W), y1 = (int)MathF.Round(op.Y + op.H);
        int h = y1 - y0;
        if (x1 <= x0 || h <= 0) return;
        for (int y = Math.Max(0, y0); y < Math.Min(_h, y1); y++)
        {
            Rgba c = op.Colour == op.Colour2 ? op.Colour : Rgba.Lerp(op.Colour, op.Colour2, (y - y0 + 0.5f) / h);
            float a = c.A / 255f;
            if (a <= 0f) continue;
            for (int x = Math.Max(0, x0); x < Math.Min(_w, x1); x++) Blend(x, y, c.R * a, c.G * a, c.B * a, a);
        }
    }

    private void Glow(DrawOp op)
    {
        if (op.W < 0.5f || op.H < 0.5f) return;
        float a0 = op.Colour.A / 255f;
        for (int y = Math.Max(0, (int)(op.Y - op.H)); y < Math.Min(_h, (int)(op.Y + op.H) + 1); y++)
        for (int x = Math.Max(0, (int)(op.X - op.W)); x < Math.Min(_w, (int)(op.X + op.W) + 1); x++)
        {
            float dx = (x + 0.5f - op.X) / op.W, dy = (y + 0.5f - op.Y) / op.H;
            float f = LightMath.GlowFalloff(MathF.Sqrt(dx * dx + dy * dy));
            if (f <= 0f) continue;
            float a = f * a0;
            float r = op.Colour.R * a, g = op.Colour.G * a, b = op.Colour.B * a;
            if (op.Additive)
            {
                int i = (y * _w + x) * 3;
                _px[i] = MathF.Min(255f, _px[i] + r);
                _px[i + 1] = MathF.Min(255f, _px[i + 1] + g);
                _px[i + 2] = MathF.Min(255f, _px[i + 2] + b);
            }
            else Blend(x, y, r, g, b, a);
        }
    }

    private void Vignette(float strength)
    {
        for (int y = 0; y < _h; y++)
        for (int x = 0; x < _w; x++)
        {
            float d = LightMath.VignetteDarkness((x + 0.5f) / _w * 2f - 1f, (y + 0.5f) / _h * 2f - 1f) * strength;
            if (d > 0f) Blend(x, y, 0, 0, 0, d);
        }
    }

    private void Grain(DrawOp op)
    {
        (int ox, int oy) = LightMath.GrainOffset(op.Frame);
        for (int y = 0; y < _h; y++)
        for (int x = 0; x < _w; x++)
        {
            float v = LightMath.GrainValue((x + ox * 2) / 2, (y + oy * 2) / 2);
            float a = MathF.Abs(v) * op.Amount;
            Blend(x, y, v > 0f ? 255f * a : 0f, v > 0f ? 255f * a : 0f, v > 0f ? 255f * a : 0f, a);
        }
    }

    // ---- sprites ----

    private sealed record Sheet(byte[] Premul, int W, int H, SpriteManifest Manifest);

    private Sheet? SheetFor(string id, Rgba house)
    {
        uint key = ((uint)house.R << 16) | ((uint)house.G << 8) | house.B;
        if (_sheets.TryGetValue((id, key), out Sheet? cached)) return cached;
        Sheet? sheet = null;
        if (_sprites is not null && _sprites.Ids.Contains(id))
        {
            SpriteManifest m = _sprites.Manifest(id);
            (byte[] src, int sw, int sh) = Png.Decode(Path.Combine(Path.GetDirectoryName(m.SourcePath)!, m.SheetFile));
            var baseP = new byte[src.Length];
            var mask = new byte[src.Length];
            HouseSplit.Split(src, HouseRamp.For(m), baseP, mask);
            HouseSplit.Premultiply(mask);
            HouseSplit.Premultiply(baseP);
            var composed = new byte[src.Length];
            HouseSplit.Compose(baseP, mask, new Color(house.R, house.G, house.B, house.A), composed);
            sheet = new Sheet(composed, sw, sh, m);
        }
        _sheets[(id, key)] = sheet;
        return sheet;
    }

    private void Sprite(DrawOp op)
    {
        Sheet? sheet = SheetFor(op.Id ?? "", op.Colour);
        if (sheet is null) return;
        SpriteManifest m = sheet.Manifest;
        int cols = sheet.W / m.FrameW;
        int fx = op.Frame % cols * m.FrameW, fy = op.Frame / cols * m.FrameH;
        float s = op.Scale;
        float left = op.X - m.OriginX * s, top = op.Y - m.OriginY * s;
        int x0 = Math.Max(0, (int)MathF.Floor(left)), x1 = Math.Min(_w, (int)MathF.Ceiling(left + m.FrameW * s));
        int y0 = Math.Max(0, (int)MathF.Floor(top)), y1 = Math.Min(_h, (int)MathF.Ceiling(top + m.FrameH * s));
        for (int y = y0; y < y1; y++)
        for (int x = x0; x < x1; x++)
        {
            float u = (x + 0.5f - left) / s - 0.5f, v = (y + 0.5f - top) / s - 0.5f;
            int u0 = (int)MathF.Floor(u), v0 = (int)MathF.Floor(v);
            float tu = u - u0, tv = v - v0;
            float r = 0, g = 0, b = 0, a = 0;
            for (int j = 0; j < 2; j++)
            for (int i = 0; i < 2; i++)
            {
                int su = Math.Clamp(u0 + i, 0, m.FrameW - 1), sv = Math.Clamp(v0 + j, 0, m.FrameH - 1);
                float wgt = (i == 0 ? 1 - tu : tu) * (j == 0 ? 1 - tv : tv);
                int idx = ((fy + sv) * sheet.W + fx + su) * 4;
                r += sheet.Premul[idx] * wgt; g += sheet.Premul[idx + 1] * wgt; b += sheet.Premul[idx + 2] * wgt; a += sheet.Premul[idx + 3] * wgt;
            }
            if (a <= 0.5f) continue;
            float k = op.Alpha * op.Amount;
            Blend(x, y, r * k, g * k, b * k, a / 255f * op.Alpha);
        }
    }

    // ---- text ----

    private void Text(DrawOp op)
    {
        FontWeight weight = op.Bold ? FontWeight.Bold : FontWeight.Regular;
        FontAtlas? atlas = FontLibrary.Get(weight, Rts.Game.Ui.BitmapFont.SizeForScale(op.Frame));
        if (atlas is null || op.Id is null) return;
        string path = Path.Combine(FontLibrary.Directory!, atlas.TextureFile);
        if (!_fontPng.TryGetValue(path, out var tex)) _fontPng[path] = tex = Png.Decode(path);
        int x = (int)MathF.Round(op.X), y = (int)MathF.Round(op.Y), previous = -1;
        float ca = op.Colour.A / 255f;
        foreach (char ch in op.Id)
        {
            int cp = atlas.ResolveCodepoint(ch);
            if (previous >= 0) x += atlas.KerningBetween(previous, cp);
            FontGlyph gl = atlas.GetGlyph(cp);
            for (int gy = 0; gy < gl.H; gy++)
            for (int gx = 0; gx < gl.W; gx++)
            {
                int px = x + gl.XOffset + gx, py = y + gl.YOffset + gy;
                if ((uint)px >= (uint)_w || (uint)py >= (uint)_h) continue;
                float a = tex.Rgba[((gl.Y + gy) * tex.W + gl.X + gx) * 4 + 3] / 255f * ca;
                if (a > 0f) Blend(px, py, op.Colour.R * a, op.Colour.G * a, op.Colour.B * a, a);
            }
            x += gl.Advance;
            previous = cp;
        }
    }
}

/// <summary>Minimal PNG reading (8-bit RGBA) and writing for the proof renders.</summary>
internal static class Png
{
    public static (byte[] Rgba, int W, int H) Decode(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        int pos = 8, w = 0, h = 0;
        var idat = new MemoryStream();
        while (pos + 12 <= file.Length)
        {
            int len = BigEndian(file, pos);
            string type = Encoding.ASCII.GetString(file, pos + 4, 4);
            int data = pos + 8;
            if (type == "IHDR")
            {
                w = BigEndian(file, data);
                h = BigEndian(file, data + 4);
                if (file[data + 8] != 8 || file[data + 9] != 6 || file[data + 12] != 0)
                    throw new InvalidDataException($"{path}: only 8-bit RGBA, non-interlaced PNG is supported");
            }
            else if (type == "IDAT") idat.Write(file, data, len);
            else if (type == "IEND") break;
            pos = data + len + 4;
        }
        idat.Position = 0;
        var raw = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.CopyTo(raw);
        byte[] rawBytes = raw.ToArray();
        int stride = w * 4;
        var o = new byte[h * stride];
        for (int y = 0; y < h; y++)
        {
            int filter = rawBytes[y * (stride + 1)], src = y * (stride + 1) + 1, row = y * stride, prev = row - stride;
            for (int x = 0; x < stride; x++)
            {
                int a = x >= 4 ? o[row + x - 4] : 0, b = y > 0 ? o[prev + x] : 0, c = x >= 4 && y > 0 ? o[prev + x - 4] : 0, r = rawBytes[src + x];
                o[row + x] = (byte)(filter switch { 0 => r, 1 => r + a, 2 => r + b, 3 => r + (a + b) / 2, 4 => r + Paeth(a, b, c), _ => throw new InvalidDataException("bad PNG filter") });
            }
        }
        return (o, w, h);
    }

    public static byte[] Encode(byte[] rgba, int w, int h)
    {
        var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        Put(ihdr, 0, w);
        Put(ihdr, 4, h);
        ihdr[8] = 8;
        ihdr[9] = 6;
        Chunk(png, "IHDR", ihdr);
        int stride = w * 4;
        var raw = new byte[h * (stride + 1)];
        for (int y = 0; y < h; y++) Array.Copy(rgba, y * stride, raw, y * (stride + 1) + 1, stride);
        var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) zs.Write(raw);
        Chunk(png, "IDAT", z.ToArray());
        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        Put(len, 0, data.Length);
        s.Write(len);
        byte[] t = Encoding.ASCII.GetBytes(type);
        s.Write(t);
        s.Write(data);
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in t.Concat(data))
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        var c = new byte[4];
        Put(c, 0, (int)~crc);
        s.Write(c);
    }

    private static void Put(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    private static int BigEndian(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
