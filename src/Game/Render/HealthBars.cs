// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// Health bars above units and buildings. Shown only when the unit is damaged or selected (buildings: only when
/// damaged). Green above half health, yellow above a quarter, red below. Ghosts have no bar: their health is stale.
/// </summary>
public static class HealthBars
{
    private const int VehicleWidthPx = 40;
    private const int InfantryWidthPx = 16;
    private const int VehicleHalfPx = 26;
    private const int InfantryHalfPx = 9;
    private const int BarHeightPx = 4;
    private const int GapPx = 8;
    private static readonly Color Back = new(0, 0, 0, 200);
    private static readonly Color Green = new(0x3C, 0xC8, 0x50);
    private static readonly Color Yellow = new(0xE8, 0xC0, 0x30);
    private static readonly Color Red = new(0xD0, 0x30, 0x30);

    /// <summary>Draws the bars for the frame's units and buildings. Does nothing without rules.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera)
    {
        if (frame.Rules is null) return;
        float zoom = camera.Zoom;
        int height = Math.Max(2, (int)MathF.Round(BarHeightPx * zoom));
        int gap = (int)MathF.Round(GapPx * zoom);

        foreach (RenderUnit unit in frame.Units)
        {
            UnitView view = unit.View;
            bool vehicle = view.Kind == UnitKind.Vehicle;
            int max = vehicle ? frame.Rules.Vehicles[view.TypeIndex].MaxHealth : frame.Rules.Infantry[view.TypeIndex].MaxHealth;
            if (!frame.Selection.Contains(UnitKey.Of(view)) && view.Health >= max) continue;

            Vector2 centre = camera.ScreenFromWorld(unit.World);
            int half = (int)MathF.Round((vehicle ? VehicleHalfPx : InfantryHalfPx) * zoom);
            int width = (int)MathF.Round((vehicle ? VehicleWidthPx : InfantryWidthPx) * zoom);
            var r = new Rectangle((int)MathF.Round(centre.X) - width / 2, (int)MathF.Round(centre.Y) - half - gap - height, width, height);
            Bar(batch, pixel, r, view.Health, max);
        }

        foreach (BuildingView building in frame.Snapshot.Buildings)
        {
            if (building.Ghost) continue;
            int max = frame.Rules.Buildings[building.TypeIndex].MaxHealth;
            if (building.Health >= max) continue;

            float left = building.Cell.X * Camera.CellPx;
            float top = building.Cell.Y * Camera.CellPx;
            Rectangle foot = camera.ScreenRect(left, top,
                left + building.FootprintW * Camera.CellPx, top + building.FootprintH * Camera.CellPx);
            int width = foot.Width * 3 / 4;
            var r = new Rectangle(foot.Center.X - width / 2, foot.Top - gap - height, width, height);
            Bar(batch, pixel, r, building.Health, max);
        }
    }

    private static void Bar(SpriteBatch batch, Texture2D pixel, Rectangle r, int health, int max)
    {
        float fraction = max <= 0 ? 0f : Math.Clamp(health / (float)max, 0f, 1f);
        Shapes.Fill(batch, pixel, r, Back);
        int filled = (int)MathF.Round(r.Width * fraction);
        if (filled <= 0) return;
        Color colour = fraction > 0.5f ? Green : fraction > 0.25f ? Yellow : Red;
        Shapes.Fill(batch, pixel, new Rectangle(r.X, r.Y, filled, r.Height), colour);
    }
}
