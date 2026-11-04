// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Menu;

/// <summary>
/// The pure maths behind the menu background: parallax offsets, slow camera drift, looping and cover-scaling. No state, no
/// clocks, so the same time gives the same picture (and the tests can pin it).
/// </summary>
public static class ParallaxMath
{
    /// <summary>The virtual canvas the scene data is authored on.</summary>
    public const float RefWidth = 1920f;

    /// <summary>The virtual canvas the scene data is authored on.</summary>
    public const float RefHeight = 1080f;

    /// <summary>
    /// How far a layer slides when the camera is at <paramref name="camera"/>: factor 0 is the sky (never moves), 1 is the
    /// ground plane (moves one for one with the camera, in the opposite direction), above 1 is a close foreground.
    /// </summary>
    public static float LayerOffset(float camera, float factor) => -camera * factor;

    /// <summary>A slow sinusoidal drift: <paramref name="amplitude"/> peak, one full swing every <paramref name="period"/> seconds.</summary>
    public static float Drift(float seconds, float amplitude, float period, float phase = 0f) =>
        period <= 0f ? 0f : amplitude * MathF.Sin(MathF.Tau * (seconds / period) + phase);

    /// <summary>Positive modulo: <paramref name="value"/> folded into 0 (inclusive) .. <paramref name="period"/> (exclusive).</summary>
    public static float Wrap(float value, float period)
    {
        if (period <= 0f) return value;
        float r = value % period;
        return r < 0f ? r + period : r;
    }

    /// <summary>Scale that makes the reference canvas cover the whole window (no bars), keeping the aspect ratio.</summary>
    public static float CoverScale(float viewWidth, float viewHeight) =>
        MathF.Max(viewWidth / RefWidth, viewHeight / RefHeight);

    /// <summary>Screen position of the reference canvas' top-left corner when it is centred in the window at <paramref name="scale"/>.</summary>
    public static (float X, float Y) CoverOrigin(float viewWidth, float viewHeight, float scale) =>
        ((viewWidth - RefWidth * scale) / 2f, (viewHeight - RefHeight * scale) / 2f);

    /// <summary>Hermite smoothstep of <paramref name="t"/> clamped to 0..1 (used for cross-fades).</summary>
    public static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Fraction part of a value, always 0..1 (also for negatives).</summary>
    public static float Frac(float v) => v - MathF.Floor(v);
}
