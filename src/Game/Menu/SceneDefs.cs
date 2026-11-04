// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Menu;

/// <summary>What a <see cref="LayerDef"/> draws.</summary>
public enum LayerKind
{
    /// <summary>A vertical colour gradient band (sky, haze).</summary>
    Gradient,
    /// <summary>A soft radial light (sun, furnace glow).</summary>
    Glow,
    /// <summary>A procedural silhouette strip that tiles sideways (ruined towers, smelter stacks, ridges, pylons).</summary>
    Skyline,
    /// <summary>The ground: a gradient with an uneven top edge.</summary>
    Ground,
    /// <summary>Real sprites from data/sprites placed in the scene.</summary>
    Sprites,
    /// <summary>Drifting ash, embers or snow.</summary>
    Particles,
}

/// <summary>The procedural shape a <see cref="LayerKind.Skyline"/> layer draws.</summary>
public enum SkylineStyle
{
    /// <summary>Broken tower blocks with lit windows and aerials.</summary>
    Ruins,
    /// <summary>Low smelter halls with tall chimneys and furnace glow.</summary>
    Stacks,
    /// <summary>Rolling hills.</summary>
    Ridge,
    /// <summary>Sparse tall pylons with cross-arms (close foreground).</summary>
    Pylons,
}

/// <summary>What a particle looks like and how it moves.</summary>
public enum ParticleKind
{
    /// <summary>Grey flakes that sink and sway.</summary>
    Ash,
    /// <summary>Glowing sparks that rise and flicker.</summary>
    Ember,
    /// <summary>White flakes (the Rust Belt winter).</summary>
    Snow,
}

/// <summary>One sprite placed in a scene (from a <c>[[scene.layer.item]]</c> table).</summary>
public sealed record SpriteItem
{
    /// <summary>Sprite id (the file stem under data/sprites). May not exist yet; then <see cref="FallbackWidth"/> by <see cref="FallbackHeight"/> silhouette is drawn.</summary>
    public string Sprite { get; init; } = "";

    /// <summary>Position on the 1920x1080 canvas. For <see cref="Anchor"/> "origin" this is the sprite's ground point, for "bottom" the middle of the frame's bottom edge.</summary>
    public float X { get; init; }

    /// <summary>See <see cref="X"/>.</summary>
    public float Y { get; init; }

    /// <summary>Draw scale on the canvas.</summary>
    public float Scale { get; init; } = 2f;

    /// <summary>Facing 0..255 (0 north, clockwise). 64 faces east.</summary>
    public byte Facing { get; init; } = 64;

    /// <summary>Sprite state name.</summary>
    public string State { get; init; } = "idle";

    /// <summary>"origin" (use the manifest's origin pixel) or "bottom" (centre of the frame's bottom edge; for buildings).</summary>
    public string Anchor { get; init; } = "origin";

    /// <summary>House colour index 0..7 (see PlayerColours), applied to the paint pixels.</summary>
    public int House { get; init; }

    /// <summary>Sideways speed in canvas pixels per second (a convoy rolling by). 0 = parked.</summary>
    public float Speed { get; init; }

    /// <summary>Canvas distance after which a moving item wraps round.</summary>
    public float LoopWidth { get; init; } = 3200f;

    /// <summary>Animation ticks per second (walking, smoke). 0 = frame 0.</summary>
    public float TicksPerSecond { get; init; }

    /// <summary>Opacity 0..1.</summary>
    public float Alpha { get; init; } = 1f;

    /// <summary>Darkens the item towards black (0 = as drawn, 1 = black). Use it for distance and dusk; a haze gradient layer above also works.</summary>
    public float Shade { get; init; }

    /// <summary>Whether to draw a soft shadow under the item.</summary>
    public bool Shadow { get; init; } = true;

    /// <summary>Width of the silhouette used when the sprite id does not exist.</summary>
    public float FallbackWidth { get; init; } = 120f;

    /// <summary>Height of the silhouette used when the sprite id does not exist.</summary>
    public float FallbackHeight { get; init; } = 70f;

    /// <summary>Colour of the silhouette stand-in (<c>#RRGGBB</c>), used before the real art exists.</summary>
    public Rgba FallbackColour { get; init; } = new(0x22, 0x25, 0x22);

    /// <summary>Shape of the stand-in: "block" (a vehicle or building) or "blimp" (a soft dark ellipse, for airships).</summary>
    public string FallbackShape { get; init; } = "block";

    /// <summary>Set by the loader: true when the sprite id is known to the sprite library.</summary>
    public bool Resolved { get; init; }
}

/// <summary>One layer of a scene. Which fields matter depends on <see cref="Kind"/>; the rest keep their defaults.</summary>
public sealed record LayerDef
{
    /// <summary>What it draws.</summary>
    public LayerKind Kind { get; init; }

