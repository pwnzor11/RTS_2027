// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Render;

/// <summary>
/// The maths of drifting cloud shadows (design 18 section 3, mood row; M16-V-04). A fixed set of large soft blobs per map size, each moving
/// along the wind and wrapping round the map, placed from the game tick only: no sim RNG, no wall clock, so a screenshot is repeatable.
/// View only; see <see cref="CloudShadowLayer"/> for the drawing.
/// </summary>
public static class CloudShadows
{
    /// <summary>Sim ticks per second (the sim runs at 20 Hz).</summary>
    public const int TicksPerSecond = 20;

    /// <summary>Opacity of a blob at its centre, 0..255 (32 = 12.5 %, inside the 10 to 15 % brief). ASSUMED.</summary>
    public const byte PeakAlpha = 32;

    /// <summary>Drift speed in zoom-1 world pixels per second (about a tenth of a cell a second). ASSUMED.</summary>
    public const float SpeedPxPerSecond = 7f;

    /// <summary>Extra room round the map in which a blob wraps, so none pops in view at the edge. Zoom-1 pixels.</summary>
    public const int WrapMarginPx = 768;

    /// <summary>Most blobs on any map.</summary>
    public const int MaxBlobs = 24;

    private const ulong Seed = 0x434C_4F55UL;

    /// <summary>One cloud: its start centre and half-size in zoom-1 world pixels.</summary>
    /// <param name="X">Start centre x (before drift).</param>
    /// <param name="Y">Start centre y.</param>
    /// <param name="RadiusX">Half width.</param>
    /// <param name="RadiusY">Half height.</param>
    /// <param name="Strength">Share of <see cref="PeakAlpha"/> at the centre, 0.6 to 1.</param>
    public readonly record struct Blob(float X, float Y, float RadiusX, float RadiusY, float Strength);

    /// <summary>How many blobs a map gets: about one per 150 cells, at least 4.</summary>
    public static int CountFor(int mapWidth, int mapHeight) => Math.Clamp(mapWidth * mapHeight / 150, 4, MaxBlobs);

    /// <summary>The blobs of a map, the same every time for the same size.</summary>
    public static Blob[] Layout(int mapWidth, int mapHeight)
    {
        int n = CountFor(mapWidth, mapHeight);
        int spanX = WrapSpan(mapWidth), spanY = WrapSpan(mapHeight);
        var blobs = new Blob[n];
        for (int i = 0; i < n; i++)
        {
            uint h1 = DecorHash.Mix(Seed, i, mapWidth, 1), h2 = DecorHash.Mix(Seed, i, mapHeight, 2), h3 = DecorHash.Mix(Seed, i, 0, 3);
            float rx = (5f + (h1 & 255) / 255f * 4f) * Camera.CellPx;      // 5 to 9 cells across
            float ry = rx * (0.55f + ((h1 >> 8) & 255) / 255f * 0.35f);
            blobs[i] = new Blob(
                (h2 & 0xFFFF) / 65536f * spanX - WrapMarginPx,
                (h2 >> 16) / 65536f * spanY - WrapMarginPx,
                rx, ry, 0.6f + (h3 & 255) / 255f * 0.4f);
        }

        return blobs;
    }

    /// <summary>The unit wind direction: east-south-east by default, west when the weather's own particles blow west.</summary>
    public static Vector2 Wind(WeatherKind kind)
    {
        float sx = WeatherModel.ProfileOf(kind).VelocityX < 0f ? -1f : 1f;
        var v = new Vector2(sx, 0.3f);
        v.Normalize();
        return v;
    }

    /// <summary>Where blob <paramref name="blob"/> is after <paramref name="tick"/> ticks: its centre in zoom-1 world pixels, wrapped round the map plus margin.</summary>
    public static Vector2 Position(Blob blob, int tick, Vector2 wind, int mapWidth, int mapHeight)
    {
        float t = tick / (float)TicksPerSecond;
        int spanX = WrapSpan(mapWidth), spanY = WrapSpan(mapHeight);
        float x = Wrap(blob.X + WrapMarginPx + wind.X * SpeedPxPerSecond * t, spanX) - WrapMarginPx;
        float y = Wrap(blob.Y + WrapMarginPx + wind.Y * SpeedPxPerSecond * t, spanY) - WrapMarginPx;
        return new Vector2(x, y);
    }

    /// <summary>The opacity at a blob's centre, 0..255.</summary>
    public static byte AlphaOf(Blob blob) => (byte)Math.Clamp((int)MathF.Round(PeakAlpha * blob.Strength), 0, 255);

    /// <summary>
    /// Whether the layer is drawn: not under a storm (rain) and not under a dark mood grade (a night-like tint). The mood table has no "night"
    /// flag, so a grade counts as dark when it is active, at least 30 strong and its tint's luminance is under 110 of 255. ASSUMED.
    /// </summary>
    public static bool Active(MoodGrade mood, WeatherKind weather)
    {
        ArgumentNullException.ThrowIfNull(mood);
        if (weather == WeatherKind.Rain) return false;
        if (mood.Active && mood.Strength >= 30)
        {
            uint t = mood.Tint;
            double lum = 0.2126 * ((t >> 16) & 255) + 0.7152 * ((t >> 8) & 255) + 0.0722 * (t & 255);
            if (lum < 110.0) return false;
        }

        return true;
    }

    /// <summary>Whether a blob at <paramref name="centre"/> touches the world rectangle (left, top, right, bottom).</summary>
    public static bool Touches(Blob blob, Vector2 centre, float left, float top, float right, float bottom) =>
        centre.X + blob.RadiusX >= left && centre.X - blob.RadiusX <= right && centre.Y + blob.RadiusY >= top && centre.Y - blob.RadiusY <= bottom;

    private static int WrapSpan(int cells) => cells * Camera.CellPx + 2 * WrapMarginPx;

    private static float Wrap(float v, int span)
    {
        float r = v % span;
        return r < 0 ? r + span : r;
    }
}
