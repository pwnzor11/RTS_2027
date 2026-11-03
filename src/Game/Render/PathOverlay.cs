using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Game.Render;

/// <summary>
/// F4: for each selected unit, the line through the cells it still has to walk (from its current position), and an
/// outline on the cell it has reserved.
/// </summary>
public static class PathOverlay
{
    private const int LinePx = 2;
    private const int ReservedPx = 2;
    private static readonly Color Route = new(0xF2, 0xD0, 0x4B);
    private static readonly Color Reserved = new(0x4B, 0xD6, 0xF2);

    /// <summary>Draws the routes and reservations of the selected units.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera)
    {
        if (frame.Sim is null) return;
        foreach (UnitKey key in frame.Selection.Units)
        {
            MobileStore? store = StoreOf(frame.Sim, key);
            if (store is null || !frame.TryFind(key, out Vector2 start)) continue;
            int i = key.Id.Index;

            CellCoord[]? cells = store.PathCells[i];
            if (cells is not null)
            {
                Vector2 previous = camera.ScreenFromWorld(start);
                for (int k = store.PathPos[i]; k < cells.Length; k++)
                {
                    Vector2 next = camera.ScreenFromWorld(Camera.CentreOf(cells[k]));
                    Shapes.Line(batch, pixel, previous, next, Route, LinePx);
                    previous = next;
                }
            }

            if (store.NextCellReserved[i])
            {
                CellCoord cell = store.ReservedCell[i];
                Rectangle r = camera.ScreenRect(cell.X * Camera.CellPx, cell.Y * Camera.CellPx,
                    (cell.X + 1) * Camera.CellPx, (cell.Y + 1) * Camera.CellPx);
                Shapes.Outline(batch, pixel, r, Reserved, ReservedPx);
            }
        }
    }

    private static MobileStore? StoreOf(Rts.Sim.Simulation sim, UnitKey key)
    {
        MobileStore store = key.Kind == ActorKind.Vehicle ? sim.Units : sim.Infantry;
        return store.Slots.IsAlive(key.Id) ? store : null;
    }
}
