// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.IO.Compression;
using System.IO.Hashing;
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Tools;

/// <summary>Top-down PNG preview of a map for `map render --png` (M12-06): flat colours per land type, resource fields in bright yellow and
/// green, blocking props as coloured discs, neutral ruins as magenta boxes, starts as red circles. A tiny dependency-free PNG writer.</summary>
public static class MapRender
{
    /// <summary>Renders the map to RGB bytes (3 per pixel), <paramref name="scale"/> pixels per cell.</summary>
    public static byte[] Rgb(MapData map, MapMeta? meta, int scale)
    {
        int w = map.Width * scale, h = map.Height * scale;
        var px = new byte[w * h * 3];
        bool[]? ruins = MapAnalysis.FeatureMask(map, meta?.Features);
        for (int cy = 0; cy < map.Height; cy++)
            for (int cx = 0; cx < map.Width; cx++)
            {
                int i = cy * map.Width + cx;
                (int r, int g, int b) = Ground(map.Land[i]);
                int lift = map.HeightLevel[i] * 14;
                r = Math.Min(255, r + lift); g = Math.Min(255, g + lift); b = Math.Min(255, b + lift);
                OverlayKind o = map.Overlay[i];
                if (o == OverlayKind.Ashfall) (r, g, b) = (230, 200, 40);
                else if (o == OverlayKind.Pitchblende) (r, g, b) = (60, 220, 120);
                else if (o == OverlayKind.Wall) (r, g, b) = (150, 150, 150);
                if (ruins is not null && ruins[i]) (r, g, b) = (210, 40, 200);
                for (int py = 0; py < scale; py++)
                    for (int pxx = 0; pxx < scale; pxx++)
                    {
                        int p = ((cy * scale + py) * w + cx * scale + pxx) * 3;
                        px[p] = (byte)r; px[p + 1] = (byte)g; px[p + 2] = (byte)b;
                    }
                if (OverlayKinds.IsProp(o)) Disc(px, w, cx * scale + scale / 2, cy * scale + scale / 2, Math.Max(1, scale * 4 / 10), Prop(o));
            }
        foreach (CellCoord s in map.StartPositions)
            Disc(px, w, s.X * scale + scale / 2, s.Y * scale + scale / 2, Math.Max(3, scale * 2), (230, 30, 30));
        return px;
    }

    /// <summary>Writes a PNG (8-bit RGB, no interlace) of the map.</summary>
    public static void WritePng(MapData map, MapMeta? meta, int scale, string path)
    {
        int w = map.Width * scale, h = map.Height * scale;
        byte[] rgb = Rgb(map, meta, scale);
        var raw = new byte[(w * 3 + 1) * h];
        for (int y = 0; y < h; y++) Buffer.BlockCopy(rgb, y * w * 3, raw, y * (w * 3 + 1) + 1, w * 3);
        using var idat = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var f = File.Create(path);
        f.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        Be(ihdr, 0, w); Be(ihdr, 4, h); ihdr[8] = 8; ihdr[9] = 2;
        Chunk(f, "IHDR", ihdr);
        Chunk(f, "IDAT", idat.ToArray());
        Chunk(f, "IEND", Array.Empty<byte>());
    }

    private static void Be(byte[] b, int at, int v) { b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v; }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        Be(len, 0, data.Length);
        s.Write(len);
        var t = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(t);
        s.Write(data);
        var crc = new Crc32();
        crc.Append(t); crc.Append(data);
        var c = new byte[4];
        crc.GetCurrentHash(c);
        Array.Reverse(c);   // Crc32 returns little-endian; PNG wants big-endian
        s.Write(c);
    }

    private static void Disc(byte[] px, int w, int cx, int cy, int r, (int R, int G, int B) col)
    {
        int h = px.Length / 3 / w;
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || (x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                int p = (y * w + x) * 3;
                px[p] = (byte)col.R; px[p + 1] = (byte)col.G; px[p + 2] = (byte)col.B;
            }
    }

    private static (int, int, int) Ground(LandType l) => l switch
    {
        LandType.Clear => (150, 140, 118), LandType.Road => (88, 88, 94), LandType.Rough => (122, 104, 82), LandType.Ash => (104, 100, 98),
        LandType.Water => (28, 58, 110), LandType.Rock => (66, 62, 58), LandType.Cliff => (42, 38, 36), LandType.Shallow => (78, 120, 160),
        LandType.Coast => (205, 192, 150), _ => (255, 0, 255),
    };

    private static (int, int, int) Prop(OverlayKind o) => o switch
    {
        OverlayKind.Tree => (24, 60, 28), OverlayKind.Wreck => (170, 72, 40), OverlayKind.Block => (200, 200, 200),
        OverlayKind.Pylon => (220, 220, 70), OverlayKind.Snag => (112, 78, 48), _ => (255, 255, 255),
    };
}
