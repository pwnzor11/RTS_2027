// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>How far a sprite's pixels can reach from its anchor, so the sprite pass can skip what is wholly off screen.</summary>
public interface ISpriteExtents
{
    /// <summary>Zoom-1 world pixels from a unit's ground point to the farthest pixel it (shadow, turret, brackets, markers) can draw.</summary>
    float UnitReach(UnitView unit);

    /// <summary>Zoom-1 world pixels beyond a building's footprint rectangle that its sprite, anchors and markers can draw.</summary>
    float BuildingReach(BuildingView building);
}

/// <summary>
/// View culling for the y-sorted sprite pass (A43 FOLLOWUP-12): which buildings and units can touch the screen. Reaches are generous
/// (the frame's longest side plus a margin for tall sprites, shadows and turrets), so an on-screen item is never dropped and the
/// picture is the same as drawing everything; only items wholly outside the view are skipped. Pure, so it is tested without a GPU.
/// </summary>
public static class SpriteCull
{
    /// <summary>Extra zoom-1 world pixels added to every sprite's reach (selection brackets, tunnel and dive markers, rounding).</summary>
    public const float MarginPx = 64f;

    /// <summary>
    /// Fills <paramref name="items"/> (appended, not cleared) with a draw item for every building and unit that can be seen.
    /// With <paramref name="extents"/> null or an empty viewport nothing is culled.
    /// </summary>
    public static void Collect(List<DrawItem> items, IReadOnlyList<BuildingView> buildings, IReadOnlyList<RenderUnit> units,
        ISpriteExtents? extents, Camera camera, Viewport viewport)
    {
        bool cull = extents is not null && viewport.Width > 0 && viewport.Height > 0;
        float left = camera.Position.X, top = camera.Position.Y;
        float right = left + viewport.Width / camera.Zoom, bottom = top + viewport.Height / camera.Zoom;
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingView b = buildings[i];
            float bl = b.Cell.X * Camera.CellPx, bt = b.Cell.Y * Camera.CellPx;
            float br = bl + b.FootprintW * Camera.CellPx;
            float bb = bt + b.FootprintH * Camera.CellPx;
            if (cull)
            {
                float reach = extents!.BuildingReach(b);
                if (br + reach < left || bl - reach > right || bb + reach < top || bt - reach > bottom) continue;
            }
            items.Add(new DrawItem(DrawKind.Building, i, bb, bl));
        }

        for (int i = 0; i < units.Count; i++)
        {
            RenderUnit unit = units[i];
            if (cull)
            {
                float reach = extents!.UnitReach(unit.View);
                Vector2 w = unit.World;
                if (w.X + reach < left || w.X - reach > right || w.Y + reach < top || w.Y - reach > bottom) continue;
            }
            items.Add(new DrawItem(DrawKind.Unit, i, unit.World.Y, unit.World.X));
        }
    }
}
