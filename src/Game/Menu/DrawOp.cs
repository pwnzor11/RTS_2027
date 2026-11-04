// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Menu;

/// <summary>What a <see cref="DrawOp"/> asks the renderer to draw.</summary>
public enum DrawOpKind
{
    /// <summary>A rectangle, optionally with a top-to-bottom colour gradient.</summary>
    Rect,
    /// <summary>One sprite frame from the sprite library.</summary>
    Sprite,
    /// <summary>A soft elliptical light or shadow.</summary>
    Glow,
    /// <summary>Darkened window edges.</summary>
    Vignette,
    /// <summary>Film grain over the whole window.</summary>
    Grain,
    /// <summary>A line of UI text.</summary>
    Text,
}

/// <summary>
/// One drawing instruction in screen pixels. The scene composer and the menu layout produce lists of these; the GPU
/// renderer (<see cref="DrawOpRenderer"/>) draws them with SpriteBatch and the test proof renders draw the very same list on
/// the CPU, so a screenshot shows exactly what the player would see.
/// </summary>
public readonly record struct DrawOp
{
    /// <summary>What to draw.</summary>
    public DrawOpKind Kind { get; init; }

    /// <summary>Rect: left. Sprite: x of the origin pixel. Glow: centre x. Text: left.</summary>
    public float X { get; init; }

    /// <summary>Rect: top. Sprite: y of the origin pixel. Glow: centre y. Text: top.</summary>
    public float Y { get; init; }

    /// <summary>Rect: width. Glow: x radius.</summary>
    public float W { get; init; }

    /// <summary>Rect: height. Glow: y radius.</summary>
    public float H { get; init; }

    /// <summary>Rect: top colour. Glow: colour. Sprite: house tint. Text: colour.</summary>
    public Rgba Colour { get; init; }

    /// <summary>Rect: bottom colour (equal to <see cref="Colour"/> for a flat fill).</summary>
    public Rgba Colour2 { get; init; }

    /// <summary>Sprite id, or the text to draw.</summary>
    public string? Id { get; init; }

    /// <summary>Sprite: frame index. Text: the 5x7 scale (screen pixels per font pixel). Grain: frame counter.</summary>
    public int Frame { get; init; }

    /// <summary>Sprite: draw scale.</summary>
    public float Scale { get; init; }

    /// <summary>Glow: add the light to what is below instead of blending over it.</summary>
    public bool Additive { get; init; }

    /// <summary>Sprite: brightness 0..1 (1 = as drawn). Vignette/Grain: strength 0..1.</summary>
    public float Amount { get; init; }

    /// <summary>Sprite: opacity 0..1.</summary>
    public float Alpha { get; init; }

    /// <summary>Text: true for the bold face.</summary>
    public bool Bold { get; init; }

    /// <summary>A flat or gradient rectangle.</summary>
    public static DrawOp Rect(float x, float y, float w, float h, Rgba top, Rgba? bottom = null) =>
        new() { Kind = DrawOpKind.Rect, X = x, Y = y, W = w, H = h, Colour = top, Colour2 = bottom ?? top };

    /// <summary>A sprite frame with its origin pixel at (x, y).</summary>
    public static DrawOp Sprite(string id, int frame, float x, float y, float scale, Rgba house, float brightness, float alpha) =>
        new() { Kind = DrawOpKind.Sprite, Id = id, Frame = frame, X = x, Y = y, Scale = scale, Colour = house, Amount = brightness, Alpha = alpha };

    /// <summary>A soft ellipse of light (or shadow when not additive).</summary>
    public static DrawOp Glow(float cx, float cy, float rx, float ry, Rgba colour, bool additive) =>
        new() { Kind = DrawOpKind.Glow, X = cx, Y = cy, W = rx, H = ry, Colour = colour, Colour2 = colour, Additive = additive };

    /// <summary>Window-edge darkening.</summary>
    public static DrawOp Vignette(float strength) => new() { Kind = DrawOpKind.Vignette, Amount = strength };

    /// <summary>Film grain.</summary>
    public static DrawOp Grain(float strength, int frame) => new() { Kind = DrawOpKind.Grain, Amount = strength, Frame = frame };

    /// <summary>A line of text with its top-left at (x, y); <paramref name="scale"/> is the 5x7 font scale.</summary>
    public static DrawOp Text(string text, float x, float y, int scale, Rgba colour, bool bold = false) =>
        new() { Kind = DrawOpKind.Text, Id = text, X = x, Y = y, Frame = scale, Colour = colour, Colour2 = colour, Bold = bold };
}

/// <summary>The shared falloff curves, so the GPU textures and the CPU proof agree.</summary>
public static class LightMath
{
    /// <summary>Glow brightness 0..1 at normalised distance <paramref name="d"/> from the centre (0 centre, 1 edge).</summary>
    public static float GlowFalloff(float d)
    {
        if (d >= 1f) return 0f;
        float f = 1f - d;
        return f * f * (0.35f + 0.65f * f);
    }

    /// <summary>Vignette darkness 0..1 at a window position given as -1..1 on each axis (strength 1).</summary>
    public static float VignetteDarkness(float nx, float ny)
    {
        // Elliptical: clear in the middle 55%, then a smooth ramp to full darkness at the corners.
        float d = MathF.Sqrt(nx * nx * 0.9f + ny * ny * 1.1f) / 1.2f;
        float t = Math.Clamp((d - 0.45f) / 0.55f, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Grain pixel brightness -1..1 for texel (x, y) of the 128x128 grain tile.</summary>
    public static float GrainValue(int x, int y)
    {
        uint h = (uint)(x & 127) * 374761393u + (uint)(y & 127) * 668265263u + 1442695041u;
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFu) / 32767.5f - 1f;
    }

    /// <summary>Where the grain tile starts for a frame (it jumps every frame, so the grain crawls).</summary>
    public static (int X, int Y) GrainOffset(int frame)
    {
        uint h = (uint)frame * 2654435761u + 12345u;
        h ^= h >> 15;
        h *= 2246822519u;
        return ((int)(h & 127u), (int)((h >> 8) & 127u));
    }
}
