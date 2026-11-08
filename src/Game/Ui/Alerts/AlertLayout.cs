// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Ui.Alerts;

/// <summary>Pure placement of the alert stack: the top-right corner of the map view, moved down past any panel it would overlap.</summary>
public static class AlertLayout
{
    /// <summary>
    /// The rectangle for a stack of <paramref name="size"/> inside <paramref name="mapView"/>. Starts at the top right (inset by
    /// <paramref name="margin"/>) and drops under each rectangle in <paramref name="avoid"/> that it would touch. Empty rectangles are ignored.
    /// </summary>
    public static Rectangle Place(Rectangle mapView, Point size, int margin, int gap, IEnumerable<Rectangle> avoid)
    {
        var rects = avoid.Where(r => r.Width > 0 && r.Height > 0).OrderBy(r => r.Top).ToList();
        int x = mapView.Right - margin - size.X;
        int y = mapView.Top + margin;
        bool moved = true;
        while (moved)
        {
            moved = false;
            var me = new Rectangle(x, y, size.X, size.Y);
            foreach (Rectangle r in rects)
            {
                Rectangle padded = r;
                padded.Inflate(gap, 0);
                if (!me.Intersects(padded) || me.Top >= r.Bottom + gap) continue;
                y = r.Bottom + gap;
                moved = true;
                break;
            }
        }

        return new Rectangle(x, y, size.X, size.Y);
    }
}
