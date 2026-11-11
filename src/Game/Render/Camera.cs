// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// The view: a zoom of 0.5, 1 or 2 (cell 32, 64 or 128 px, always whole pixels) and a position in zoom-1 world pixels
/// (64 px per cell). Converts between screen, world and cell coordinates.
/// </summary>
public sealed class Camera
{
    /// <summary>Cell size in pixels at zoom 1 (D-039).</summary>
    public const int CellPx = 64;

    private static readonly float[] ZoomLevels = { 0.5f, 1f, 2f };
    private static readonly string[] ZoomLabels = { "0.5", "1", "2" };

    private int _zoomIndex = 1;
    private Vector2 _position; // top-left of the view in zoom-1 world pixels

    /// <summary>Current zoom factor: 0.5, 1 or 2.</summary>
    public float Zoom => ZoomLevels[_zoomIndex];

    /// <summary>Current zoom as text for the HUD.</summary>
    public string ZoomLabel => ZoomLabels[_zoomIndex];

    /// <summary>Steps the zoom, clamped to the available levels. Positive zooms in.</summary>
    public void ZoomBy(int steps) => _zoomIndex = Math.Clamp(_zoomIndex + steps, 0, ZoomLevels.Length - 1);

    /// <summary>
    /// Steps the zoom keeping the world point under <paramref name="anchorScreen"/> (the cursor) where it is on screen (S18 GAMEUI-02).
    /// </summary>
    public void ZoomBy(int steps, Point anchorScreen)
    {
        Vector2 anchorWorld = WorldFromScreen(anchorScreen);
        int before = _zoomIndex;
        ZoomBy(steps);
        if (_zoomIndex != before) _position = anchorWorld - anchorScreen.ToVector2() / Zoom;
    }

    /// <summary>Moves the view by a delta in screen pixels. Positive x moves the view right.</summary>
    public void PanScreen(Vector2 screenDelta) => _position += screenDelta / Zoom;

    /// <summary>Keeps the view over the map.</summary>
    public void Clamp(int mapWidth, int mapHeight, Viewport viewport)
    {
        float maxX = MathF.Max(0f, mapWidth * CellPx - viewport.Width / Zoom);
        float maxY = MathF.Max(0f, mapHeight * CellPx - viewport.Height / Zoom);
        _position = new Vector2(Math.Clamp(_position.X, 0f, maxX), Math.Clamp(_position.Y, 0f, maxY));
    }

    /// <summary>Screen pixel to zoom-1 world pixel.</summary>
    public Vector2 WorldFromScreen(Point screen) => _position + screen.ToVector2() / Zoom;

    /// <summary>Zoom-1 world pixel to screen pixel (not rounded).</summary>
    public Vector2 ScreenFromWorld(Vector2 world) => (world - _position) * Zoom;

    /// <summary>World x to a whole screen pixel.</summary>
    public int ScreenX(float worldX) => (int)MathF.Round((worldX - _position.X) * Zoom);

    /// <summary>World y to a whole screen pixel.</summary>
    public int ScreenY(float worldY) => (int)MathF.Round((worldY - _position.Y) * Zoom);

    /// <summary>A world-space box to a screen rectangle.</summary>
    public Rectangle ScreenRect(float left, float top, float right, float bottom)
    {
        int l = ScreenX(left), t = ScreenY(top);
        return new Rectangle(l, t, ScreenX(right) - l, ScreenY(bottom) - t);
    }

    /// <summary>The cell containing a zoom-1 world point.</summary>
    public static CellCoord CellAt(Vector2 world) =>
        new((int)MathF.Floor(world.X / CellPx), (int)MathF.Floor(world.Y / CellPx));

    /// <summary>Centre of a cell in zoom-1 world pixels.</summary>
    public static Vector2 CentreOf(CellCoord cell) => new((cell.X + 0.5f) * CellPx, (cell.Y + 0.5f) * CellPx);

    /// <summary>A lepton position in zoom-1 world pixels (256 leptons per 64 px cell).</summary>
    public static Vector2 WorldFromLepton(LeptonCoord lepton) =>
        new(lepton.X * (CellPx / (float)SimConstants.LeptonsPerCell), lepton.Y * (CellPx / (float)SimConstants.LeptonsPerCell));

    /// <summary>
    /// Inclusive range of map cells that intersect the view, clamped to the map. Returns false if none do.
    /// </summary>
    public bool VisibleCells(int mapWidth, int mapHeight, Viewport viewport, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = Math.Max(0, (int)MathF.Floor(_position.X / CellPx));
        y0 = Math.Max(0, (int)MathF.Floor(_position.Y / CellPx));
        x1 = Math.Min(mapWidth - 1, (int)MathF.Floor((_position.X + viewport.Width / Zoom) / CellPx));
        y1 = Math.Min(mapHeight - 1, (int)MathF.Floor((_position.Y + viewport.Height / Zoom) / CellPx));
        return x0 <= x1 && y0 <= y1;
    }
}
