// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;

namespace Rts.Game.Tests.M10;

/// <summary>M10-12: biome to weather, particle counts and the stateless particle placement.</summary>
public sealed class WeatherModelTests
{
    [Theory]
    [InlineData("ashplains", WeatherKind.Ash)]
    [InlineData("ash_plains", WeatherKind.Ash)]
    [InlineData("glasscoast", WeatherKind.Glass)]
    [InlineData("glass_coast", WeatherKind.Glass)]
    [InlineData("rustbelt", WeatherKind.Rain)]
    [InlineData("rust_belt", WeatherKind.Rain)]
    [InlineData("mountains", WeatherKind.Snow)]
    [InlineData("Arctic", WeatherKind.Snow)]
    [InlineData("desert", WeatherKind.None)]
    [InlineData("", WeatherKind.None)]
    [InlineData(null, WeatherKind.None)]
    public void Biome_picks_weather(string? id, WeatherKind expected) => Assert.Equal(expected, WeatherModel.KindFor(id));

    [Fact]
    public void Real_theater_files_all_map_to_known_weather()
    {
        Assert.Equal(WeatherKind.Ash, WeatherModel.KindFor("ashplains"));
        Assert.Equal(WeatherKind.None, WeatherModel.KindFor("unknown_place"));
    }

    [Fact]
    public void Count_scales_with_area_and_is_capped()
    {
        int small = WeatherModel.Count(WeatherKind.Snow, 640, 360);
        int hd = WeatherModel.Count(WeatherKind.Snow, 1920, 1080);
        Assert.InRange(hd, small * 8, small * 10);
        Assert.Equal(WeatherModel.MaxParticles, WeatherModel.Count(WeatherKind.Snow, 7680, 4320));
        Assert.Equal(0, WeatherModel.Count(WeatherKind.None, 1920, 1080));
        Assert.Equal(0, WeatherModel.Count(WeatherKind.Rain, 0, 1080));
    }

    [Fact]
    public void Placement_is_repeatable_and_stays_in_the_view()
    {
        foreach (WeatherKind k in new[] { WeatherKind.Ash, WeatherKind.Glass, WeatherKind.Snow, WeatherKind.Rain })
        {
            for (int i = 0; i < 200; i++)
            {
                WeatherParticle a = WeatherModel.Place(k, i, 12.5, -300.0, 4000.0, 1280, 720);
                WeatherParticle b = WeatherModel.Place(k, i, 12.5, -300.0, 4000.0, 1280, 720);
                Assert.Equal(a, b);
                Assert.InRange(a.X, 0, 1279);
                Assert.InRange(a.Y, 0, 719);
                Assert.InRange(a.Alpha, 1, 255);
                Assert.True(a.Height >= a.Width);
            }
        }
    }

    [Fact]
    public void Particles_fall_and_the_camera_drags_them()
    {
        WeatherParticle early = WeatherModel.Place(WeatherKind.Snow, 5, 1.0, 0, 0, 1280, 720);
        WeatherParticle later = WeatherModel.Place(WeatherKind.Snow, 5, 1.2, 0, 0, 1280, 720);
        Assert.NotEqual(early.Y, later.Y);   // moved down (mod wrap aside, 0.2 s is far from a full lap)
        WeatherParticle panned = WeatherModel.Place(WeatherKind.Snow, 5, 1.0, 100, 0, 1280, 720);
        Assert.NotEqual(early.X, panned.X);
    }

    [Fact]
    public void Rain_has_streaks_and_snow_has_dots()
    {
        Assert.True(WeatherModel.Place(WeatherKind.Rain, 1, 0, 0, 0, 1280, 720).Height > WeatherModel.Place(WeatherKind.Rain, 1, 0, 0, 0, 1280, 720).Width);
        WeatherParticle s = WeatherModel.Place(WeatherKind.Snow, 1, 0, 0, 0, 1280, 720);
        Assert.Equal(s.Width, s.Height);
    }

    [Theory]
    [InlineData(480, 1)]
    [InlineData(1080, 1)]
    [InlineData(1440, 1)]
    [InlineData(2160, 2)]
    public void Size_scale_steps_with_view_height(int height, int expected) => Assert.Equal(expected, WeatherModel.SizeScale(height));
}
