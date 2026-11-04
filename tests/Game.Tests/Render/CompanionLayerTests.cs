// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Sprites;

namespace Rts.Game.Tests.Render;

/// <summary>Companion turret and shadow sheets (ART-RENDER): manifest keys, the hull-rotated turret pivot and the layer plan.</summary>
public sealed class CompanionLayerTests
{
    private const string PivotText = "pivot_x = 0.0\npivot_y = 0.2794\npivot_z = 0.6";

    private static string Sheet(string layer, string extra) => string.Join("\n",
        "file = \"t.png\"", "frame_w = 32", "frame_h = 32", "origin_x = 16", "origin_y = 24", $"layer = \"{layer}\"", extra,
        "[[state]]", "name = \"idle\"", "facings = 16", "frames = 1", "first = 0", "ticks_per_frame = 1");

    private static string Body() => string.Join("\n",
        "file = \"t.png\"", "frame_w = 32", "frame_h = 32", "origin_x = 16", "origin_y = 24",
        "[[state]]", "name = \"idle\"", "facings = 16", "frames = 1", "first = 0", "ticks_per_frame = 1");

    [Fact]
    public void Turret_manifest_parses_its_layer_and_pivot()
    {
        SpriteManifest m = SpriteManifest.Parse(Sheet("turret", PivotText), "x_turret", "p.sprite.toml");

        Assert.Equal("turret", m.Layer);
        Assert.Equal(new TurretPivot(0.0, 0.2794, 0.6), m.Pivot);
    }

    [Fact]
    public void Integer_pivot_values_are_accepted()
    {
        SpriteManifest m = SpriteManifest.Parse(Sheet("turret", "pivot_x = 0\npivot_y = 1\npivot_z = 0"), "x_turret", "p");

        Assert.Equal(new TurretPivot(0, 1, 0), m.Pivot);
    }

    [Fact]
    public void Body_manifest_defaults_to_body_layer_without_pivot_and_altitude()
    {
        SpriteManifest m = SpriteManifest.Parse(Body(), "x", "p");

        Assert.Equal("body", m.Layer);
        Assert.Null(m.Pivot);
        Assert.Equal(0.0, m.Altitude);
    }

    [Fact]
    public void Shadow_manifest_reads_altitude()
    {
        SpriteManifest m = SpriteManifest.Parse(Sheet("shadow", "altitude = 0.5882"), "x_shadow", "p");

        Assert.Equal("shadow", m.Layer);
        Assert.Equal(0.5882, m.Altitude);
        Assert.Null(m.Pivot);
    }

    [Fact]
    public void Pivot_on_a_body_manifest_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => SpriteManifest.Parse(Sheet("body", PivotText), "x", "p"));
    }

    [Fact]
    public void Layer_baked_is_rejected()
    {
        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => SpriteManifest.Parse(Sheet("baked", ""), "x", "p.sprite.toml"));
        Assert.Contains("p.sprite.toml", ex.Message);
    }

    [Fact]
    public void Turret_without_pivot_z_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => SpriteManifest.Parse(Sheet("turret", "pivot_x = 0.0\npivot_y = 0.2"), "x_turret", "p"));
    }

    [Theory]
    [InlineData(0.0, 0.2794, 45.0, 13, -8)]
    [InlineData(0.0, -0.25, 45.0, -11, 7)]
    [InlineData(0.0, 0.2794, 0.0, 0, -11)]
    [InlineData(1.0, 0.0, 90.0, 0, 41)]
    public void Pivot_offset_is_rotated_by_hull_angle_and_projected_at_40_degrees(double x, double y, double angle, int dx, int dy)
    {
        (int ox, int oy) = UnitLayerPlan.PivotOffset(new TurretPivot(x, y, 0.5), angle);

        Assert.Equal((dx, dy), (ox, oy));
    }

    [Fact]
    public void Layer_plan_draws_shadow_body_and_turret_from_companion_sheets()
    {
        SpriteManifest body = SpriteManifest.Parse(Body(), "x", "p");
        SpriteManifest turret = SpriteManifest.Parse(Sheet("turret", PivotText), "x_turret", "p");
        SpriteManifest shadow = SpriteManifest.Parse(Sheet("shadow", ""), "x_shadow", "p");

        IReadOnlyList<UnitLayer> plan = UnitLayerPlan.For(body, false, false, 0, null, false, 0, turret, shadow);

        Assert.Equal(new[] { UnitLayerKind.Shadow, UnitLayerKind.Body, UnitLayerKind.Turret }, plan.Select(l => l.Kind));
        Assert.Equal(new[] { UnitLayerSheet.ShadowCompanion, UnitLayerSheet.Own, UnitLayerSheet.TurretCompanion }, plan.Select(l => l.Sheet));
        Assert.Equal((0, 0), (plan[0].OffsetX, plan[0].OffsetY));
        Assert.Equal((0, -11), (plan[2].OffsetX, plan[2].OffsetY));
    }

    [Fact]
    public void Layer_plan_turret_offset_follows_the_hull_facing()
    {
        SpriteManifest body = SpriteManifest.Parse(Body(), "x", "p");
        SpriteManifest turret = SpriteManifest.Parse(Sheet("turret", PivotText), "x_turret", "p");

        // Facing 64 is a quarter turn (16 facings, 90 degrees): the forward pivot points east, so the offset is (18, 0).
        UnitLayer t = UnitLayerPlan.For(body, false, false, 64, null, false, 0, turret).Single(l => l.Kind == UnitLayerKind.Turret);

        Assert.Equal((18, 0), (t.OffsetX, t.OffsetY));
        Assert.Equal(4, t.Frame);   // 16-facing companion, facing 64 = index 4
    }

    [Fact]
    public void Without_companions_a_body_unit_is_one_own_body_layer()
    {
        SpriteManifest body = SpriteManifest.Parse(Body(), "x", "p");

        UnitLayer only = Assert.Single(UnitLayerPlan.For(body, false, false, 0, null, false, 0));

        Assert.Equal(UnitLayerKind.Body, only.Kind);
        Assert.Equal(UnitLayerSheet.Own, only.Sheet);
    }

    [Fact]
    public void Shipped_reactor_crawler_has_a_turret_companion_and_no_shadow_companion()
    {
        using SpriteLibrary library = SpriteLibrary.Scan(SpriteManifestTests.RepoFile("data", "sprites"));

        Assert.NotNull(library.Companion("dir_reactor_crawler", "turret"));
        Assert.Null(library.Companion("dir_reactor_crawler", "shadow"));
        Assert.NotNull(library.Companion("dir_dirigible", "shadow"));
    }

    [Fact]
    public void A_turret_sheet_without_its_body_sheet_is_rejected_by_scan()
    {
        string root = Path.Combine(Path.GetTempPath(), "companion_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "orphan_turret.sprite.toml"), Sheet("turret", PivotText));

            Assert.Throws<InvalidDataException>(() => SpriteLibrary.Scan(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
