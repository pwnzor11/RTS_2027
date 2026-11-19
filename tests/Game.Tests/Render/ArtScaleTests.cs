// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Game.Sprites;

namespace Rts.Game.Tests.Render;

/// <summary>
/// D-275: unit sheets are rendered at 80 px per cell (the size the x1.25 unit scale draws them at) and carry <c>px_per_cell = 80</c>;
/// the renderer converts sheet pixels with <see cref="SpriteManifest.ArtScale"/>, so an 80 px sheet reaches the screen 1:1 and lands
/// exactly where the old 64 px sheet did.
/// </summary>
public sealed class ArtScaleTests
{
    private static SpriteManifest Sheet(int frame, int ox, int oy, string extra = "") => SpriteManifest.Parse(string.Join("\n",
        "file = \"t.png\"", $"frame_w = {frame}", $"frame_h = {frame}", $"origin_x = {ox}", $"origin_y = {oy}", extra,
        "[[state]]", "name = \"idle\"", "facings = 16", "frames = 1", "first = 0", "ticks_per_frame = 1"), "x", "p");

    [Fact]
    public void Px_per_cell_defaults_to_the_world_cell()
    {
        SpriteManifest m = Sheet(112, 56, 70);
        Assert.Equal(64, m.PxPerCell);
        Assert.Equal(1f, m.ArtScale);
    }

    [Fact]
    public void An_80_px_sheet_has_art_scale_four_fifths()
    {
        SpriteManifest m = Sheet(140, 70, 88, "px_per_cell = 80");
        Assert.Equal(80, m.PxPerCell);
        Assert.Equal(0.8f, m.ArtScale, 5);
    }

    [Theory]
    [InlineData("px_per_cell = 0")]
    [InlineData("px_per_cell = -80")]
    public void Px_per_cell_must_be_positive(string line) =>
        Assert.Throws<InvalidDataException>(() => Sheet(140, 70, 88, line));

    [Fact]
    public void An_80_px_sheet_drawn_at_the_unit_scale_is_one_to_one()
    {
        SpriteManifest m = Sheet(140, 70, 88, "px_per_cell = 80");
        float sheetScale = SpriteLayout.UnitSpriteScale(1f, 1f) * m.ArtScale;
        Assert.Equal(1f, sheetScale, 5);
        Assert.Equal(2f, SpriteLayout.UnitSpriteScale(1f, 2f) * m.ArtScale, 5);   // zoom 2 doubles exactly
    }

    [Fact]
    public void Frame_top_of_an_80_px_sheet_matches_the_64_px_sheet_it_replaces()
    {
        var origin = new Vector2(300, 400);
        float scale = SpriteLayout.UnitSpriteScale(1f, 1f);
        float oldTop = SpriteLayout.FrameTopLeft(origin, Sheet(112, 56, 72), scale).Y;
        float newTop = SpriteLayout.FrameTopLeft(origin, Sheet(140, 70, 90, "px_per_cell = 80"), scale).Y;
        Assert.Equal(oldTop, newTop, 3);
        (_, Vector2 size) = SpriteLayout.UnitFrameRect(origin, Sheet(140, 70, 90, "px_per_cell = 80"), scale);
        Assert.Equal(new Vector2(140f, 140f), size);   // drawn at its own size
    }

    [Fact]
    public void Hull_lift_is_the_same_on_screen_for_both_sheet_sizes() =>
        Assert.Equal(DamageFx.HullLift(Sheet(112, 56, 72)), DamageFx.HullLift(Sheet(140, 70, 90, "px_per_cell = 80")), 3);

    [Theory]
    [InlineData(0.3, 0.5, 0.0)]
    [InlineData(-0.2, 0.4, 90.0)]
    [InlineData(0.0, -0.35, 202.5)]
    public void Turret_pivot_in_80_px_sheet_pixels_lands_on_the_same_screen_point(double x, double y, double angle)
    {
        var pivot = new TurretPivot(x, y, 0.5);
        (int ox, int oy) = UnitLayerPlan.PivotOffset(pivot, angle);
        (int nx, int ny) = UnitLayerPlan.PivotOffset(pivot, angle, 80);
        // old: 64 px offset x 1.25; new: 80 px offset x 1.0 (ArtScale 0.8 x 1.25). Both are rounded to whole sheet pixels.
        Assert.InRange(nx - ox * 1.25, -1.0, 1.0);
        Assert.InRange(ny - oy * 1.25, -1.0, 1.0);
    }

    [Fact]
    public void Every_unit_sheet_is_rendered_at_80_px_and_companions_match_their_body()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "sprites"));
        var seen = 0;
        foreach (string kind in new[] { "vehicles", "infantry" })
        {
            foreach (string path in Directory.GetFiles(Path.Combine(root, kind), "*.sprite.toml"))
            {
                SpriteManifest m = SpriteManifest.Load(path);
                Assert.True(m.PxPerCell == 80, $"{path}: px_per_cell {m.PxPerCell}, expected 80 (D-275)");
                seen++;
            }
        }
        Assert.True(seen >= 100, $"only {seen} unit manifests found under {root}");
    }
}
