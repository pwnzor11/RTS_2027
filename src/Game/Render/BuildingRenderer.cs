using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sprites;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// Buildings as their footprint: a filled rectangle in the owner's colour with a 2 px dark border. Last-seen enemy
/// ghosts are half-transparent with a dashed border.
/// </summary>
public static class BuildingRenderer
{
    private const int BorderPx = 2;
    private const int DashPx = 6;
    private const int GapPx = 4;
    private const byte GhostAlpha = 128;
    private static readonly Color Border = new(0x10, 0x10, 0x10);

    /// <summary>Draws every building and ghost in the snapshot.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, SimSnapshot snapshot, Camera camera)
    {
        foreach (BuildingView building in snapshot.Buildings)
        {
            float left = building.Cell.X * Camera.CellPx;
            float top = building.Cell.Y * Camera.CellPx;
            Rectangle r = camera.ScreenRect(left, top,
                left + building.FootprintW * Camera.CellPx, top + building.FootprintH * Camera.CellPx);
            Color fill = PlayerColours.Base(building.Player);

            if (building.Ghost)
            {
                Shapes.Fill(batch, pixel, r, Shapes.Alpha(fill, GhostAlpha));
                Shapes.DashedOutline(batch, pixel, r, Shapes.Alpha(Border, GhostAlpha), BorderPx, DashPx, GapPx);
            }
            else
            {
                Shapes.Fill(batch, pixel, r, fill);
                Shapes.Outline(batch, pixel, r, Border, BorderPx);
            }
        }
    }
}
