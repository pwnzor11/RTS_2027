// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;

namespace Rts.Game.Ui;

/// <summary>
/// The minimap's coordinate transforms, free of graphics so they can be tested. A map of <c>MapWidth x MapHeight</c> cells is fitted
/// into <see cref="Area"/> keeping its aspect ratio at a fractional scale, centred; the
/// fitted rectangle is <see cref="Image"/>. Cells are floats (3.5 = the middle of cell 3); world pixels are the camera's zoom-1
/// pixels (<see cref="Camera.CellPx"/> per cell).
/// </summary>
public readonly struct MinimapMath
{
    private MinimapMath(Rectangle area, int mapWidth, int mapHeight, float scale, Rectangle image)
    {
        Area = area; MapWidth = mapWidth; MapHeight = mapHeight; Scale = scale; Image = image;
    }

    /// <summary>The space available.</summary>
    public Rectangle Area { get; }

    /// <summary>Map width in cells.</summary>
    public int MapWidth { get; }

    /// <summary>Map height in cells.</summary>
    public int MapHeight { get; }

    /// <summary>Screen pixels per cell.</summary>
    public float Scale { get; }

    /// <summary>The part of <see cref="Area"/> the map covers (centred).</summary>
    public Rectangle Image { get; }

    /// <summary>Fits a map into an area. An empty area or map gives an empty image.</summary>
    public static MinimapMath Fit(Rectangle area, int mapWidth, int mapHeight)
    {
        if (area.Width <= 0 || area.Height <= 0 || mapWidth <= 0 || mapHeight <= 0)
            return new MinimapMath(area, mapWidth, mapHeight, 0f, Rectangle.Empty);
        float scale = MathF.Min(area.Width / (float)mapWidth, area.Height / (float)mapHeight);
        int w = Math.Max(1, (int)MathF.Round(mapWidth * scale)), h = Math.Max(1, (int)MathF.Round(mapHeight * scale));
        var image = new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
        return new MinimapMath(area, mapWidth, mapHeight, scale, image);
    }

    /// <summary>True if the point is on the map picture.</summary>
    public bool Contains(Point screen) => Image.Contains(screen);

    /// <summary>The map position (in cells, clamped to the map) under a screen point.</summary>
    public Vector2 CellFromPoint(Point screen)
    {
        if (Scale <= 0f) return Vector2.Zero;
        float x = (screen.X - Image.X) * MapWidth / (float)Image.Width;
        float y = (screen.Y - Image.Y) * MapHeight / (float)Image.Height;
        return new Vector2(Math.Clamp(x, 0f, MapWidth), Math.Clamp(y, 0f, MapHeight));
    }

    /// <summary>The screen point for a map position in cells (not clamped).</summary>
    public Vector2 PointFromCell(Vector2 cell) =>
        new(Image.X + cell.X * Image.Width / MapWidth, Image.Y + cell.Y * Image.Height / MapHeight);

    /// <summary>The camera's view as a rectangle on the minimap, from its top-left and size in zoom-1 world pixels.</summary>
    public Rectangle ViewportRect(Vector2 cameraTopLeftWorld, Vector2 viewSizeWorld)
    {
        Vector2 a = PointFromCell(cameraTopLeftWorld / Camera.CellPx);
        Vector2 b = PointFromCell((cameraTopLeftWorld + viewSizeWorld) / Camera.CellPx);
        int left = (int)MathF.Round(a.X), top = (int)MathF.Round(a.Y);
        return new Rectangle(left, top, Math.Max(1, (int)MathF.Round(b.X) - left), Math.Max(1, (int)MathF.Round(b.Y) - top));
    }

    /// <summary>
    /// The camera top-left (zoom-1 world pixels) that centres the view on the clicked minimap point, kept inside the map. A view
    /// larger than the map is pinned to the top-left.
    /// </summary>
    public Vector2 CameraTopLeftFor(Point screen, Vector2 viewSizeWorld)
    {
        Vector2 centre = CellFromPoint(screen) * Camera.CellPx;
        Vector2 topLeft = centre - viewSizeWorld / 2f;
        float maxX = MathF.Max(0f, MapWidth * Camera.CellPx - viewSizeWorld.X);
        float maxY = MathF.Max(0f, MapHeight * Camera.CellPx - viewSizeWorld.Y);
        return new Vector2(Math.Clamp(topLeft.X, 0f, maxX), Math.Clamp(topLeft.Y, 0f, maxY));
    }

    /// <summary>The map cell under a screen point, for orders (clamped inside the map).</summary>
    public Rts.Sim.Core.CellCoord OrderCell(Point screen)
    {
        Vector2 c = CellFromPoint(screen);
        return new Rts.Sim.Core.CellCoord(Math.Clamp((int)MathF.Floor(c.X), 0, MapWidth - 1), Math.Clamp((int)MathF.Floor(c.Y), 0, MapHeight - 1));
    }
}
