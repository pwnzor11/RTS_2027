// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Menu;

/// <summary>Conversions between <see cref="Rgba"/> and MonoGame colours.</summary>
public static class ColourConvert
{
    /// <summary>An opaque MonoGame colour as <see cref="Rgba"/>.</summary>
    public static Rgba ToRgba(this Color c) => new(c.R, c.G, c.B, c.A);

    /// <summary>An <see cref="Rgba"/> as a plain (not premultiplied) MonoGame colour, for tinting sprite masks.</summary>
    public static Color ToXna(this Rgba c) => new(c.R, c.G, c.B, c.A);

    /// <summary>A premultiplied MonoGame colour (what SpriteBatch's default blend expects), with alpha scaled by <paramref name="mul"/>.</summary>
    public static Color Premultiplied(this Rgba c, float mul = 1f)
    {
        float a = Math.Clamp(c.A / 255f * mul, 0f, 1f);
        return new Color((byte)(c.R * a), (byte)(c.G * a), (byte)(c.B * a), (byte)(a * 255f));
    }
}
