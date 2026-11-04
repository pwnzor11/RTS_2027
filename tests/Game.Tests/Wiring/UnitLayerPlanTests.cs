// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Sprites;

namespace Rts.Game.Tests.Wiring;

/// <summary>Layer plan of a unit sprite (M3-18): body-only units are unchanged, extra layers appear from manifest states.</summary>
public sealed class UnitLayerPlanTests
{
    private static string Text(string extra, string top = "") => string.Join("\n",
        "file = \"t.png\"", "frame_w = 32", "frame_h = 32", "origin_x = 16", "origin_y = 24", top,
        "[[state]]", "name = \"idle\"", "facings = 16", "frames = 1", "first = 0", "ticks_per_frame = 1", extra);

    private static string State(string name, int first) => string.Join("\n",
        "[[state]]", $"name = \"{name}\"", "facings = 16", "frames = 1", $"first = {first}", "ticks_per_frame = 1");

    [Fact]
    public void Body_only_unit_plan_is_the_single_legacy_frame()
    {
        SpriteManifest m = SpriteManifest.Parse(Text(""), "t", "t.toml");
        foreach (byte facing in new byte[] { 0, 37, 128, 255 })
        {
            IReadOnlyList<UnitLayer> plan = UnitLayerPlan.For(m, false, false, facing, null, false, 5);
            UnitLayer only = Assert.Single(plan);
            Assert.Equal(UnitLayerKind.Body, only.Kind);
            Assert.Equal(SpriteStates.UnitFrame(m, false, false, facing, 5), only.Frame);
            Assert.Equal(0, only.OffsetX);
            Assert.Equal(0, only.OffsetY);
        }
    }

    [Fact]
    public void Shadow_goes_under_the_body_with_the_manifest_offset_and_turret_on_top_with_its_own_facing()
    {
        string text = Text(State("shadow", 16) + "\n" + State("turret", 32), "shadow_dx = 6\nshadow_dy = -3");
        SpriteManifest m = SpriteManifest.Parse(text, "t", "t.toml");

        IReadOnlyList<UnitLayer> plan = UnitLayerPlan.For(m, false, false, 0, 64, false, 0);

        Assert.Equal(new[] { UnitLayerKind.Shadow, UnitLayerKind.Body, UnitLayerKind.Turret }, plan.Select(l => l.Kind));
        Assert.Equal((6, -3), (plan[0].OffsetX, plan[0].OffsetY));
        Assert.Equal(0, plan[1].Frame);
        Assert.Equal(32 + 4, plan[2].Frame);   // 64/256 of a turn = 4 of 16 facings
        Assert.Equal(UnitLayerPlan.For(m, false, false, 64, null, false, 0)[2].Frame, plan[2].Frame);   // null = body facing
    }

    [Fact]
    public void Dive_replaces_the_body_only_when_diving_and_defined()
    {
        SpriteManifest withDive = SpriteManifest.Parse(Text(State("dive", 16)), "t", "t.toml");
        Assert.Equal(UnitLayerKind.Dive, Assert.Single(UnitLayerPlan.For(withDive, false, false, 0, null, true, 0)).Kind);
        Assert.Equal(UnitLayerKind.Body, Assert.Single(UnitLayerPlan.For(withDive, false, false, 0, null, false, 0)).Kind);

        SpriteManifest without = SpriteManifest.Parse(Text(""), "t", "t.toml");
        Assert.Equal(UnitLayerKind.Body, Assert.Single(UnitLayerPlan.For(without, false, false, 0, null, true, 0)).Kind);
    }
}
