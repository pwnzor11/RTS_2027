using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>Flat debug terrain: one colour per land type, with a grid line on every cell edge. Not final art.</summary>
public static class TerrainRenderer
{
    private static readonly Color GridLine = new(0, 0, 0, 90);

    /// <summary>Draws the visible cells and their grid lines.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, SimSnapshot snapshot, Camera camera, Viewport viewport)
    {
        if (!camera.VisibleCells(snapshot.MapWidth, snapshot.MapHeight, viewport, out int x0, out int y0, out int x1, out int y1))
        {
            return;
        }

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Rectangle r = camera.ScreenRect(x * Camera.CellPx, y * Camera.CellPx, (x + 1) * Camera.CellPx, (y + 1) * Camera.CellPx);
                batch.Draw(pixel, r, ColourFor(snapshot.Land[y * snapshot.MapWidth + x]));
            }
        }

        int gridTop = camera.ScreenY(y0 * Camera.CellPx);
        int gridBottom = camera.ScreenY((y1 + 1) * Camera.CellPx);
        for (int x = x0; x <= x1 + 1; x++)
        {
            batch.Draw(pixel, new Rectangle(camera.ScreenX(x * Camera.CellPx), gridTop, 1, gridBottom - gridTop), GridLine);
        }

        int gridLeft = camera.ScreenX(x0 * Camera.CellPx);
        int gridRight = camera.ScreenX((x1 + 1) * Camera.CellPx);
        for (int y = y0; y <= y1 + 1; y++)
        {
            batch.Draw(pixel, new Rectangle(gridLeft, camera.ScreenY(y * Camera.CellPx), gridRight - gridLeft, 1), GridLine);
        }
    }

    /// <summary>Debug colour for a land type. Unknown types draw magenta so they are obvious.</summary>
    public static Color ColourFor(LandType land) => land switch
    {
        LandType.Clear => new Color(0x23, 0x27, 0x1E),
        LandType.Road => new Color(0x4E, 0x4A, 0x42),
        LandType.Rough => new Color(0x2F, 0x33, 0x24),
        LandType.Ash => new Color(0x3A, 0x3A, 0x38),
        LandType.Water => new Color(0x1C, 0x2E, 0x3A),
        LandType.Rock => new Color(0x46, 0x41, 0x3B),
        LandType.Cliff => new Color(0x18, 0x18, 0x16),
        _ => Color.Magenta,
    };
}
