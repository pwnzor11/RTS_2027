// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Render;

/// <summary>M11-06 / D-172 (3): units are drawn 25 % larger around their ground point; pick radius and frame tops follow.</summary>
public sealed class UnitDrawScaleTests
{
    private static SpriteManifest Manifest() => SpriteManifest.Parse(string.Join("\n",
        "file = \"t.png\"", "frame_w = 32", "frame_h = 32", "origin_x = 16", "origin_y = 24",
        "[[state]]", "name = \"idle\"", "facings = 16", "frames = 1", "first = 0", "ticks_per_frame = 1"), "x", "p");

    [Fact]
    public void Constant_is_one_and_a_quarter() => Assert.Equal(1.25f, SpriteLayout.UnitDrawScale);

    [Theory]
    [InlineData(1f, 1f, 1.25f)]
    [InlineData(1f, 2f, 2.5f)]
    [InlineData(2f, 1f, 2.5f)]   // a big ship keeps its own scale on top
    public void Sprite_scale_multiplies_type_scale_and_zoom(float type, float zoom, float expected) =>
        Assert.Equal(expected, SpriteLayout.UnitSpriteScale(type, zoom), 4);

    [Fact]
    public void Frame_rect_scales_about_the_origin_pixel()
    {
        var origin = new Vector2(100, 200);
        (Vector2 topLeft, Vector2 size) = SpriteLayout.UnitFrameRect(origin, Manifest(), SpriteLayout.UnitSpriteScale(1f, 1f));
        Assert.Equal(new Vector2(40f, 40f), size);                 // 32 * 1.25
        Assert.Equal(new Vector2(100 - 20f, 200 - 30f), topLeft);  // origin (16,24) * 1.25
        // the ground contact point stays at the origin
        Assert.Equal(origin, topLeft + new Vector2(16, 24) * 1.25f);
    }

    [Fact]
    public void Frame_top_for_health_bars_rises_with_the_scale()
    {
        var origin = new Vector2(100, 200);
        float plain = SpriteLayout.FrameTopLeft(origin, Manifest(), 1f).Y;
        float scaled = SpriteLayout.FrameTopLeft(origin, Manifest(), SpriteLayout.UnitSpriteScale(1f, 1f)).Y;
        Assert.Equal(176f, plain);
        Assert.Equal(170f, scaled);
    }

    [Fact]
    public void Pick_radius_covers_the_scaled_vehicle_bracket()
    {
        const float vehicleBracketHalfAtZoom1 = 26f * SpriteLayout.UnitDrawScale;
        Assert.Equal(40f, Selection.PickRadiusPx);
        Assert.True(Selection.PickRadiusPx >= vehicleBracketHalfAtZoom1);
    }

    [Fact]
    public void Click_on_the_scaled_body_edge_selects_the_unit_and_a_far_click_does_not()
    {
        var unit = new RenderUnit(new UnitView(new EntityId(1, 1), UnitKind.Infantry, 0, 0, new LeptonCoord(0, 0), 0, 100),
            new Vector2(96, 96));
        var list = new[] { unit };
        Assert.NotNull(Selection.Pick(list, new Vector2(96 + 39, 96), 0));   // inside the 40 px radius (other cell)
        Assert.Null(Selection.Pick(list, new Vector2(96 + 41, 96), 0));
    }
}
