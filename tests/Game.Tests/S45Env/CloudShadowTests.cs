// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;

namespace Rts.Game.Tests.S45Env;

/// <summary>M16-V-04: cloud shadow layout, drift, wrap, opacity and the dark-mood / storm switch (pure maths, no GPU).</summary>
public sealed class CloudShadowTests
{
    [Fact]
    public void Layout_is_repeatable_and_sized_by_the_map()
    {
        Assert.Equal(CloudShadows.Layout(64, 64), CloudShadows.Layout(64, 64));
        Assert.Equal(4, CloudShadows.CountFor(16, 16));
        Assert.Equal(CloudShadows.MaxBlobs, CloudShadows.CountFor(256, 256));
        Assert.Equal(CloudShadows.CountFor(64, 64), CloudShadows.Layout(64, 64).Length);
    }

    [Fact]
    public void Blobs_are_large_and_soft_but_faint()
    {
        foreach (CloudShadows.Blob b in CloudShadows.Layout(64, 64))
        {
            Assert.InRange(b.RadiusX, 4 * Camera.CellPx, 10 * Camera.CellPx);
            byte a = CloudShadows.AlphaOf(b);
            Assert.InRange(a / 255.0, 0.07, 0.15);   // 10 to 15 % at the centre, less for weak blobs
        }
    }

    [Fact]
    public void Drift_follows_the_wind_from_game_ticks_only()
    {
        var b = new CloudShadows.Blob(1000, 800, 300, 200, 1f);
        Vector2 wind = CloudShadows.Wind(WeatherKind.Ash);
        Vector2 p0 = CloudShadows.Position(b, 0, wind, 64, 64);
        Vector2 p1 = CloudShadows.Position(b, 10 * CloudShadows.TicksPerSecond, wind, 64, 64);
        Assert.Equal(1000f, p0.X, 0.01f);
        Assert.Equal(wind.X * CloudShadows.SpeedPxPerSecond * 10, p1.X - p0.X, 0.05f);
        Assert.Equal(wind.Y * CloudShadows.SpeedPxPerSecond * 10, p1.Y - p0.Y, 0.05f);
        Assert.Equal(p1, CloudShadows.Position(b, 10 * CloudShadows.TicksPerSecond, wind, 64, 64));   // same tick, same place
    }

    [Fact]
    public void Wind_is_a_unit_vector_and_blows_west_only_with_westward_weather()
    {
        foreach (WeatherKind k in new[] { WeatherKind.None, WeatherKind.Ash, WeatherKind.Glass, WeatherKind.Snow, WeatherKind.Rain })
            Assert.Equal(1f, CloudShadows.Wind(k).Length(), 0.001f);
        Assert.True(CloudShadows.Wind(WeatherKind.Ash).X > 0);
        Assert.True(CloudShadows.Wind(WeatherKind.Rain).X < 0);
    }

    [Fact]
    public void Blobs_wrap_round_the_map_and_never_leave_the_wrapped_area()
    {
        Vector2 wind = CloudShadows.Wind(WeatherKind.None);
        int span = 64 * Camera.CellPx + 2 * CloudShadows.WrapMarginPx;
        foreach (CloudShadows.Blob b in CloudShadows.Layout(64, 64))
        {
            for (int tick = 0; tick < 20 * 3600 * 3; tick += 20 * 600)
            {
                Vector2 p = CloudShadows.Position(b, tick, wind, 64, 64);
                Assert.InRange(p.X, -CloudShadows.WrapMarginPx, span - CloudShadows.WrapMarginPx);
                Assert.InRange(p.Y, -CloudShadows.WrapMarginPx, span - CloudShadows.WrapMarginPx);
            }
        }
    }

    [Fact]
    public void Touches_culls_blobs_outside_the_view()
    {
        var b = new CloudShadows.Blob(0, 0, 100, 100, 1f);
        Assert.True(CloudShadows.Touches(b, new Vector2(500, 500), 450, 450, 900, 900));
        Assert.False(CloudShadows.Touches(b, new Vector2(0, 0), 450, 450, 900, 900));
    }

    [Fact]
    public void Layer_is_off_in_storms_and_dark_moods_only()
    {
        Assert.True(CloudShadows.Active(MoodGrade.None, WeatherKind.None));
        Assert.True(CloudShadows.Active(new MoodGrade(0xE39A4A, 40, null), WeatherKind.Ash));   // warm dusty grade: still daylight
        Assert.False(CloudShadows.Active(MoodGrade.None, WeatherKind.Rain));                       // a storm
        Assert.False(CloudShadows.Active(new MoodGrade(0x101830, 60, null), WeatherKind.None));   // night-blue grade
        Assert.True(CloudShadows.Active(new MoodGrade(0x101830, 0, null), WeatherKind.None));     // a dark tint with no strength changes nothing
    }

    [Fact]
    public void Soft_texture_peaks_in_the_middle_and_is_clear_at_the_rim()
    {
        const int n = 64;
        byte[] rgba = CloudShadowLayer.BuildSoftTexture(n);
        int At(int x, int y) => rgba[(y * n + x) * 4 + 3];
        Assert.True(At(n / 2, n / 2) > 240);
        Assert.Equal(0, At(0, 0));
        Assert.True(At(n - 1, n / 2) <= 2);
        Assert.True(At(n / 2, n / 2) > At(n / 4, n / 2) && At(n / 4, n / 2) > At(n / 8, n / 2));
        for (int i = 0; i < rgba.Length; i += 4) Assert.Equal(0, rgba[i] | rgba[i + 1] | rgba[i + 2]);   // black: premultiplied alpha only
    }
}
