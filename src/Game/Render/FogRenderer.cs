using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// Fog of war from the snapshot's vision: never seen (0) is black, seen before (1) is 50% black, visible now (2) is
/// clear. Omniscient snapshots carry no vision and are drawn without fog.
/// </summary>
public static class FogRenderer
{
    private const byte Shroud = 0;
    private const byte Fog = 1;
    private const byte HalfBlack = 128;
    private static readonly Color Black = new(0, 0, 0, 255);
    private static readonly Color HalfBlackColour = new(0, 0, 0, (int)HalfBlack);

    /// <summary>Darkens the visible cells that are not currently visible to the viewer.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, SimSnapshot snapshot, Camera camera, Viewport viewport)
    {
        if (snapshot.Viewer < 0 || snapshot.Vision.Count != snapshot.MapWidth * snapshot.MapHeight) return;
        if (!camera.VisibleCells(snapshot.MapWidth, snapshot.MapHeight, viewport, out int x0, out int y0, out int x1, out int y1))
        {
            return;
        }

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                byte vision = snapshot.Vision[y * snapshot.MapWidth + x];
                if (vision > Fog) continue;
                Color colour = vision == Shroud ? Black : HalfBlackColour;
                Shapes.Fill(batch, pixel, camera.ScreenRect(x * Camera.CellPx, y * Camera.CellPx, (x + 1) * Camera.CellPx, (y + 1) * Camera.CellPx), colour);
            }
        }
    }
}
