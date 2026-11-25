// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;

namespace Rts.Game.Tests.S45Fx;

/// <summary>S45 V-04: the glow texture, flash fade, flicker and the flash cap.</summary>
public sealed class GlowTests
{
    private static GlowFlash Flash(int born, int life = 10) => new(new GlowSource(0, 0, 30f, 0xFFFFFF, 1f), born, life);

    [Fact]
    public void Falloff_is_one_at_the_centre_zero_at_the_edge_and_never_rises()
    {
        Assert.Equal(1f, GlowModel.Falloff(0f), 3);
        Assert.Equal(0f, GlowModel.Falloff(1f));
        Assert.Equal(0f, GlowModel.Falloff(2f));
        float last = 2f;
        for (int i = 0; i <= 100; i++)
        {
            float f = GlowModel.Falloff(i / 100f);
            Assert.InRange(f, 0f, 1f);
            Assert.True(f <= last);
            last = f;
        }
    }

    [Fact]
    public void Texture_is_square_bright_in_the_middle_and_clear_in_the_corners()
    {
        byte[] px = GlowModel.BuildPixels();
        int n = GlowModel.TexturePx;
        Assert.Equal(n * n * 4, px.Length);
        int mid = ((n / 2) * n + n / 2) * 4;
        Assert.True(px[mid] >= 250 && px[mid + 3] >= 250);
        Assert.Equal(0, px[0]);
        Assert.Equal(0, px[3]);
        Assert.Equal(0, px[(n * n - 1) * 4]);
        Assert.Equal(px[(5 * n + 20) * 4], px[(20 * n + 5) * 4]);   // symmetric
    }

    [Fact]
    public void Flash_strength_flares_and_dies()
    {
        Assert.Equal(1f, GlowModel.FlashStrength(0, 10));
        Assert.True(GlowModel.FlashStrength(3, 10) > GlowModel.FlashStrength(6, 10));
        Assert.Equal(0f, GlowModel.FlashStrength(10, 10));
        Assert.Equal(0f, GlowModel.FlashStrength(-1, 10));
    }

    [Fact]
    public void Flicker_is_deterministic_bounded_and_steady_on_request()
    {
        for (int tick = 0; tick < 200; tick++)
        {
            float f = GlowModel.Flicker(42u, tick, false);
            Assert.InRange(f, 0.7f, 1f);
            Assert.Equal(f, GlowModel.Flicker(42u, tick, false));
            Assert.Equal(0.85f, GlowModel.Flicker(42u, tick, true));
        }

        Assert.Equal(GlowModel.Flicker(7u, 3, false), GlowModel.Flicker(7u, 5, false));   // changes every 3 ticks
        var seen = new HashSet<float>();
        for (int tick = 0; tick < 300; tick += 3) seen.Add(GlowModel.Flicker(7u, tick, false));
        Assert.True(seen.Count > 10);
    }

    [Fact]
    public void Field_caps_flashes_and_prunes_them()
    {
        var field = new GlowField();
        for (int i = 0; i < GlowModel.MaxFlashes + 10; i++) field.Add(Flash(i));
        Assert.Equal(GlowModel.MaxFlashes, field.Items.Count);
        Assert.Equal(10, field.Items[0].Born);
        field.Prune(1000);
        Assert.Empty(field.Items);
        field.Add(Flash(500));
        field.Prune(10);   // from the future
        Assert.Empty(field.Items);
    }

    [Fact]
    public void Collect_scales_the_intensity_by_age()
    {
        var field = new GlowField();
        field.Add(Flash(0));
        var fresh = new List<GlowSource>();
        var old = new List<GlowSource>();
        field.Collect(0, fresh);
        field.Collect(7, old);
        Assert.Equal(1f, fresh[0].Intensity);
        Assert.InRange(old[0].Intensity, 0.01f, 0.5f);
        var gone = new List<GlowSource>();
        field.Collect(10, gone);
        Assert.Empty(gone);
    }

    [Fact]
    public void Tint_is_the_colour_scaled_and_clamped()
    {
        Assert.Equal(new Microsoft.Xna.Framework.Color(255, 128, 0, 255), GlowModel.Tint(0xFF8000, 1f));
        Assert.Equal(new Microsoft.Xna.Framework.Color(0, 0, 0, 0), GlowModel.Tint(0xFF8000, -1f));
        Assert.Equal(GlowModel.Tint(0xFF8000, 1f), GlowModel.Tint(0xFF8000, 5f));
    }

    [Fact]
    public void Glow_toggle_defaults_on_and_round_trips()
    {
        Assert.True(Settings.Default.AdditiveGlow);
        Assert.False(Settings.Parse((Settings.Default with { AdditiveGlow = false }).ToToml()).AdditiveGlow);
        Assert.True(Settings.Parse("version = 2\n").AdditiveGlow);
    }
}
