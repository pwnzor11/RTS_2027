// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sprites;

namespace Rts.Game.Menu;

/// <summary>
/// The animated menu background: loads the scenes from <c>data/menu/scenes.toml</c>, keeps the clock, and each frame composes the
/// current scene (and the next one while cross-fading) with a vignette and film grain over it. Menu screens draw it under
/// their own panels; it has no effect on the simulation.
/// </summary>
public sealed class MenuBackground : IDisposable
{
    /// <summary>Strength of the dark vignette (0..1).</summary>
    // ASSUMED (M3-16): vignette 0.72 and grain 0.05 look right on the proof renders; tune here.
    public const float VignetteStrength = 0.72f;

    /// <summary>Strength of the film grain (0..1).</summary>
    public const float GrainStrength = 0.05f;

    /// <summary>Grain frames per second (the grain crawls at film rate).</summary>
    public const int GrainFps = 12;

    private readonly SceneComposer _composer;
    private readonly DrawOpRenderer _renderer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<DrawOp> _ops = new(2048);

    /// <summary>Loads the scenes and builds the renderer.</summary>
    /// <param name="device">Graphics device.</param>
    /// <param name="pixel">1x1 white texture.</param>
    /// <param name="sprites">The loaded sprite library, or null (then every sprite is a silhouette).</param>
    /// <param name="dataDir">The data folder (holding <c>menu/scenes.toml</c>).</param>
    public MenuBackground(GraphicsDevice device, Texture2D pixel, SpriteLibrary? sprites, string dataDir)
    {
        Func<string, SpriteManifest?> manifestOf = id => TryManifest(sprites, id);
        Scenes = SceneSet.Load(Path.Combine(dataDir, "menu", "scenes.toml"), id => manifestOf(id) is not null);
        foreach (string w in Scenes.Warnings) Console.Error.WriteLine($"menu scenes: {w}");
        _composer = new SceneComposer(manifestOf);
        _renderer = new DrawOpRenderer(device, pixel, sprites);
    }

    /// <summary>The loaded scenes.</summary>
    public SceneSet Scenes { get; }

    /// <summary>Seconds since the menu opened.</summary>
    public float Seconds => (float)_clock.Elapsed.TotalSeconds;

    /// <summary>Caption of the scene that is mostly on screen now.</summary>
    public string Caption
    {
        get
        {
            SceneCursor c = Cursor();
            return Scenes.Scenes[c.Mix > 0.5f ? c.Next : c.Current].Caption;
        }
    }

    /// <summary>Draws the background filling the window. The batch is begun and is begun again afterwards.</summary>
    public void Draw(SpriteBatch sb, Viewport viewport)
    {
        _ops.Clear();
        BuildFrame(Scenes, _composer, Seconds, viewport.Width, viewport.Height, _ops);
        _renderer.Draw(sb, _ops, viewport);
    }

    /// <summary>Draws arbitrary ops (the lobby's own panel) with this background's renderer.</summary>
    public void DrawOps(SpriteBatch sb, IReadOnlyList<DrawOp> ops, Viewport viewport) => _renderer.Draw(sb, ops, viewport);

    /// <summary>
    /// The ops for one frame at <paramref name="seconds"/>: the scene (cross-fading into the next at the end of its time), then the
    /// vignette and grain. Pure, so tests and the proof renderer use it directly.
    /// </summary>
    public static void BuildFrame(SceneSet scenes, SceneComposer composer, float seconds, float viewW, float viewH, List<DrawOp> ops)
    {
        // ASSUMED (M3-16): the menu always opens on the first scene in scenes.toml and plays them in file order.
        SceneCursor c = SceneTimeline.Locate(scenes.Scenes.Select(s => s.Duration).ToArray(), scenes.FadeSeconds, seconds);
        composer.Compose(scenes.Scenes[c.Current], c.LocalTime, viewW, viewH, 1f, ops);
        if (c.Mix > 0f) composer.Compose(scenes.Scenes[c.Next], c.NextLocalTime, viewW, viewH, c.Mix, ops);
        ops.Add(DrawOp.Vignette(VignetteStrength));
        ops.Add(DrawOp.Grain(GrainStrength, (int)(seconds * GrainFps)));
    }

    /// <inheritdoc />
    public void Dispose() => _renderer.Dispose();

    private SceneCursor Cursor() => SceneTimeline.Locate(Scenes.Scenes.Select(s => s.Duration).ToArray(), Scenes.FadeSeconds, Seconds);

    private static SpriteManifest? TryManifest(SpriteLibrary? lib, string id)
    {
        if (lib is null) return null;
        try { return lib.Manifest(id); }
        catch (KeyNotFoundException) { return null; }
    }
}
