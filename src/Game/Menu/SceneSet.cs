// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Menu;

/// <summary>
/// The scenes loaded from <c>data/menu/scenes.toml</c>, plus anything the loader had to repair. Loading never throws: a
/// missing or broken file, or a scene with no usable layers, gives <see cref="Fallback"/> and a warning, so the menu always has
/// a background. A sprite id that is not in the sprite library is not an error: the item keeps its place and draws a dark
/// silhouette (<see cref="SpriteItem.Resolved"/> is false) until the art arrives.
/// </summary>
public sealed class SceneSet
{
    /// <summary>Default cross-fade length in seconds.</summary>
    public const float DefaultFade = 3f;

    private SceneSet(IReadOnlyList<SceneDef> scenes, float fade, IReadOnlyList<string> warnings)
    {
        Scenes = scenes;
        FadeSeconds = fade;
        Warnings = warnings;
    }

    /// <summary>The scenes in play order.</summary>
    public IReadOnlyList<SceneDef> Scenes { get; }

    /// <summary>Cross-fade length in seconds.</summary>
    public float FadeSeconds { get; }

    /// <summary>Problems found while loading (unknown layer kinds, bad colours, missing sprites).</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Every sprite id the scenes name, in first-use order, with whether the library knew it.</summary>
    public IEnumerable<(string Scene, string Sprite, bool Resolved)> SpriteRefs() =>
        Scenes.SelectMany(s => s.Layers.SelectMany(l => l.Items.Select(i => (s.Id, i.Sprite, i.Resolved))));

    /// <summary>A plain dusk gradient with a few ash flakes, used when nothing else loads.</summary>
    public static SceneSet Fallback(IReadOnlyList<string>? warnings = null) => new(
        new[]
        {
            new SceneDef
            {
                Id = "fallback",
                Caption = "",
                Duration = 30f,
                Layers = new[]
                {
                    new LayerDef { Kind = LayerKind.Gradient, Color = new Rgba(0x2A, 0x2E, 0x2A), Color2 = new Rgba(0x0B, 0x0C, 0x0A) },
                    new LayerDef { Kind = LayerKind.Particles, Parallax = 0.6f, Particle = ParticleKind.Ash, Color = new Rgba(0xB0, 0xB0, 0xA4, 150) },
                },
            },
        },
        DefaultFade, warnings ?? Array.Empty<string>());

    /// <summary>Reads <paramref name="path"/>; never throws.</summary>
    /// <param name="path">The scenes.toml file.</param>
    /// <param name="spriteExists">True when a sprite id exists in the library.</param>
    public static SceneSet Load(string path, Func<string, bool> spriteExists)
    {
        try
        {
            return File.Exists(path)
                ? Parse(File.ReadAllText(path), spriteExists)
                : Fallback(new[] { $"{path} not found; using the plain background" });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Fallback(new[] { $"{path} unreadable: {e.Message}" });
        }
    }

    /// <summary>Parses scenes.toml text; never throws.</summary>
    public static SceneSet Parse(string text, Func<string, bool> spriteExists)
    {
        var warnings = new List<string>();
        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
        }
        catch (Exception e)
        {
            return Fallback(new[] { $"scenes.toml is not valid TOML: {e.Message}" });
        }

        float fade = DefaultFade;
        if (root.TryGetValue("menu", out object? menu) && menu is TomlTable mt)
            fade = Math.Clamp(Num(mt, "fade_seconds", DefaultFade), 0.5f, 10f);

        var scenes = new List<SceneDef>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (TomlTable st in Tables(root, "scene"))
        {
            string id = Str(st, "id", "");
            if (id.Length == 0) { warnings.Add("a [[scene]] has no id; skipped"); continue; }
            if (!ids.Add(id)) { warnings.Add($"scene '{id}' is defined twice; the second is skipped"); continue; }

            var layers = new List<LayerDef>();
            foreach (TomlTable lt in Tables(st, "layer"))
            {
                LayerDef? layer = ParseLayer(lt, id, spriteExists, warnings);
                if (layer is not null) layers.Add(layer);
            }
            if (layers.Count == 0) { warnings.Add($"scene '{id}' has no usable layers; skipped"); continue; }

            scenes.Add(new SceneDef
            {
                Id = id,
                Caption = Str(st, "caption", ""),
                Duration = Math.Clamp(Num(st, "duration", 25f), 4f, 120f),
                DriftX = Num(st, "drift_x", 90f),
                DriftY = Num(st, "drift_y", 8f),
                DriftPeriod = Math.Clamp(Num(st, "drift_period", 60f), 5f, 600f),
                Grade = Wash(Colour(st, "grade", new Rgba(0, 0, 0, 0), warnings, id)),
                Layers = layers,
            });
        }

