// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game;
using Rts.Game.Veterancy;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// The short gold box drawn around a unit for the length of its promotion flash (VET-5). Purely visual: the tracker decides who is
/// flashing, this only draws them.
/// </summary>
public static class PromotionFlash
{
    private const int HalfPx = 16;
    private const int ThicknessPx = 2;
    private static readonly Color Gold = new(0xFF, 0xE0, 0x7A);

    /// <summary>Draws a box around each unit in <paramref name="units"/> whose promotion flash is running at <paramref name="tick"/>.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, IReadOnlyList<RenderUnit> units, Camera camera, PromotionTracker tracker, int tick)
    {
        foreach (RenderUnit unit in units)
        {
            var key = new RankKey(unit.View.Kind, unit.View.Id);
            if (!tracker.IsFlashing(key, tick)) continue;
            Vector2 centre = SpriteLayout.UnitOrigin(camera, unit.World);
            float zoom = camera.Zoom;
            int half = (int)MathF.Round(HalfPx * zoom);
            var box = new Rectangle((int)centre.X - half, (int)centre.Y - half, half * 2, half * 2);
            Shapes.Outline(batch, pixel, box, Gold, Math.Max(1, (int)MathF.Round(ThicknessPx * zoom)));
        }
    }
}
