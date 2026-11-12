// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>
/// The pure maths of the weather layer (M10-12): which biome gets which weather, how many particles a view needs, and where particle
/// <c>i</c> is at a given time. Stateless: a particle's place is a function of its index, the time and the camera, from the integer
/// hash in <see cref="DecorHash"/>, never from the sim RNG, so a screenshot is repeatable. View only.
/// </summary>
public static class WeatherModel
{
    /// <summary>Most particles drawn at once, however big the view. ASSUMED.</summary>
    public const int MaxParticles = 700;

    /// <summary>How much of the camera's movement the particles follow (0 = glued to the screen, 1 = glued to the ground). ASSUMED.</summary>
    public const float Parallax = 0.5f;

    private static readonly string[] SnowWords = { "mountain", "snow", "tundra", "arctic", "frozen", "winter", "glacier", "icefield", "ice" };

    /// <summary>The weather for a theater or biome id (<c>rustbelt</c>, <c>rust_belt</c>, <c>ashplains</c>, <c>glass_coast</c>, <c>mountains</c> ...).</summary>
    public static WeatherKind KindFor(string? theaterId)
    {
        if (string.IsNullOrEmpty(theaterId)) return WeatherKind.None;
        string id = TerrainPalette.NormalizeTheaterId(theaterId);
        if (id.Contains("ashplain", StringComparison.Ordinal) || id == "ash") return WeatherKind.Ash;
        if (id.Contains("glasscoast", StringComparison.Ordinal) || id == "glass") return WeatherKind.Glass;
        if (id.Contains("rustbelt", StringComparison.Ordinal)) return WeatherKind.Rain;
        foreach (string w in SnowWords) if (id.Contains(w, StringComparison.Ordinal)) return WeatherKind.Snow;
        return WeatherKind.None;
    }

    /// <summary>Look and motion of a kind. <see cref="WeatherKind.None"/> has zero density.</summary>
    // ASSUMED (M10-12): densities and speeds tuned by eye for a subtle layer, not measured.
    public static WeatherProfile ProfileOf(WeatherKind kind) => kind switch
    {
        WeatherKind.Ash => new WeatherProfile(12f, 30f, 28f, 2, 3, 0, 10f, 0xB9B1A8, 150),
        WeatherKind.Glass => new WeatherProfile(9f, 110f, 14f, 1, 3, 0, 4f, 0xD8F2F0, 170),
        WeatherKind.Snow => new WeatherProfile(16f, 18f, 62f, 2, 4, 0, 14f, 0xF4F8FF, 200),
        WeatherKind.Rain => new WeatherProfile(10f, -110f, 720f, 1, 2, 12, 0f, 0x9DB3C9, 130),
        _ => default,
    };

    /// <summary>Particles for a view of <paramref name="width"/> by <paramref name="height"/> pixels: density times area, at most <see cref="MaxParticles"/>.</summary>
    public static int Count(WeatherKind kind, int width, int height)
    {
        if (kind == WeatherKind.None || width <= 0 || height <= 0) return 0;
        double n = ProfileOf(kind).PerHundredKilopixels * ((double)width * height / 100_000.0);
        return (int)Math.Clamp(Math.Round(n), 0, MaxParticles);
    }

    /// <summary>Size scale for a view height: 1 up to about 1300 px tall, 2 from 4K-ish heights (2160 = 2).</summary>
    public static int SizeScale(int height) => Math.Max(1, (height + 270) / 1080);

    /// <summary>
    /// Places particle <paramref name="index"/>. Particles wrap around the view. <paramref name="camX"/> and <paramref name="camY"/> are the camera
    /// position in screen pixels (world pixels times zoom); moving the camera slides the particles by <see cref="Parallax"/> of that.
    /// </summary>
    public static WeatherParticle Place(WeatherKind kind, int index, double seconds, double camX, double camY, int width, int height, ulong seed = 0x5745_4154UL)
    {
        WeatherProfile p = ProfileOf(kind);
        uint h1 = DecorHash.Mix(seed, index, 1, 11);
        uint h2 = DecorHash.Mix(seed, index, 2, 12);
        uint h3 = DecorHash.Mix(seed, index, 3, 13);
        double depth = (h3 & 255) / 255.0;                    // 0 far .. 1 near
        double speed = 0.7 + 0.6 * depth;                     // nearer particles move faster
        double phase = (h2 >> 16) / 65536.0 * Math.PI * 2;
        double sway = p.Sway * Math.Sin(seconds * (0.6 + depth * 0.5) + phase);
        int scale = SizeScale(height);
        int size = (p.SizeMin + (int)(depth * (p.SizeMax - p.SizeMin) + 0.5)) * scale;
        double par = Parallax * (0.5 + 0.5 * depth);
        double x = Wrap((h1 & 0xFFFF) / 65536.0 * width + p.VelocityX * speed * seconds * scale + sway * scale - camX * par, width);
        double y = Wrap((h1 >> 16) / 65536.0 * height + p.VelocityY * speed * seconds * scale - camY * par, height);
        int alpha = (int)(p.MaxAlpha * (0.45 + 0.55 * depth));
        return new WeatherParticle((int)x, (int)y, size, size + p.Streak * scale, alpha);
    }

    private static double Wrap(double v, int size)
    {
        double r = v % size;
        return r < 0 ? r + size : r;
    }
}