        return scenes.Count == 0
            ? Fallback(warnings.Append("no usable scenes in scenes.toml").ToList())
            : new SceneSet(scenes, fade, warnings);
    }

    private static LayerDef? ParseLayer(TomlTable t, string scene, Func<string, bool> spriteExists, List<string> warnings)
    {
        string kindText = Str(t, "kind", "");
        if (!Enum.TryParse(kindText, ignoreCase: true, out LayerKind kind) || !Enum.IsDefined(kind))
        {
            warnings.Add($"scene '{scene}': unknown layer kind '{kindText}'; layer skipped");
            return null;
        }

        var items = new List<SpriteItem>();
        if (kind == LayerKind.Sprites)
        {
            foreach (TomlTable it in Tables(t, "item"))
            {
                string sprite = Str(it, "sprite", "");
                if (sprite.Length == 0) { warnings.Add($"scene '{scene}': an item has no sprite; skipped"); continue; }
                bool ok = spriteExists(sprite);
                if (!ok) warnings.Add($"scene '{scene}': sprite '{sprite}' not found; drawn as a silhouette");
                items.Add(new SpriteItem
                {
                    Sprite = sprite,
                    X = Num(it, "x", 0f),
                    Y = Num(it, "y", 0f),
                    Scale = Math.Clamp(Num(it, "scale", 2f), 0.1f, 12f),
                    Facing = (byte)Math.Clamp((int)Num(it, "facing", 64f), 0, 255),
                    State = Str(it, "state", "idle"),
                    Anchor = Str(it, "anchor", "origin") == "bottom" ? "bottom" : "origin",
                    House = (int)Num(it, "house", 0f),
                    Speed = Num(it, "speed", 0f),
                    LoopWidth = Math.Max(100f, Num(it, "loop_width", 3200f)),
                    TicksPerSecond = Math.Max(0f, Num(it, "ticks_per_second", 0f)),
                    Alpha = Math.Clamp(Num(it, "alpha", 1f), 0f, 1f),
                    Shade = Math.Clamp(Num(it, "shade", 0f), 0f, 1f),
                    Shadow = Bool(it, "shadow", true),
                    FallbackWidth = Math.Max(8f, Num(it, "fallback_w", 120f)),
                    FallbackHeight = Math.Max(8f, Num(it, "fallback_h", 70f)),
                    FallbackColour = Colour(it, "fallback_color", new Rgba(0x22, 0x25, 0x22), warnings, scene),
                    FallbackShape = Str(it, "fallback_shape", "block") == "blimp" ? "blimp" : "block",
                    Resolved = ok,
                });
            }
        }

        Rgba color = Colour(t, "color", new Rgba(20, 20, 20), warnings, scene);
        return new LayerDef
        {
            Kind = kind,
            Parallax = Math.Clamp(Num(t, "parallax", 0f), 0f, 4f),
            Color = color,
            Color2 = Colour(t, "color2", color, warnings, scene),
            Rim = Colour(t, "rim", new Rgba(0, 0, 0, 0), warnings, scene),
            Lit = Colour(t, "lit", new Rgba(0, 0, 0, 0), warnings, scene),
            Y0 = Num(t, "y0", 0f),
            Y1 = Num(t, "y1", ParallaxMath.RefHeight),
            X = Num(t, "x", 0f),
            Y = Num(t, "y", 0f),
            Radius = Math.Max(1f, Num(t, "radius", 300f)),
            Intensity = Math.Clamp(Num(t, "intensity", 1f), 0f, 4f),
            Flicker = Math.Clamp(Num(t, "flicker", 0f), 0f, 1f),
            FlickerHz = Math.Max(0.01f, Num(t, "flicker_hz", 0.7f)),
            Additive = Bool(t, "additive", true),
            Style = Enum.TryParse(Str(t, "style", "ruins"), true, out SkylineStyle style) ? style : SkylineStyle.Ruins,
            Seed = (int)Num(t, "seed", 1f),
            MinHeight = Num(t, "min_height", 80f),
            MaxHeight = Math.Max(Num(t, "min_height", 80f), Num(t, "max_height", 320f)),
            Period = Math.Max(400f, Num(t, "period", 2400f)),
            Roughness = Math.Max(0f, Num(t, "roughness", 14f)),
            Particle = Enum.TryParse(Str(t, "particle", "ash"), true, out ParticleKind pk) ? pk : ParticleKind.Ash,
            Count = Math.Clamp((int)Num(t, "count", 100f), 0, 600),
            VelX = Num(t, "vel_x", -30f),
            VelY = Num(t, "vel_y", 40f),
            SizeMin = Math.Max(0.5f, Num(t, "size_min", 1.5f)),
            SizeMax = Math.Max(Math.Max(0.5f, Num(t, "size_min", 1.5f)), Num(t, "size_max", 4f)),
            Sway = Num(t, "sway", 18f),
            Items = items,
        };
    }

    // A grade given as plain #RRGGBB would paint the whole scene over; treat "fully opaque" as "no strength given" and use a light wash.
    private static Rgba Wash(Rgba c) => c.A == 255 ? c with { A = 32 } : c;

    private static IEnumerable<TomlTable> Tables(TomlTable parent, string key)
    {
        if (!parent.TryGetValue(key, out object? raw) || raw is not IEnumerable<object?> list) yield break;
        foreach (object? o in list)
            if (o is TomlTable t) yield return t;
    }

    private static float Num(TomlTable t, string key, float fallback) =>
        t.TryGetValue(key, out object? v) ? v switch { long l => l, double d => (float)d, _ => fallback } : fallback;

    private static string Str(TomlTable t, string key, string fallback) =>
        t.TryGetValue(key, out object? v) && v is string s ? s : fallback;

    private static bool Bool(TomlTable t, string key, bool fallback) =>
        t.TryGetValue(key, out object? v) && v is bool b ? b : fallback;

    private static Rgba Colour(TomlTable t, string key, Rgba fallback, List<string> warnings, string scene)
    {
        if (!t.TryGetValue(key, out object? v)) return fallback;
        if (v is string s && Rgba.TryParse(s, out Rgba c)) return c;
        warnings.Add($"scene '{scene}': bad colour for '{key}'; using the default");
        return fallback;
    }
}
