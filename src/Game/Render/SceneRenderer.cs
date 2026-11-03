// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sprites;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// Draws one frame in order: terrain, occupancy (F3), buildings, units, projectiles, fog, path (F4) and range (F5)
/// overlays, health bars, then the drag box.
/// </summary>
public sealed class SceneRenderer
{
    private static readonly Color DragColour = Color.White;

    private readonly UnitRenderer _units;

    /// <summary>Creates a renderer that draws vehicles with the given sprite sheet.</summary>
    public SceneRenderer(SpriteSheet sheet)
    {
        _units = new UnitRenderer(sheet);
    }

    /// <summary>Draws the frame. <paramref name="pixel"/> is a 1x1 white texture; the batch is already begun.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera, Viewport viewport)
    {
        SimSnapshot snapshot = frame.Snapshot;
        TerrainRenderer.Draw(batch, pixel, snapshot, camera, viewport);
        if (frame.Overlays.Occupancy && frame.Sim is not null)
        {
            OccupancyOverlay.Draw(batch, pixel, frame.Sim, snapshot, camera, viewport);
        }

        BuildingRenderer.Draw(batch, pixel, snapshot, camera);
        _units.Draw(batch, pixel, frame, camera);
        ProjectileRenderer.Draw(batch, pixel, frame.Projectiles, camera);
        FogRenderer.Draw(batch, pixel, snapshot, camera, viewport);

        if (frame.Overlays.Paths) PathOverlay.Draw(batch, pixel, frame, camera);
        if (frame.Overlays.Ranges) RangeOverlay.Draw(batch, pixel, frame, camera);
        HealthBars.Draw(batch, pixel, frame, camera);

        if (frame.DragRect is Rectangle box) Shapes.Outline(batch, pixel, box, DragColour);
    }
}
