// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>
/// Procedural placeholder art for dust and smoke puffs (D-184), drawn in code like <see cref="PropArt"/>: four 32 px frames in a row, a lumpy
/// blob that grows and thins out (the last two frames are dithered, hard pixels, no soft alpha). The pixels are grey, lit from the upper left
/// like every sprite; the colour of a puff multiplies them, so one atlas serves every biome's dust and the dark smoke. Real art is M10.
/// </summary>
public static class PuffArt
{
    /// <summary>Frames in the atlas.</summary>
    public const int Frames = 4;

    /// <summary>Frame size in pixels.</summary>
    public const int FramePx = 32;

    /// <summary>Draws the atlas: <see cref="Frames"/> x <see cref="FramePx"/> wide, <see cref="FramePx"/> high, premultiplied RGBA.</summary>
    public static byte[] Render()
    {
        var canvas = new PropCanvas(Frames * FramePx, FramePx);
        for (int f = 0; f < Frames; f++) Frame(canvas, f);
        return canvas.Rgba;
    }

    private static void Frame(PropCanvas c, int f)
    {
        int ox = f * FramePx, radius = 6 + f * 3;
        // four lobes around the centre, the same lobes in every frame so the puff grows instead of changing shape
        var lx = new int[4];
        var ly = new int[4];
        var lr = new int[4];
        for (int i = 0; i < 4; i++)
        {
            uint h = DecorHash.Mix(0x5055_4646UL, i, 0, 9);
            lx[i] = (int)(h % 7u) - 3;
            ly[i] = (int)((h >> 8) % 5u) - 2;
            lr[i] = radius * (6 + (int)((h >> 16) % 4u)) / 10;
        }

        int cx = FramePx / 2, cy = FramePx / 2;
        for (int y = 0; y < FramePx; y++)
        {
            for (int x = 0; x < FramePx; x++)
            {
                int px = x - cx, py = y - cy;
                bool inside = false;
                for (int i = 0; i < 4 && !inside; i++)
                {
                    int dx = px - lx[i] * (f + 1) / 2, dy = py - ly[i] * (f + 1) / 2;
                    inside = dx * dx + dy * dy <= lr[i] * lr[i];
                }

                if (!inside) continue;
                if (f == 2 && ((x & 1) == 0 && (y & 1) == 0)) continue;   // 25 % holes
                if (f == 3 && ((x + y) & 1) == 0) continue;               // 50 % holes
                int shade = (px + py) < 0 ? 255 : (px + py) < radius / 2 ? 228 : 198;   // lit from the upper left
                c.Over(ox + x, y, new TerrainPalette.Rgb((byte)shade, (byte)shade, (byte)shade), 255);
            }
        }
    }
}
