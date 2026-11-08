// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Ui;

namespace Rts.Game.Screens.Editor;

/// <summary>Screen rectangles of the editor, from the window size and UI scale. Pure, so it is tested.</summary>
public sealed record EditorLayout(Rectangle TopBar, Rectangle Tools, Rectangle MapView, Rectangle Issues, Rectangle StatusBar)
{
    /// <summary>Lays out: a top bar, a tool panel on the left, the validation panel on the right, a status line at the bottom, the map between.</summary>
    public static EditorLayout Compute(int width, int height, UiUnit scale)
    {
        int bar = 12 * scale, left = Math.Min(width / 3, 62 * scale), right = Math.Min(width / 3, 70 * scale), status = 9 * scale;
        var top = new Rectangle(0, 0, width, bar);
        var bottom = new Rectangle(0, height - status, width, status);
        var tools = new Rectangle(0, bar, left, height - bar - status);
        var issues = new Rectangle(width - right, bar, right, height - bar - status);
        var map = new Rectangle(left, bar, Math.Max(1, width - left - right), height - bar - status);
        return new EditorLayout(top, tools, map, issues, bottom);
    }

    /// <summary>The map cell under a screen point, given the pixel size of a cell and the world origin offset (screen position of cell 0,0).</summary>
    public static (int X, int Y) CellAt(Point screen, Point origin, int cellPixels) =>
        (FloorDiv(screen.X - origin.X, cellPixels), FloorDiv(screen.Y - origin.Y, cellPixels));

    /// <summary>The origin that centres cell (x, y) in <paramref name="view"/>.</summary>
    public static Point OriginToCentre(Rectangle view, int x, int y, int cellPixels) =>
        new(view.X + view.Width / 2 - x * cellPixels - cellPixels / 2, view.Y + view.Height / 2 - y * cellPixels - cellPixels / 2);

    private static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);
}
