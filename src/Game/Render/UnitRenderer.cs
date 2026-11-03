// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// Vehicles as the tank sprite drawn in the player's house colour (all vehicle types use it for now), infantry as 12 px
/// squares in the player colour, and white corner brackets around selected units.
/// </summary>
public sealed class UnitRenderer
{
    private const string VehicleState = "idle";
    private const int InfantrySidePxAtZoom1 = 12;
    private const int VehicleBracketHalfPxAtZoom1 = 26;
    private const int InfantryBracketHalfPxAtZoom1 = 9;
    private const int BracketLengthPxAtZoom1 = 8;
    private static readonly Color SelectionColour = Color.White;

    private readonly SpriteSheet _sheet;

    /// <summary>Creates a renderer that draws vehicles with the given sprite sheet.</summary>
    public UnitRenderer(SpriteSheet sheet)
    {
        _sheet = sheet;
    }

    /// <summary>Draws every unit in the frame at its interpolated position.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera)
    {
        foreach (RenderUnit unit in frame.Units)
        {
            bool selected = frame.Selection.Contains(UnitKey.Of(unit.View));
            DrawUnit(batch, pixel, unit, selected, camera, frame.Snapshot.Tick);
        }
    }

    private void DrawUnit(SpriteBatch batch, Texture2D pixel, RenderUnit unit, bool selected, Camera camera, int tick)
    {
        Vector2 screen = camera.ScreenFromWorld(unit.World);
        int cx = (int)MathF.Round(screen.X);
        int cy = (int)MathF.Round(screen.Y);
        int player = unit.View.Player;
        bool vehicle = unit.View.Kind == UnitKind.Vehicle;
        float zoom = camera.Zoom;

        if (vehicle)
        {
            int frame = _sheet.FrameIndex(VehicleState, unit.View.Facing, tick);
            _sheet.Draw(batch, frame, new Vector2(cx, cy), PlayerColours.Base(player), zoom);
        }
        else
        {
            int side = (int)MathF.Round(InfantrySidePxAtZoom1 * zoom);
            Shapes.Fill(batch, pixel, new Rectangle(cx - side / 2, cy - side / 2, side, side), PlayerColours.Base(player));
        }

        if (selected)
        {
            int half = (int)MathF.Round((vehicle ? VehicleBracketHalfPxAtZoom1 : InfantryBracketHalfPxAtZoom1) * zoom);
            int length = Math.Max(3, (int)MathF.Round(BracketLengthPxAtZoom1 * zoom));
            int thick = zoom < 1f ? 1 : 2;
            DrawBracket(batch, pixel, cx, cy, half, length, thick);
        }
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
