// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>
/// A tiny premultiplied-RGBA raster surface for the procedural placeholder props: filled triangles, lines and ellipses, integer maths only.
/// No GPU types, so the tests and the contact sheets use the same pixels the game draws.
/// </summary>
public sealed class PropCanvas
{
    /// <summary>Creates a transparent surface.</summary>
    public PropCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        Rgba = new byte[width * height * 4];
    }

    /// <summary>Width in pixels.</summary>
    public int Width { get; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; }

    /// <summary>Premultiplied RGBA bytes, row-major.</summary>
    public byte[] Rgba { get; }

    /// <summary>Composites one pixel (colour with opacity <paramref name="a"/> 0..255) over what is there.</summary>
    public void Over(int x, int y, TerrainPalette.Rgb c, int a)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height || a <= 0) return;
        int o = (y * Width + x) * 4, inv = 255 - a;
        Rgba[o] = (byte)(c.R * a / 255 + Rgba[o] * inv / 255);
        Rgba[o + 1] = (byte)(c.G * a / 255 + Rgba[o + 1] * inv / 255);
        Rgba[o + 2] = (byte)(c.B * a / 255 + Rgba[o + 2] * inv / 255);
        Rgba[o + 3] = (byte)(a + Rgba[o + 3] * inv / 255);
    }

    /// <summary>Fills a triangle (either winding).</summary>
    public void Triangle(int x0, int y0, int x1, int y1, int x2, int y2, TerrainPalette.Rgb c, int a = 255)
    {
        int minX = Math.Min(x0, Math.Min(x1, x2)), maxX = Math.Max(x0, Math.Max(x1, x2));
        int minY = Math.Min(y0, Math.Min(y1, y2)), maxY = Math.Max(y0, Math.Max(y1, y2));
        long area = (long)(x1 - x0) * (y2 - y0) - (long)(y1 - y0) * (x2 - x0);
        if (area == 0) return;
        int sign = area > 0 ? 1 : -1;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                long w0 = ((long)(x2 - x1) * (y - y1) - (long)(y2 - y1) * (x - x1)) * sign;
                long w1 = ((long)(x0 - x2) * (y - y2) - (long)(y0 - y2) * (x - x2)) * sign;
                long w2 = ((long)(x1 - x0) * (y - y0) - (long)(y1 - y0) * (x - x0)) * sign;
                if (w0 >= 0 && w1 >= 0 && w2 >= 0) Over(x, y, c, a);
            }
        }
    }

    /// <summary>Draws a one-pixel line (Bresenham).</summary>
    public void Line(int x0, int y0, int x1, int y1, TerrainPalette.Rgb c, int a = 255)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        while (true)
        {
            Over(x0, y0, c, a);
            if (x0 == x1 && y0 == y1) return;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>Fills an axis-aligned ellipse.</summary>
    public void Ellipse(int cx, int cy, int rx, int ry, TerrainPalette.Rgb c, int a)
    {
        if (rx < 1 || ry < 1) return;
        for (int y = -ry; y <= ry; y++)
            for (int x = -rx; x <= rx; x++)
                if ((long)x * x * ry * ry + (long)y * y * rx * rx <= (long)rx * rx * ry * ry) Over(cx + x, cy + y, c, a);
    }
}
