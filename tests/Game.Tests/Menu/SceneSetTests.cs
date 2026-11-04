// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Menu;
using Rts.Game.Sprites;

namespace Rts.Game.Tests.Menu;

public sealed class SceneSetTests
{
    internal static string DataDir => Path.GetDirectoryName(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!)!;

    private static SpriteLibrary Library() => SpriteLibrary.Scan(Path.Combine(DataDir, "sprites"));

    private static SceneSet Shipped(SpriteLibrary lib) =>
        SceneSet.Load(Path.Combine(DataDir, "menu", "scenes.toml"), id => lib.Ids.Contains(id));

    [Fact]
    public void The_shipped_scenes_load_without_warnings_other_than_missing_art()
    {
        using SpriteLibrary lib = Library();
        SceneSet set = Shipped(lib);

        Assert.InRange(set.Scenes.Count, 3, 4);
        Assert.DoesNotContain(set.Scenes, s => s.Id == "fallback");
        Assert.All(set.Warnings, w => Assert.Contains("not found; drawn as a silhouette", w));
    }

    [Fact]
    public void Scenes_stay_20_to_30_seconds_and_have_distinct_ids_and_captions()
    {
        using SpriteLibrary lib = Library();
        SceneSet set = Shipped(lib);

        Assert.All(set.Scenes, s => Assert.InRange(s.Duration, 20f, 30f));
        Assert.Equal(set.Scenes.Count, set.Scenes.Select(s => s.Id).Distinct().Count());
        Assert.All(set.Scenes, s => Assert.NotEqual("", s.Caption));
        Assert.All(set.Scenes, s => Assert.True(s.Layers.Count >= 8, s.Id));
    }

    [Fact]
    public void Every_sprite_a_scene_names_either_resolves_to_a_real_sprite_or_falls_back()
    {
        using SpriteLibrary lib = Library();
        SceneSet set = Shipped(lib);

        var refs = set.SpriteRefs().ToList();
        Assert.NotEmpty(refs);
        foreach ((string scene, string sprite, bool resolved) in refs)
        {
            // Resolved must say exactly what the library says; nothing is left half-resolved.
            Assert.Equal(lib.Ids.Contains(sprite), resolved);
            if (resolved) Assert.NotNull(lib.Manifest(sprite));
            else Assert.Contains(set.Warnings, w => w.Contains(sprite) && w.Contains(scene));
        }
    }

    [Fact]
    public void The_authority_scenes_use_real_sprites_and_the_directorate_scene_is_allowed_to_fall_back()
    {
        using SpriteLibrary lib = Library();
        SceneSet set = Shipped(lib);

        foreach (var (scene, sprite, resolved) in set.SpriteRefs())
        {
            if (sprite.StartsWith("auth_", StringComparison.Ordinal) || sprite.StartsWith("fire_", StringComparison.Ordinal) || sprite.StartsWith("smoke_", StringComparison.Ordinal))
                Assert.True(resolved, $"{scene}: {sprite}");
        }
        Assert.Contains(set.SpriteRefs(), r => r.Scene == "authority_convoy" && r.Sprite == "auth_tank" && r.Resolved);
    }

    [Fact]
    public void Every_real_sprite_item_names_a_state_the_sprite_has()
    {
        using SpriteLibrary lib = Library();
        SceneSet set = Shipped(lib);

        foreach (SceneDef scene in set.Scenes)
        foreach (SpriteItem item in scene.Layers.SelectMany(l => l.Items).Where(i => i.Resolved))
        {
            SpriteManifest m = lib.Manifest(item.Sprite);
            Assert.True(m.States.Any(s => s.Name == item.State), $"{scene.Id}: {item.Sprite} has no state '{item.State}'");
        }
    }

    [Fact]
    public void An_unknown_sprite_id_keeps_its_place_and_is_marked_unresolved()
    {
        const string toml = """
            [[scene]]
            id = "t"
            [[scene.layer]]
            kind = "sprites"
              [[scene.layer.item]]
              sprite = "dir_nothing_yet"
              x = 10
              y = 20
              fallback_w = 200
            """;

        SceneSet set = SceneSet.Parse(toml, _ => false);

        SpriteItem item = Assert.Single(set.Scenes.Single().Layers.Single().Items);
        Assert.Equal("dir_nothing_yet", item.Sprite);
        Assert.False(item.Resolved);
        Assert.Equal(200f, item.FallbackWidth);
        Assert.Single(set.Warnings);
    }

