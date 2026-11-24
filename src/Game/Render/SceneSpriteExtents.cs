// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>The scene renderer's <see cref="ISpriteExtents"/>: reads the resolved sprite's manifest sizes. Allocation-free (the resolver caches).</summary>
public sealed class SceneSpriteExtents : ISpriteExtents
{
    private readonly SpriteResolver _sprites;

    /// <summary>Rules of the frame being drawn, or null; set by the renderer each frame.</summary>
    public SimRules? Rules { get; set; }

    /// <summary>Creates extents over <paramref name="sprites"/>.</summary>
    public SceneSpriteExtents(SpriteResolver sprites)
    {
        _sprites = sprites;
    }

    /// <inheritdoc />
    public float UnitReach(UnitView unit)
    {
        ResolvedSprite sprite = _sprites.ForUnit(Rules, unit);
        float frame = Longest(sprite.Manifest);
        if (sprite.TurretSheet is { } turret) frame = MathF.Max(frame, Longest(turret.Manifest));
        if (sprite.ShadowSheet is { } shadow) frame = MathF.Max(frame, Longest(shadow.Manifest));
        float scale = UnitRenderer.DrawScale(Rules, unit, sprite.Manifest) * SpriteLayout.UnitDrawScale;
        return (frame + SpriteCull.MarginPx) * MathF.Max(scale, SpriteLayout.UnitDrawScale);
    }

    /// <inheritdoc />
    public float BuildingReach(BuildingView building)
    {
        ResolvedSprite sprite = _sprites.ForBuilding(Rules, building);
        return Longest(sprite.Manifest) + SpriteCull.MarginPx;
    }

    private static float Longest(SpriteManifest m) => Math.Max(m.FrameW, m.FrameH) * MathF.Max(1f, m.ArtScale);
}
