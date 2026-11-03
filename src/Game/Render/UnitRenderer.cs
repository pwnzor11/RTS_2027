// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// One unit: the sprite for its rules type in its house colour (vehicles idle with 16 facings; infantry walk or idle
/// with 8 facings), plus white corner brackets when selected. Health bars are drawn separately, above the sprites.
/// </summary>
public static class UnitRenderer
{
    private const int VehicleBracketHalfPxAtZoom1 = 26;
    private const int InfantryBracketHalfPxAtZoom1 = 9;
    private const int BracketLengthPxAtZoom1 = 8;
    private static readonly Color SelectionColour = Color.White;

    /// <summary>Draws one unit at its interpolated position.</summary>
    /// <param name="batch">Batch in progress.</param>
    /// <param name="pixel">1x1 white texture.</param>
    /// <param name="sprites">Sprite resolver.</param>
    /// <param name="painter">Sprite painter.</param>
    /// <param name="memory">Renderer memory (walk state).</param>
    /// <param name="tick">Snapshot tick.</param>
    /// <param name="rules">Rules, or null.</param>
    /// <param name="unit">Unit to draw.</param>
    /// <param name="selected">Whether the unit is selected (adds brackets).</param>
    /// <param name="camera">View.</param>
    public static void Draw(SpriteBatch batch, Texture2D pixel, SpriteResolver sprites, SpritePainter painter,
        RenderMemory memory, int tick, SimRules? rules, RenderUnit unit, bool selected, Camera camera)
    {
        UnitView view = unit.View;
        bool infantry = view.Kind == UnitKind.Infantry;
        ResolvedSprite sprite = sprites.ForUnit(rules, view);
        bool moving = memory.IsMoving(UnitKey.Of(view));
        int frame = SpriteStates.UnitFrame(sprite.Manifest, infantry, moving, view.Facing, tick);
        Vector2 origin = SpriteLayout.UnitOrigin(camera, unit.World);
        painter.Draw(batch, sprite, frame, origin, PlayerColours.Base(view.Player), camera.Zoom);

        if (!selected) return;
        float zoom = camera.Zoom;
        int cx = (int)origin.X;
        int cy = (int)origin.Y;
        int half = (int)MathF.Round((infantry ? InfantryBracketHalfPxAtZoom1 : VehicleBracketHalfPxAtZoom1) * zoom);
        int length = Math.Max(3, (int)MathF.Round(BracketLengthPxAtZoom1 * zoom));
        int thick = zoom < 1f ? 1 : 2;
        DrawBracket(batch, pixel, cx, cy, half, length, thick);
    }

    // Four L-shaped corners around (cx, cy).
    private static void DrawBracket(SpriteBatch batch, Texture2D pixel, int cx, int cy, int half, int length, int thick)
    {
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                int px = cx + sx * half;
                int py = cy + sy * half;
                int hx = sx < 0 ? px : px - length;
                int hy = sy < 0 ? py : py - thick;
                Shapes.Fill(batch, pixel, new Rectangle(hx, hy, length, thick), SelectionColour);
                int vx = sx < 0 ? px : px - thick;
                int vy = sy < 0 ? py : py - length;
                Shapes.Fill(batch, pixel, new Rectangle(vx, vy, thick, length), SelectionColour);
            }
        }
    }
}