    [Fact]
    public void Bad_files_and_bad_layers_never_throw_and_always_leave_a_scene()
    {
        SceneSet junk = SceneSet.Parse("this is = = not toml [[[", _ => true);
        Assert.Equal("fallback", junk.Scenes.Single().Id);
        Assert.NotEmpty(junk.Warnings);

        SceneSet empty = SceneSet.Parse("", _ => true);
        Assert.Equal("fallback", empty.Scenes.Single().Id);

        SceneSet badKind = SceneSet.Parse("""
            [[scene]]
            id = "a"
            [[scene.layer]]
            kind = "teleporter"
            [[scene.layer]]
            kind = "gradient"
            color = "not a colour"
            """, _ => true);
        Assert.Equal("a", badKind.Scenes.Single().Id);
        Assert.Single(badKind.Scenes.Single().Layers);
        Assert.Equal(2, badKind.Warnings.Count);

        Assert.Equal("fallback", SceneSet.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".toml"), _ => true).Scenes.Single().Id);
    }

    [Fact]
    public void Colours_parse_with_and_without_alpha()
    {
        Assert.True(Rgba.TryParse("#FFB064", out Rgba a));
        Assert.Equal(new Rgba(0xFF, 0xB0, 0x64, 255), a);
        Assert.True(Rgba.TryParse("9A8E7870", out Rgba b));
        Assert.Equal(new Rgba(0x9A, 0x8E, 0x78, 0x70), b);
        Assert.False(Rgba.TryParse("#12345", out _));
        Assert.False(Rgba.TryParse("nope", out _));
    }

    [Fact]
    public void Composing_a_scene_is_deterministic_and_the_camera_drifts_slowly()
    {
        using SpriteLibrary lib = Library();
        SceneSet set = Shipped(lib);
        SpriteManifest? Manifest(string id) => lib.Ids.Contains(id) ? lib.Manifest(id) : null;

        var first = new List<DrawOp>();
        var second = new List<DrawOp>();
        new SceneComposer(Manifest).Compose(set.Scenes[0], 12.5f, 1280, 720, 1f, first);
        new SceneComposer(Manifest).Compose(set.Scenes[0], 12.5f, 1280, 720, 1f, second);
        Assert.Equal(first, second);
        Assert.InRange(first.Count, 200, 6000);

        // Slow: the camera moves a few pixels per second, never more than its amplitude.
        (float x0, _) = SceneComposer.Camera(set.Scenes[0], 10f);
        (float x1, _) = SceneComposer.Camera(set.Scenes[0], 11f);
        Assert.True(MathF.Abs(x1 - x0) < 12f);
        Assert.All(Enumerable.Range(0, 200), i => Assert.True(MathF.Abs(SceneComposer.Camera(set.Scenes[0], i).X) <= set.Scenes[0].DriftX + 0.01f));
    }

    [Fact]
    public void Cross_fade_draws_both_scenes_and_a_single_scene_draws_one()
    {
        using SpriteLibrary lib = Library();
        SceneSet set = Shipped(lib);
        var composer = new SceneComposer(id => lib.Ids.Contains(id) ? lib.Manifest(id) : null);

        var held = new List<DrawOp>();
        MenuBackground.BuildFrame(set, composer, 5f, 1280, 720, held);
        var fading = new List<DrawOp>();
        MenuBackground.BuildFrame(set, composer, set.Scenes[0].Duration - set.FadeSeconds / 2f, 1280, 720, fading);

        Assert.True(fading.Count > held.Count);
        Assert.Equal(DrawOpKind.Vignette, held[^2].Kind);
        Assert.Equal(DrawOpKind.Grain, held[^1].Kind);
    }

    [Fact]
    public void The_standard_house_ramp_matches_the_real_sprite_ramps_and_covers_sheets_that_declare_none()
    {
        using SpriteLibrary lib = Library();

        Assert.Equal(lib.Manifest("auth_tank").HouseRamp.ToArray(), HouseRamp.Standard);
        Assert.Equal(lib.Manifest("auth_rifleman").HouseRamp.ToArray(), HouseRamp.Standard);
        Assert.Equal(HouseRamp.Standard, HouseRamp.For(lib.Manifest("auth_refinery")));   // buildings declare no ramp
        Assert.Equal(HouseRamp.Standard, HouseRamp.For(lib.Manifest("auth_tank")));
    }

    [Fact]
    public void A_plain_grade_colour_gets_a_light_wash_not_a_cover_up()
    {
        SceneSet set = SceneSet.Parse("""
            [[scene]]
            id = "g"
            grade = "#FF0000"
            [[scene.layer]]
            kind = "gradient"
            """, _ => true);

        Assert.InRange(set.Scenes.Single().Grade.A, 1, 100);
    }
}
