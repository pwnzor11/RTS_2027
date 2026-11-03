using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;

namespace Rts.Game;

/// <summary>
/// Draws the map grid and units from a SimSnapshot. Owns the camera: integer zoom 1-4, pan in screen
/// pixels, and an origin snapped to whole pixels so every edge stays crisp under point sampling.
/// </summary>
public sealed class GridRenderer
{
    /// <summary>Cell size in pixels at zoom 1.</summary>
    public const int TilePx = 32;

    /// <summary>Smallest allowed integer zoom.</summary>
    public const int MinZoom = 1;

    /// <summary>Largest allowed integer zoom.</summary>
    public const int MaxZoom = 4;

    private const int UnitSidePxAtZoom1 = 16;
    private const int FacingLengthPxAtZoom1 = 12;
    private const int FacingThicknessPx = 2;

    private static readonly Color MapFill = new(0x23, 0x27, 0x1E);
    private static readonly Color GridLine = new(0x3A, 0x40, 0x32);
    private static readonly Color FacingLine = new(0xF0, 0xE6, 0xC8);
    private static readonly Color Player0Colour = new(0x6B, 0x7B, 0x5A);
    private static readonly Color Player1Colour = new(0xA0, 0x52, 0x2D);
    private static readonly Color OtherPlayerColour = new(0x80, 0x80, 0x80);

    private readonly Texture2D _pixel;
    private Vector2 _camera; // top-left of the view in zoom-1 pixels; snapped to whole screen pixels when drawn

    /// <summary>Creates a renderer that draws with the given 1x1 white texture.</summary>
    /// <param name="pixel">A 1x1 white texture used for every rectangle and line.</param>
    public GridRenderer(Texture2D pixel)
    {
        _pixel = pixel;
    }

    /// <summary>Current integer zoom, between <see cref="MinZoom"/> and <see cref="MaxZoom"/>.</summary>
    public int Zoom { get; private set; } = MinZoom;

    /// <summary>Changes zoom by whole steps, clamped to the allowed range.</summary>
    /// <param name="steps">Positive to zoom in, negative to zoom out.</param>
    public void ZoomBy(int steps)
    {
        Zoom = Math.Clamp(Zoom + steps, MinZoom, MaxZoom);
    }

    /// <summary>Moves the view by a delta in screen pixels. Positive x moves the view right.</summary>
    /// <param name="screenDelta">Movement in screen pixels, converted to map pixels at the current zoom.</param>
    public void PanScreen(Vector2 screenDelta)
    {
        _camera += screenDelta / Zoom;
    }

    /// <summary>Draws one frame of the snapshot.</summary>
    /// <param name="spriteBatch">A batch already begun with point sampling.</param>
    /// <param name="snapshot">The snapshot to draw. Read only.</param>
    /// <param name="viewport">The current back-buffer viewport, used to clamp the camera.</param>
    public void Draw(SpriteBatch spriteBatch, SimSnapshot snapshot, Viewport viewport)
    {
        ClampCamera(snapshot, viewport);

        int cell = TilePx * Zoom;
        int originX = (int)MathF.Round(-_camera.X * Zoom);
        int originY = (int)MathF.Round(-_camera.Y * Zoom);
        int mapWidthPx = snapshot.MapWidth * cell;
        int mapHeightPx = snapshot.MapHeight * cell;

        spriteBatch.Draw(_pixel, new Rectangle(originX, originY, mapWidthPx, mapHeightPx), MapFill);

        for (int x = 0; x <= snapshot.MapWidth; x++)
        {
            spriteBatch.Draw(_pixel, new Rectangle(originX + x * cell, originY, 1, mapHeightPx), GridLine);
        }

        for (int y = 0; y <= snapshot.MapHeight; y++)
        {
            spriteBatch.Draw(_pixel, new Rectangle(originX, originY + y * cell, mapWidthPx, 1), GridLine);
        }

        foreach (UnitView unit in snapshot.Units)
        {
            DrawUnit(spriteBatch, unit, originX, originY);
        }
    }

    private void ClampCamera(SimSnapshot snapshot, Viewport viewport)
    {
        float maxX = MathF.Max(0f, snapshot.MapWidth * TilePx - viewport.Width / (float)Zoom);
        float maxY = MathF.Max(0f, snapshot.MapHeight * TilePx - viewport.Height / (float)Zoom);
        _camera = new Vector2(Math.Clamp(_camera.X, 0f, maxX), Math.Clamp(_camera.Y, 0f, maxY));
    }

    private void DrawUnit(SpriteBatch spriteBatch, UnitView unit, int originX, int originY)
    {
        int centreX = originX + ToPixels(unit.Position.X);
        int centreY = originY + ToPixels(unit.Position.Y);

        int side = UnitSidePxAtZoom1 * Zoom;
        spriteBatch.Draw(
            _pixel,
            new Rectangle(centreX - side / 2, centreY - side / 2, side, side),
            ColourFor(unit.Player));

        // Facing: 0 = north, clockwise, 256 steps per turn. Screen y grows downwards, so north is -y.
        float radians = unit.Facing * MathF.Tau / 256f;
        Vector2 heading = new(MathF.Sin(radians), -MathF.Cos(radians));
        float rotation = MathF.Atan2(heading.Y, heading.X);

        spriteBatch.Draw(
            _pixel,
            new Vector2(centreX, centreY),
            null,
            FacingLine,
            rotation,
            new Vector2(0f, 0.5f),
            new Vector2(FacingLengthPxAtZoom1 * Zoom, FacingThicknessPx),
            SpriteEffects.None,
            0f);
    }

    /// <summary>Converts a lepton offset to pixels at the current zoom. Integer math, exact for whole pixels.</summary>
    private int ToPixels(int lepton) => lepton * TilePx * Zoom / SimConstants.LeptonsPerCell;

    private static Color ColourFor(byte player) => player switch
    {
        0 => Player0Colour,
        1 => Player1Colour,
        _ => OtherPlayerColour,
    };
}
