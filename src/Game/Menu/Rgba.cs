// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;

namespace Rts.Game.Menu;

/// <summary>An 8-bit RGBA colour that does not depend on MonoGame, so scene maths and the software proof renderer stay pure. Not premultiplied.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    /// <summary>Opaque black.</summary>
    public static Rgba Black => new(0, 0, 0);

    /// <summary>Parses <c>#RRGGBB</c> or <c>#RRGGBBAA</c> (the # is optional). Returns false for anything else.</summary>
    public static bool TryParse(string? text, out Rgba colour)
    {
        colour = default;
        if (text is null) return false;
        string t = text.Trim().TrimStart('#');
        if (t.Length != 6 && t.Length != 8) return false;
        if (!uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)) return false;
        colour = t.Length == 6
            ? new Rgba((byte)(v >> 16), (byte)(v >> 8), (byte)v)
            : new Rgba((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    /// <summary>Linear blend of every channel, <paramref name="t"/> clamped to 0..1.</summary>
    public static Rgba Lerp(Rgba a, Rgba b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Rgba(L(a.R, b.R, t), L(a.G, b.G, t), L(a.B, b.B, t), L(a.A, b.A, t));
    }

    /// <summary>The same colour with its alpha multiplied by <paramref name="factor"/> (0..1).</summary>
    public Rgba Fade(float factor) => this with { A = (byte)Math.Clamp((int)MathF.Round(A * Math.Clamp(factor, 0f, 1f)), 0, 255) };

    private static byte L(byte a, byte b, float t) => (byte)Math.Clamp((int)MathF.Round(a + (b - a) * t), 0, 255);
}
