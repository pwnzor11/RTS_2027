// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Game.Ui;

/// <summary>
/// The map preview thumbnail (M10-18): a pure function from a map to a small RGBA picture. Terrain and resource fields use the minimap's colours
/// (<see cref="Minimap.TerrainColour"/>); each start position is a dark square holding its number (1-based, in the map's start order).
/// A map smaller than the box is enlarged by a whole factor; a larger one is sampled down. No graphics device is needed.
/// </summary>
public static class MapPreview
{
    /// <summary>Largest side of the picture the setup screen asks for.</summary>
    public const int DefaultSide = 96;

    // 3x5 digit shapes, one row per entry, bit 2 = left column.
    private static readonly byte[][] Digits =
    {
        new byte[] { 7, 5, 5, 5, 7 }, new byte[] { 2, 6, 2, 2, 7 }, new byte[] { 7, 1, 7, 4, 7 }, new byte[] { 7, 1, 7, 1, 7 }, new byte[] { 5, 5, 7, 1, 1 },
        new byte[] { 7, 4, 7, 1, 7 }, new byte[] { 7, 4, 7, 5, 7 }, new byte[] { 7, 1, 1, 1, 1 }, new byte[] { 7, 5, 7, 5, 7 }, new byte[] { 7, 5, 7, 1, 7 },
    };

    /// <summary>The picture of a map, at most <paramref name="maxSide"/> pixels on its longer side.</summary>
    /// <param name="map">The map.</param>
    /// <param name="maxSide">Longer side limit in pixels (at least 8).</param>
    /// <param name="palette">The theater palette, or null to ask <see cref="TerrainPalette.ForMap"/> (the built-in colours when that fails).</param>
    public static MapPreviewImage Render(MapData map, int maxSide = DefaultSide, TerrainPalette? palette = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        maxSide = Math.Max(8, maxSide);
        palette ??= TryPalette(map);
        int longer = Math.Max(map.Width, map.Height);
        int scale = longer <= maxSide ? Math.Max(1, maxSide / longer) : 1;
        int w = longer <= maxSide ? map.Width * scale : Math.Max(1, (int)((long)map.Width * maxSide / longer));
        int h = longer <= maxSide ? map.Height * scale : Math.Max(1, (int)((long)map.Height * maxSide / longer));
        var rgba = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            int cy = Math.Min(map.Height - 1, (int)((long)y * map.Height / h));
            for (int x = 0; x < w; x++)
            {
                int cx = Math.Min(map.Width - 1, (int)((long)x * map.Width / w));
                int c = cy * map.Width + cx;
                var colour = Minimap.TerrainColour(palette, map.Land[c], map.Overlay[c], map.Density[c]);
                Put(rgba, w, x, y, colour.R, colour.G, colour.B);
            }
        }

        for (int i = 0; i < map.StartPositions.Count; i++)
        {
            CellCoord s = map.StartPositions[i];
            int px = (int)((long)(2 * s.X + 1) * w / (2 * map.Width));
            int py = (int)((long)(2 * s.Y + 1) * h / (2 * map.Height));
            Marker(rgba, w, h, px, py, i + 1);
        }

        return new MapPreviewImage(w, h, rgba);
    }

    private static TerrainPalette? TryPalette(MapData map)
    {
        try
        {
            return TerrainPalette.ForMap(map);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or TypeInitializationException)
        {
            return null;
        }
    }

    private static void Put(byte[] rgba, int w, int x, int y, byte r, byte g, byte b)
    {
        int i = (y * w + x) * 4;
        rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = 255;
    }

    // A dark box with a light rim around the number, centred on (cx, cy) and kept inside the picture.
    private static void Marker(byte[] rgba, int w, int h, int cx, int cy, int number)
    {
        string text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int textW = text.Length * 4 - 1;
        int boxW = textW + 4, boxH = 5 + 4;
        int x0 = Math.Clamp(cx - boxW / 2, 0, Math.Max(0, w - boxW));
        int y0 = Math.Clamp(cy - boxH / 2, 0, Math.Max(0, h - boxH));
        for (int y = 0; y < boxH; y++)
        {
            for (int x = 0; x < boxW; x++)
            {
                if (x0 + x >= w || y0 + y >= h) continue;
                bool rim = x == 0 || y == 0 || x == boxW - 1 || y == boxH - 1;
                if (rim) Put(rgba, w, x0 + x, y0 + y, 0xE8, 0xD8, 0x90);
                else Put(rgba, w, x0 + x, y0 + y, 0x10, 0x10, 0x0C);
            }
        }

        for (int d = 0; d < text.Length; d++)
        {
            byte[] shape = Digits[text[d] - '0'];
            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    if ((shape[row] & (4 >> col)) == 0) continue;
                    int x = x0 + 2 + d * 4 + col, y = y0 + 2 + row;
                    if (x < w && y < h) Put(rgba, w, x, y, 0xFF, 0xFF, 0xFF);
                }
            }
        }
    }
}