    /// <summary>Parallax factor: 0 = fixed sky, 1 = the ground plane, above 1 = close foreground.</summary>
    public float Parallax { get; init; }

    /// <summary>Top (or only) colour.</summary>
    public Rgba Color { get; init; } = new(20, 20, 20);

    /// <summary>Bottom colour of a gradient or ground.</summary>
    public Rgba Color2 { get; init; } = new(10, 10, 10);

    /// <summary>Highlight colour: the lit edge of silhouettes, the core of a glow.</summary>
    public Rgba Rim { get; init; } = new(0, 0, 0, 0);

    /// <summary>Lit-window or furnace colour of a skyline.</summary>
    public Rgba Lit { get; init; } = new(0, 0, 0, 0);

    /// <summary>Gradient: top edge on the canvas.</summary>
    public float Y0 { get; init; }

    /// <summary>Gradient: bottom edge on the canvas.</summary>
    public float Y1 { get; init; } = ParallaxMath.RefHeight;

    /// <summary>Glow centre x. Skyline/ground: unused.</summary>
    public float X { get; init; }

    /// <summary>Glow centre y; skyline/ground: the y of the base line.</summary>
    public float Y { get; init; }

    /// <summary>Glow radius.</summary>
    public float Radius { get; init; } = 300f;

    /// <summary>Glow strength 0..1+.</summary>
    public float Intensity { get; init; } = 1f;

    /// <summary>Glow flicker amplitude (0..1) and rate in Hz.</summary>
    public float Flicker { get; init; }

    /// <summary>See <see cref="Flicker"/>.</summary>
    public float FlickerHz { get; init; } = 0.7f;

    /// <summary>Whether a glow adds light (true) or darkens/dims by normal blending (false).</summary>
    public bool Additive { get; init; } = true;

    /// <summary>Skyline shape.</summary>
    public SkylineStyle Style { get; init; } = SkylineStyle.Ruins;

    /// <summary>Seed for the procedural shapes and particles.</summary>
    public int Seed { get; init; } = 1;

    /// <summary>Skyline: shortest feature height.</summary>
    public float MinHeight { get; init; } = 80f;

    /// <summary>Skyline: tallest feature height.</summary>
    public float MaxHeight { get; init; } = 320f;

    /// <summary>Skyline: width after which the strip repeats; ground: unused.</summary>
    public float Period { get; init; } = 2400f;

    /// <summary>Ground: height of the uneven top edge.</summary>
    public float Roughness { get; init; } = 14f;

    /// <summary>Particles: what they are.</summary>
    public ParticleKind Particle { get; init; } = ParticleKind.Ash;

    /// <summary>Particles: how many in the full canvas.</summary>
    public int Count { get; init; } = 100;

    /// <summary>Particles: velocity on the canvas in pixels per second.</summary>
    public float VelX { get; init; } = -30f;

    /// <summary>See <see cref="VelX"/>.</summary>
    public float VelY { get; init; } = 40f;

    /// <summary>Particles: smallest and largest size in canvas pixels.</summary>
    public float SizeMin { get; init; } = 1.5f;

    /// <summary>See <see cref="SizeMin"/>.</summary>
    public float SizeMax { get; init; } = 4f;

    /// <summary>Particles: sideways sway in canvas pixels.</summary>
    public float Sway { get; init; } = 18f;

    /// <summary>Sprites: the placed items.</summary>
    public IReadOnlyList<SpriteItem> Items { get; init; } = Array.Empty<SpriteItem>();
}

/// <summary>One illustrated scene: sky to foreground layers, a slow camera drift and a colour grade.</summary>
public sealed record SceneDef
{
    /// <summary>Short id, e.g. <c>authority_convoy</c>.</summary>
    public string Id { get; init; } = "";

    /// <summary>Caption shown in the corner.</summary>
    public string Caption { get; init; } = "";

    /// <summary>Seconds on screen, including the cross-fade out.</summary>
    public float Duration { get; init; } = 25f;

    /// <summary>Camera drift amplitude on the canvas (x then y) in pixels.</summary>
    public float DriftX { get; init; } = 90f;

    /// <summary>See <see cref="DriftX"/>.</summary>
    public float DriftY { get; init; } = 8f;

    /// <summary>Seconds for one full camera swing.</summary>
    public float DriftPeriod { get; init; } = 60f;

    /// <summary>Colour grade laid over the whole scene (alpha = strength).</summary>
    public Rgba Grade { get; init; } = new(0, 0, 0, 0);

    /// <summary>Layers back to front.</summary>
    public IReadOnlyList<LayerDef> Layers { get; init; } = Array.Empty<LayerDef>();
}
