using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Controls;
using Rts.Game.Sprites;
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;

namespace Rts.Game;

/// <summary>
/// Draws the map, the units and the selection from snapshots. Owns the camera: a zoom of 0.5, 1 or 2 (cell 32, 64
/// or 128 px, always whole pixels) and a pan position in zoom-1 world pixels. Vehicles are the house-recoloured
/// tank sprite (D-039, D-041); infantry are 12 px squares in the player colour until M2.
/// </summary>
public sealed class GridRenderer
{
    /// <summary>Cell size in pixels at zoom 1 (D-039).</summary>
    public const int CellPx = 64;

    private const int InfantrySidePxAtZoom1 = 12;
    private const int VehicleBracketHalfPxAtZoom1 = 26;
    private const int InfantryBracketHalfPxAtZoom1 = 9;
    private const int BracketLengthPxAtZoom1 = 8;

    // Zoom steps. Each gives a whole-pixel cell size (32, 64, 128).
    private static readonly float[] ZoomLevels = { 0.5f, 1f, 2f };
    private static readonly string[] ZoomLabels = { "0.5", "1", "2" };

    private static readonly Color GridLine = new(0, 0, 0, 90);
    private static readonly Color SelectionColour = Color.White;

    private readonly Texture2D _pixel;
    private readonly SpriteSheet _sheet;
    private int _zoomIndex = 1;
    private Vector2 _camera; // top-left of the view in zoom-1 world pixels

    /// <summary>Creates a renderer that draws with the given 1x1 white texture and vehicle sprite sheet.</summary>
    public GridRenderer(Texture2D pixel, SpriteSheet sheet)
    {
        _pixel = pixel;
        _sheet = sheet;
    }

    /// <summary>Current zoom factor: 0.5, 1 or 2.</summary>
    public float Zoom => ZoomLevels[_zoomIndex];

    /// <summary>Current zoom as text for the HUD.</summary>
    public string ZoomLabel => ZoomLabels[_zoomIndex];

    /// <summary>Steps the zoom, clamped to the available levels. Positive zooms in.</summary>
    public void ZoomBy(int steps) => _zoomIndex = Math.Clamp(_zoomIndex + steps, 0, ZoomLevels.Length - 1);

    /// <summary>Moves the view by a delta in screen pixels. Positive x moves the view right.</summary>
    public void PanScreen(Vector2 screenDelta) => _camera += screenDelta / Zoom;

    /// <summary>Keeps the camera over the map.</summary>
    public void ClampCamera(SimSnapshot snapshot, Viewport viewport)
    {
        float maxX = MathF.Max(0f, snapshot.MapWidth * CellPx - viewport.Width / Zoom);
        float maxY = MathF.Max(0f, snapshot.MapHeight * CellPx - viewport.Height / Zoom);
        _camera = new Vector2(Math.Clamp(_camera.X, 0f, maxX), Math.Clamp(_camera.Y, 0f, maxY));
    }

    /// <summary>Screen pixel to zoom-1 world pixel.</summary>
    public Vector2 WorldFromScreen(Point screen) => _camera + screen.ToVector2() / Zoom;

    /// <summary>Zoom-1 world pixel to screen pixel (not rounded).</summary>
    public Vector2 ScreenFromWorld(Vector2 world) => (world - _camera) * Zoom;

    /// <summary>A lepton position in zoom-1 world pixels (256 leptons per 64 px cell).</summary>
    public static Vector2 WorldFromLepton(LeptonCoord lepton) =>
        new(lepton.X * (CellPx / (float)SimConstants.LeptonsPerCell), lepton.Y * (CellPx / (float)SimConstants.LeptonsPerCell));

    /// <summary>
    /// Fills <paramref name="output"/> with every unit of <paramref name="current"/> at its position between
    /// <paramref name="previous"/> and <paramref name="current"/>. Units are matched by id; a unit with no match
    /// (newly spawned) is drawn at its current position.
    /// </summary>
    /// <param name="alpha">0 shows the previous positions, 1 the current ones.</param>
    public static void Interpolate(SimSnapshot current, SimSnapshot previous, float alpha, List<RenderUnit> output)
    {
        output.Clear();
        var before = new Dictionary<EntityId, UnitView>();
        if (!ReferenceEquals(current, previous))
        {
            foreach (UnitView unit in previous.Units) before[unit.Id] = unit;
        }

        foreach (UnitView unit in current.Units)
        {
            Vector2 now = WorldFromLepton(unit.Position);
            Vector2 at = now;
            if (before.TryGetValue(unit.Id, out UnitView old))
            {
                at = Vector2.Lerp(WorldFromLepton(old.Position), now, alpha);
            }
            output.Add(new RenderUnit(unit, at));
        }
    }

    /// <summary>Draws one frame: terrain, grid, units, selection brackets and the drag box.</summary>
    /// <param name="batch">A batch already begun with point sampling.</param>
    /// <param name="snapshot">The current snapshot (map size and terrain).</param>
    /// <param name="viewport">The back-buffer viewport.</param>
    /// <param name="units">Units with interpolated positions, from <see cref="Interpolate"/>.</param>
    /// <param name="selection">Selected unit ids.</param>
    /// <param name="dragRect">Screen rectangle of an active box selection, or null.</param>
    public void Draw(SpriteBatch batch, SimSnapshot snapshot, Viewport viewport, IReadOnlyList<RenderUnit> units,
        Selection selection, Rectangle? dragRect)
    {
        DrawTerrain(batch, snapshot, viewport);

        foreach (RenderUnit unit in units)
        {
            DrawUnit(batch, unit, selection.Contains(unit.View.Id));
        }

        if (dragRect is Rectangle box)
        {
            DrawOutline(batch, box, SelectionColour);
        }
    }

