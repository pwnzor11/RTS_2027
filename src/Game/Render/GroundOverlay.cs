// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// The M3 ground layers from <see cref="SimSnapshot.Ground"/>: a sickly green tint whose strength follows the radiation level, a
/// faint grout pattern on hardened cells, and Blackout clouds drawn dark. The snapshot already holds only what the viewer may
/// know (radiation and Blackout on cells visible now, hardened cells of the own team anywhere), so this draws every flagged cell.
/// <see cref="DrawBelowUnits"/> runs after the terrain and <see cref="DrawBlackout"/> after the sprites; the alpha and pattern
/// helpers are pure and unit-tested.
/// </summary>
public static class GroundOverlay
{
    /// <summary>Opacity of a Blackout cloud (0..255 black): the cell reads as dark, not as unexplored.</summary>
    public const byte BlackoutAlpha = 200;

    private static readonly Color Sickly = new(0x62, 0xD8, 0x2C);
    private static readonly Color Grout = new(0xD4, 0xD0, 0xBC);

    /// <summary>True when the snapshot carries ground data for the whole map.</summary>
    public static bool HasData(SimSnapshot snapshot) =>
        snapshot.Ground.Count > 0 && snapshot.Ground.Count == snapshot.MapWidth * snapshot.MapHeight;

    /// <summary>Radiation level 0..15 of a ground byte.</summary>
    public static int RadiationLevel(byte ground) => ground & GroundLayers.RadiationMask;

    /// <summary>
    /// Opacity of the radiation tint for a level 0..15 (0 for none; 28 for the faintest, about 130 for the strongest), with a slow
    /// pulse of up to 12 either way driven by the tick so contaminated ground looks alive.
    /// </summary>
    public static byte RadiationAlpha(int level, int tick)
    {
        if (level <= 0) return 0;
        level = Math.Min(level, 15);
        int pulse = ((tick / 4) % 6) switch { 0 => -12, 1 => -6, 2 => 0, 3 => 6, 4 => 12, _ => 0 };
        int baseAlpha = 28 + level * 7;
        return (byte)Math.Clamp(baseAlpha + pulse * level / 15, 1, 255);
    }

    /// <summary>
    /// The grout lines of a hardened cell, as rectangles in the cell's own zoom-1 pixels (64 x 64): three courses of bricks with
    /// the vertical joints staggered between courses. Thin and drawn faintly, so the ground still reads as ground.
    /// </summary>
    public static IReadOnlyList<Rectangle> GroutRects()
    {
        const int cell = Camera.CellPx;
        const int course = cell / 3;
        var rects = new List<Rectangle>();
        for (int row = 0; row < 3; row++)
        {
            int top = row * course;
            if (row > 0) rects.Add(new Rectangle(0, top, cell, 2));
            if (row % 2 == 0) rects.Add(new Rectangle(cell / 2 - 1, top, 2, course));
            else
            {
                rects.Add(new Rectangle(cell / 4 - 1, top, 2, course));
                rects.Add(new Rectangle(3 * cell / 4 - 1, top, 2, course));
            }
        }
        return rects;
    }

    /// <summary>Radiation tint and hardened grout. Call after the terrain, before buildings and units.</summary>
    public static void DrawBelowUnits(SpriteBatch batch, Texture2D pixel, SimSnapshot snapshot, Camera camera, Viewport viewport)
    {
        if (!HasData(snapshot)) return;
        if (!camera.VisibleCells(snapshot.MapWidth, snapshot.MapHeight, viewport, out int x0, out int y0, out int x1, out int y1)) return;
        IReadOnlyList<Rectangle> grout = GroutRects();
        Color groutColour = Shapes.Alpha(Grout, 46);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                byte g = snapshot.Ground[y * snapshot.MapWidth + x];
                if (g == 0) continue;
                float left = x * Camera.CellPx, top = y * Camera.CellPx;
                byte alpha = RadiationAlpha(RadiationLevel(g), snapshot.Tick);
                if (alpha > 0)
                {
                    Shapes.Fill(batch, pixel, camera.ScreenRect(left, top, left + Camera.CellPx, top + Camera.CellPx), Shapes.Alpha(Sickly, alpha));
                }

                if ((g & GroundLayers.Hardened) == 0) continue;
                foreach (Rectangle r in grout)
                {
                    Shapes.Fill(batch, pixel, camera.ScreenRect(left + r.X, top + r.Y, left + r.Right, top + r.Bottom), groutColour);
                }
            }
        }
    }

    /// <summary>Blackout clouds as dark cells. Call after the sprites and before the fog.</summary>
    public static void DrawBlackout(SpriteBatch batch, Texture2D pixel, SimSnapshot snapshot, Camera camera, Viewport viewport)
    {
        if (!HasData(snapshot)) return;
        if (!camera.VisibleCells(snapshot.MapWidth, snapshot.MapHeight, viewport, out int x0, out int y0, out int x1, out int y1)) return;
        Color dark = Shapes.Alpha(new Color(4, 6, 4), BlackoutAlpha);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if ((snapshot.Ground[y * snapshot.MapWidth + x] & GroundLayers.Blackout) == 0) continue;
                Shapes.Fill(batch, pixel, camera.ScreenRect(x * Camera.CellPx, y * Camera.CellPx, (x + 1) * Camera.CellPx, (y + 1) * Camera.CellPx), dark);
            }
        }
    }
}
