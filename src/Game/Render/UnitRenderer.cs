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
        Vector2 origin = SpriteLayout.UnitOrigin(camera, unit.World);
        Color house = PlayerColours.Base(view.Player);
        float drawScale = DrawScale(rules, view);   // M7-03: big ships are one cell but drawn larger (D-124)
        if (UnitVisuals.ShowTunnelMarker(view)) DrawTunnelMarker(batch, pixel, origin, infantry, house, camera.Zoom);
        float alpha = UnitVisuals.Alpha(view);
        if (UnitVisuals.ShowDiveMarker(view)) DrawDiveMarker(batch, pixel, origin, house, tick, camera.Zoom);
        // M3-18: shadow under the body, turret on top, dive replaces the body. M3-21: the snapshot carries the turret facing and the dive flag.
        // Companion sheets (<id>_turret, <id>_shadow) supply a layer when the body sheet has no such state.
        IReadOnlyList<UnitLayer> layers = UnitLayerPlan.For(sprite.Manifest, infantry, moving, view.Facing, UnitVisuals.TurretFacing(view),
            view.Diving, tick, sprite.TurretSheet?.Manifest, sprite.ShadowSheet?.Manifest);
        foreach (UnitLayer layer in layers)
        {
            Vector2 at = origin + new Vector2(layer.OffsetX, layer.OffsetY) * (camera.Zoom * drawScale);
            float layerAlpha = layer.Kind == UnitLayerKind.Shadow ? alpha * UnitLayerPlan.ShadowAlpha : alpha;
            ResolvedSprite layerSprite = layer.Sheet switch
            {
                UnitLayerSheet.TurretCompanion => sprite.TurretSheet!,
                UnitLayerSheet.ShadowCompanion => sprite.ShadowSheet!,
                _ => sprite,
            };
            painter.Draw(batch, layerSprite, layer.Frame, at, house, camera.Zoom * drawScale, layerAlpha);
        }

        if (!selected) return;
        float zoom = camera.Zoom;
        int cx = (int)origin.X;
        int cy = (int)origin.Y;
        int half = (int)MathF.Round((infantry ? InfantryBracketHalfPxAtZoom1 : VehicleBracketHalfPxAtZoom1) * zoom * Math.Max(1f, drawScale));
        int length = Math.Max(3, (int)MathF.Round(BracketLengthPxAtZoom1 * zoom));
        int thick = zoom < 1f ? 1 : 2;
        DrawBracket(batch, pixel, cx, cy, half, length, thick);
    }

    /// <summary>Sprite scale of a unit: the vehicle type's <c>draw_scale_permille</c> / 1000 (1 for infantry and unknown types). Game layer only: float is fine here.</summary>
    public static float DrawScale(SimRules? rules, UnitView view)
    {
        if (view.Kind != UnitKind.Vehicle || rules is null || (uint)view.TypeIndex >= (uint)rules.Vehicles.Count) return 1f;
        return rules.Vehicles[view.TypeIndex].DrawScalePermille / 1000f;
    }

    // Underground: a dashed square ring in the house colour, so the ghost reads as "in a tunnel".
    private static void DrawTunnelMarker(SpriteBatch batch, Texture2D pixel, Vector2 origin, bool infantry, Color house,
        float zoom)
    {
        int half = (int)MathF.Round((infantry ? 10 : 22) * zoom);
        var box = new Rectangle((int)origin.X - half, (int)origin.Y - half, half * 2, half * 2);
        int dash = Math.Max(2, (int)MathF.Round(4 * zoom));
        Shapes.DashedOutline(batch, pixel, box, Shapes.Alpha(house, 150), zoom < 1f ? 1 : 2, dash, dash);
    }

    // Diving: a dashed ring in the house colour that closes in over a short loop, so "going under" reads even without dive art.
    private static void DrawDiveMarker(SpriteBatch batch, Texture2D pixel, Vector2 origin, Color house, int tick, float zoom)
    {
        int phase = Math.Max(0, tick) % DiveLoopTicks;
        int half = (int)MathF.Round((26 - 14 * phase / (float)DiveLoopTicks) * zoom);
        var box = new Rectangle((int)origin.X - half, (int)origin.Y - half, half * 2, half * 2);
        int dash = Math.Max(2, (int)MathF.Round(4 * zoom));
        Shapes.DashedOutline(batch, pixel, box, Shapes.Alpha(house, 200), zoom < 1f ? 1 : 2, dash, dash);
    }

    private const int DiveLoopTicks = 20;

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
