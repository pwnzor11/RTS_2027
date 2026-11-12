// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui;

/// <summary>A small RGBA picture of a map (M10-18), row by row, 4 bytes a pixel (R, G, B, A). Graphics-free, so the LAN lobby and tests can use it.</summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Rgba">Width * Height * 4 bytes.</param>
public sealed record MapPreviewImage(int Width, int Height, byte[] Rgba)
{
    /// <summary>The pixel at (x, y) as 0xRRGGBBAA.</summary>
    public uint Pixel(int x, int y)
    {
        int i = (y * Width + x) * 4;
        return ((uint)Rgba[i] << 24) | ((uint)Rgba[i + 1] << 16) | ((uint)Rgba[i + 2] << 8) | Rgba[i + 3];
    }
}