    /// <summary>
    /// Flat debug palette, one colour per land type. Not final art; the terrain tiles come with the art pass.
    /// </summary>
    public static Color ColourFor(LandType land) => land switch
    {
        LandType.Clear => new Color(0x23, 0x27, 0x1E),
        LandType.Road => new Color(0x4E, 0x4A, 0x42),
        LandType.Rough => new Color(0x2F, 0x33, 0x24),
        LandType.Ash => new Color(0x3A, 0x3A, 0x38),
        LandType.Water => new Color(0x1C, 0x2E, 0x3A),
        LandType.Rock => new Color(0x46, 0x41, 0x3B),
        LandType.Cliff => new Color(0x18, 0x18, 0x16),
        _ => Color.Magenta, // unknown land type: obvious on screen
    };

    private int ScreenX(float worldX) => (int)MathF.Round((worldX - _camera.X) * Zoom);

    private int ScreenY(float worldY) => (int)MathF.Round((worldY - _camera.Y) * Zoom);

    private void DrawTerrain(SpriteBatch batch, SimSnapshot snapshot, Viewport viewport)
    {
        int firstX = Math.Max(0, (int)MathF.Floor(_camera.X / CellPx));
        int firstY = Math.Max(0, (int)MathF.Floor(_camera.Y / CellPx));
        int lastX = Math.Min(snapshot.MapWidth - 1, (int)MathF.Floor((_camera.X + viewport.Width / Zoom) / CellPx));
        int lastY = Math.Min(snapshot.MapHeight - 1, (int)MathF.Floor((_camera.Y + viewport.Height / Zoom) / CellPx));
        if (firstX > lastX || firstY > lastY) return;

        for (int y = firstY; y <= lastY; y++)
        {
            int top = ScreenY(y * CellPx);
            int bottom = ScreenY((y + 1) * CellPx);
            for (int x = firstX; x <= lastX; x++)
            {
                int left = ScreenX(x * CellPx);
                int right = ScreenX((x + 1) * CellPx);
                batch.Draw(_pixel, new Rectangle(left, top, right - left, bottom - top),
                    ColourFor(snapshot.Land[y * snapshot.MapWidth + x]));
            }
        }

        // Grid lines on every visible cell edge.
        int gridTop = ScreenY(firstY * CellPx);
        int gridBottom = ScreenY((lastY + 1) * CellPx);
        for (int x = firstX; x <= lastX + 1; x++)
        {
            int sx = ScreenX(x * CellPx);
            batch.Draw(_pixel, new Rectangle(sx, gridTop, 1, gridBottom - gridTop), GridLine);
        }

        int gridLeft = ScreenX(firstX * CellPx);
        int gridRight = ScreenX((lastX + 1) * CellPx);
        for (int y = firstY; y <= lastY + 1; y++)
        {
            int sy = ScreenY(y * CellPx);
            batch.Draw(_pixel, new Rectangle(gridLeft, sy, gridRight - gridLeft, 1), GridLine);
        }
    }

    private void DrawUnit(SpriteBatch batch, RenderUnit unit, bool selected)
    {
        Vector2 screen = ScreenFromWorld(unit.World);
        int cx = (int)MathF.Round(screen.X);
        int cy = (int)MathF.Round(screen.Y);
        int player = unit.View.Player;
        bool vehicle = unit.View.Kind == UnitKind.Vehicle;

        if (vehicle)
        {
            int frame = Facing.ToFacings(unit.View.Facing, _sheet.Facings);
            batch.Draw(_sheet.Texture(player), new Vector2(cx, cy), _sheet.FrameRect(frame), Color.White,
                0f, _sheet.Anchor, Zoom, SpriteEffects.None, 0f);
        }
        else
        {
            int side = (int)MathF.Round(InfantrySidePxAtZoom1 * Zoom);
            batch.Draw(_pixel, new Rectangle(cx - side / 2, cy - side / 2, side, side), PlayerColours.Base(player));
        }

        if (selected)
        {
            int half = (int)MathF.Round((vehicle ? VehicleBracketHalfPxAtZoom1 : InfantryBracketHalfPxAtZoom1) * Zoom);
            int length = Math.Max(3, (int)MathF.Round(BracketLengthPxAtZoom1 * Zoom));
            int thick = Zoom < 1f ? 1 : 2;
            DrawBracket(batch, cx, cy, half, length, thick);
        }
    }

    /// <summary>Four L-shaped corners around (cx, cy).</summary>
    private void DrawBracket(SpriteBatch batch, int cx, int cy, int half, int length, int thick)
    {
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                int px = cx + sx * half;
                int py = cy + sy * half;
                int hx = sx < 0 ? px : px - length;
                int hy = sy < 0 ? py : py - thick;
                batch.Draw(_pixel, new Rectangle(hx, hy, length, thick), SelectionColour);
                int vx = sx < 0 ? px : px - thick;
                int vy = sy < 0 ? py : py - length;
                batch.Draw(_pixel, new Rectangle(vx, vy, thick, length), SelectionColour);
            }
        }
    }

    private void DrawOutline(SpriteBatch batch, Rectangle r, Color colour)
    {
        batch.Draw(_pixel, new Rectangle(r.Left, r.Top, r.Width, 1), colour);
        batch.Draw(_pixel, new Rectangle(r.Left, r.Bottom - 1, r.Width, 1), colour);
        batch.Draw(_pixel, new Rectangle(r.Left, r.Top, 1, r.Height), colour);
        batch.Draw(_pixel, new Rectangle(r.Right - 1, r.Top, 1, r.Height), colour);
    }
}
