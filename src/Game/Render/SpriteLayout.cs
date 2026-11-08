// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Sprites;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// Where a sprite's origin pixel goes on screen. Pure camera maths, shared by the renderers, the health bars and the
/// CPU proof, so all of them place a sprite the same way.
/// </summary>
public static class SpriteLayout
{
    /// <summary>
    /// Screen position of a building's origin: the top-left of its footprint, whole pixels. A manifest origin of
    /// (0, 32) therefore puts the frame's footprint top-left on the cell's top-left corner, 32 px below the frame top.
    /// </summary>
    public static Vector2 BuildingOrigin(Camera camera, CellCoord cell)
    {
        Vector2 world = new(cell.X * Camera.CellPx, cell.Y * Camera.CellPx);
        return Round(camera.ScreenFromWorld(world));
    }

    /// <summary>
    /// D-172 (3), M11-06: units (infantry, vehicles, aircraft, ships) are drawn this much larger than their art, around
    /// the origin pixel (ground contact). Render only: sim footprints, ranges and pathing are unchanged, and the default
    /// camera zoom is unchanged. Buildings and terrain do not use it.
    /// </summary>
    public const float UnitDrawScale = 1.25f;

    /// <summary>Total sprite scale of a unit: its type's own draw scale (big ships) times <see cref="UnitDrawScale"/>, then the camera zoom.</summary>
    /// <param name="typeDrawScale">The type's own scale (<c>draw_scale_permille</c> / 1000; 1 for most).</param>
    /// <param name="zoom">Camera zoom.</param>
    public static float UnitSpriteScale(float typeDrawScale, float zoom) => typeDrawScale * UnitDrawScale * zoom;

    /// <summary>
    /// Screen rectangle a unit frame covers when drawn at <paramref name="origin"/> with <paramref name="scale"/>
    /// (a value from <see cref="UnitSpriteScale"/>): the frame is scaled about its origin pixel, so the ground contact
    /// point does not move.
    /// </summary>
    public static (Vector2 TopLeft, Vector2 Size) UnitFrameRect(Vector2 origin, SpriteManifest manifest, float scale) =>
        (origin - new Vector2(manifest.OriginX, manifest.OriginY) * scale, new Vector2(manifest.FrameW, manifest.FrameH) * scale);

    /// <summary>Screen position of a unit's origin: its ground contact point, whole pixels.</summary>
    /// <param name="camera">View.</param>
    /// <param name="world">Interpolated zoom-1 world position of the unit.</param>
    public static Vector2 UnitOrigin(Camera camera, Vector2 world) => Round(camera.ScreenFromWorld(world));

    /// <summary>
    /// Screen position of the top-left of a frame when its origin pixel sits at <paramref name="origin"/>. Useful to
    /// place things above a sprite (health bars).
    /// </summary>
    /// <param name="origin">Screen position of the origin pixel.</param>
    /// <param name="manifest">Manifest with the origin inside the frame.</param>
    /// <param name="zoom">Camera zoom.</param>
    public static Vector2 FrameTopLeft(Vector2 origin, SpriteManifest manifest, float zoom) =>
        origin - new Vector2(manifest.OriginX, manifest.OriginY) * zoom;

    private static Vector2 Round(Vector2 v) => new(MathF.Round(v.X), MathF.Round(v.Y));
}
