// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Game.Render;

/// <summary>
/// F3: where the sim has a vehicle (blue tint on the cell), an infantry soldier (green square in the cell corner)
/// or a building (red tint) in its occupancy map. With a fog viewer only cells that viewer can see are marked, so the
/// overlay does not show what the fog hides.
/// </summary>
public static class OccupancyOverlay
{
    private const byte TintAlpha = 90;
    private const int InfantryMarkPx = 16;
    private static readonly Color VehicleTint = new(0x30, 0x70, 0xFF);
    private static readonly Color InfantryMark = new(0x30, 0xE0, 0x50);
    private static readonly Color BuildingTint = new(0xFF, 0x30, 0x30);

    /// <summary>Draws the occupancy marks for the visible cells.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, Simulation sim, SimSnapshot snapshot, Camera camera, Viewport viewport)
    {
        int width = sim.Map.Width, height = sim.Map.Height;
        if (!camera.VisibleCells(width, height, viewport, out int x0, out int y0, out int x1, out int y1)) return;
        bool fogged = snapshot.Viewer >= 0 && snapshot.Vision.Count == width * height;

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (fogged && snapshot.Vision[y * width + x] != 2) continue;
                var cell = new CellCoord(x, y);
                Rectangle r = camera.ScreenRect(x * Camera.CellPx, y * Camera.CellPx, (x + 1) * Camera.CellPx, (y + 1) * Camera.CellPx);

                if (!sim.Map.VehicleAt(cell).IsNone) Shapes.Fill(batch, pixel, r, Shapes.Alpha(VehicleTint, TintAlpha));
                if (!sim.Map.BuildingAt(cell).IsNone) Shapes.Fill(batch, pixel, r, Shapes.Alpha(BuildingTint, TintAlpha));
                if (HasInfantry(sim.Map, cell))
                {
                    int mark = (int)MathF.Round(InfantryMarkPx * camera.Zoom);
                    Shapes.Fill(batch, pixel, new Rectangle(r.X, r.Y, mark, mark), InfantryMark);
                }
            }
        }
    }

    private static bool HasInfantry(Rts.Sim.Map.MapState map, CellCoord cell)
    {
        for (int slot = 0; slot < InfantryStore.SubSlotCount; slot++)
            if (!map.InfantryAt(cell, slot).IsNone) return true;
        return false;
    }
}
