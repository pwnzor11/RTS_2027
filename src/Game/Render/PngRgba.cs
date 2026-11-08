// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.IO.Compression;
using System.Text;

namespace Rts.Game.Render;

/// <summary>A small CPU-side PNG reader (8-bit RGBA, not interlaced) so art atlases can be read, tinted and tested without a graphics device.</summary>
public static class PngRgba
{
    /// <summary>Decodes a PNG file to straight (not premultiplied) RGBA bytes.</summary>
    /// <param name="path">File path.</param>
    /// <exception cref="InvalidDataException">The file is not an 8-bit RGBA non-interlaced PNG.</exception>
    public static (byte[] Rgba, int Width, int Height) Load(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        if (file.Length < 33 || file[1] != (byte)'P') throw new InvalidDataException($"{path}: not a PNG");
        int pos = 8, w = 0, h = 0;
        var idat = new MemoryStream();
        while (pos + 12 <= file.Length)
        {
            int len = Be(file, pos);
            string type = Encoding.ASCII.GetString(file, pos + 4, 4);
            int data = pos + 8;
            if (type == "IHDR")
            {
                w = Be(file, data);
                h = Be(file, data + 4);
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
        byte[] rb = raw.ToArray();
        int stride = w * 4;
        var px = new byte[h * stride];
        for (int y = 0; y < h; y++)
        {
            int filter = rb[y * (stride + 1)], src = y * (stride + 1) + 1, row = y * stride, prev = row - stride;
            for (int x = 0; x < stride; x++)
            {
                int a = x >= 4 ? px[row + x - 4] : 0, b = y > 0 ? px[prev + x] : 0, c = x >= 4 && y > 0 ? px[prev + x - 4] : 0, r = rb[src + x];
                px[row + x] = (byte)(filter switch
                {
                    0 => r,
                    1 => r + a,
                    2 => r + b,
                    3 => r + (a + b) / 2,
                    4 => r + Paeth(a, b, c),
                    _ => throw new InvalidDataException($"{path}: bad PNG filter {filter}"),
                });
            }
        }

        return (px, w, h);
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static int Be(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
}
