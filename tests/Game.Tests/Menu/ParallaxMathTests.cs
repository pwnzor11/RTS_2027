// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Menu;

namespace Rts.Game.Tests.Menu;

public sealed class ParallaxMathTests
{
    [Theory]
    [InlineData(100f, 0f, 0f)]       // sky never moves
    [InlineData(100f, 1f, -100f)]    // ground plane moves one for one, opposite the camera
    [InlineData(100f, 0.25f, -25f)]
    [InlineData(-40f, 1.5f, 60f)]    // foreground moves faster than the ground
    public void Layer_offset_is_minus_camera_times_factor(float camera, float factor, float expected) =>
        Assert.Equal(expected, ParallaxMath.LayerOffset(camera, factor), 4);

    [Fact]
    public void Deeper_layers_move_less_than_nearer_ones()
    {
        float far = MathF.Abs(ParallaxMath.LayerOffset(80f, 0.1f));
        float mid = MathF.Abs(ParallaxMath.LayerOffset(80f, 0.45f));
        float near = MathF.Abs(ParallaxMath.LayerOffset(80f, 1.5f));
        Assert.True(far < mid && mid < near);
    }

    [Fact]
    public void Drift_is_sinusoidal_with_the_given_amplitude_and_period()
    {
        Assert.Equal(0f, ParallaxMath.Drift(0f, 90f, 60f), 3);
        Assert.Equal(90f, ParallaxMath.Drift(15f, 90f, 60f), 3);    // quarter period: peak
        Assert.Equal(0f, ParallaxMath.Drift(30f, 90f, 60f), 3);
        Assert.Equal(-90f, ParallaxMath.Drift(45f, 90f, 60f), 3);
        Assert.Equal(ParallaxMath.Drift(7f, 90f, 60f), ParallaxMath.Drift(67f, 90f, 60f), 3);   // repeats every period
        Assert.Equal(0f, ParallaxMath.Drift(12f, 90f, 0f));                                        // no period, no drift
    }

    [Theory]
    [InlineData(5f, 10f, 5f)]
    [InlineData(10f, 10f, 0f)]
    [InlineData(-1f, 10f, 9f)]
    [InlineData(-25f, 10f, 5f)]
    [InlineData(37f, 10f, 7f)]
    public void Wrap_is_a_positive_modulo(float value, float period, float expected) =>
        Assert.Equal(expected, ParallaxMath.Wrap(value, period), 4);

    [Fact]
    public void Cover_scale_fills_the_window_for_any_aspect_ratio()
    {
        foreach ((float w, float h) in new[] { (1280f, 720f), (1920f, 1080f), (2560f, 1080f), (1024f, 768f), (800f, 1200f) })
        {
            float k = ParallaxMath.CoverScale(w, h);
            (float ox, float oy) = ParallaxMath.CoverOrigin(w, h, k);
            Assert.True(ox <= 0.001f && oy <= 0.001f, $"{w}x{h}: canvas starts inside the window");
            Assert.True(ox + ParallaxMath.RefWidth * k >= w - 0.01f, $"{w}x{h}: right edge covered");
            Assert.True(oy + ParallaxMath.RefHeight * k >= h - 0.01f, $"{w}x{h}: bottom edge covered");
        }
        Assert.Equal(1f, ParallaxMath.CoverScale(1920f, 1080f), 4);
    }

    [Fact]
    public void Smoothstep_is_0_at_the_start_1_at_the_end_and_clamped()
    {
        Assert.Equal(0f, ParallaxMath.Smooth(-3f));
        Assert.Equal(0.5f, ParallaxMath.Smooth(0.5f), 4);
        Assert.Equal(1f, ParallaxMath.Smooth(9f));
    }

    [Fact]
    public void Timeline_holds_a_scene_then_cross_fades_into_the_next_and_loops()
    {
        float[] d = { 20f, 25f, 30f };

        SceneCursor start = SceneTimeline.Locate(d, 3f, 0f);
        Assert.Equal((0, 0, 0f), (start.Current, start.Next, start.Mix));

        SceneCursor held = SceneTimeline.Locate(d, 3f, 16.9f);
        Assert.Equal(0, held.Current);
        Assert.Equal(0f, held.Mix);

        SceneCursor mid = SceneTimeline.Locate(d, 3f, 18.5f);   // halfway through the 17..20 fade
        Assert.Equal((0, 1), (mid.Current, mid.Next));
        Assert.Equal(0.5f, mid.Mix, 3);
        Assert.Equal(1.5f, mid.NextLocalTime, 3);

        SceneCursor second = SceneTimeline.Locate(d, 3f, 20.5f);
        Assert.Equal(1, second.Current);
        Assert.Equal(0f, second.Mix);
        // The incoming scene's clock carries straight on from the fade (3 s) instead of restarting.
        Assert.Equal(3.5f, second.LocalTime, 3);

        SceneCursor looped = SceneTimeline.Locate(d, 3f, 75f + 1f);
        Assert.Equal(0, looped.Current);
    }

    [Fact]
    public void Timeline_has_a_sane_answer_for_one_scene_or_none()
    {
        Assert.Equal(0, SceneTimeline.Locate(new[] { 25f }, 3f, 100f).Current);
        Assert.Equal(0f, SceneTimeline.Locate(new[] { 25f }, 3f, 100f).Mix);
        Assert.Equal(0, SceneTimeline.Locate(Array.Empty<float>(), 3f, 100f).Current);
    }

    [Fact]
    public void Light_curves_are_bounded_and_monotonic()
    {
        Assert.Equal(1f, LightMath.GlowFalloff(0f), 4);
        Assert.Equal(0f, LightMath.GlowFalloff(1f));
        Assert.True(LightMath.GlowFalloff(0.3f) > LightMath.GlowFalloff(0.6f));
        Assert.Equal(0f, LightMath.VignetteDarkness(0f, 0f));
        Assert.True(LightMath.VignetteDarkness(1f, 1f) > 0.95f);
        Assert.InRange(LightMath.GrainValue(3, 9), -1f, 1f);
    }
}
