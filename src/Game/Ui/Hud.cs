// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Fonts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Ui;

/// <summary>A translucent text panel: one or more lines of <see cref="BitmapFont"/> text on a dark backing.</summary>
public static class Hud
{
    private const int Pad = 8;
    private const int LineGapPx = 4;
    private static readonly Color PanelColour = new(0, 0, 0, 170);
    private static readonly Color TextColour = new(0xD8, 0xC8, 0x8A);

    /// <summary>Height in screen pixels of a panel with <paramref name="lines"/> lines at <paramref name="scale"/>.</summary>
    public static int HeightOf(int lines, int scale) => BitmapFont.LineHeight(scale, FontWeight.Regular) * lines + LineGapPx * (lines - 1) + Pad;

    /// <summary>Draws the panel with its top-left corner at <paramref name="origin"/>.</summary>
    public static void Panel(SpriteBatch batch, Texture2D pixel, Point origin, IReadOnlyList<string> lines, int scale)
    {
        if (lines.Count == 0) return;
        int textWidth = lines.Max(line => BitmapFont.Measure(line, scale));
        int lineHeight = BitmapFont.LineHeight(scale, FontWeight.Regular) + LineGapPx;
        batch.Draw(pixel, new Rectangle(origin.X, origin.Y, textWidth + 2 * Pad, HeightOf(lines.Count, scale)), PanelColour);
        for (int i = 0; i < lines.Count; i++)
        {
            var at = new Vector2(origin.X + Pad, origin.Y + Pad / 2 + i * lineHeight);
            BitmapFont.Draw(batch, pixel, lines[i], at, TextColour, scale);
        }
    }

    /// <summary>Height in screen pixels of a panel with <paramref name="lines"/> lines at a fractional UI scale (1, 1.5 or 2; SUP-4 / VET-5).</summary>
    public static int HeightOf(int lines, float uiScale) =>
        (int)(BitmapFont.LineHeight(uiScale, FontWeight.Regular) * lines + LineGapPx * (lines - 1) + Pad);

    /// <summary>Draws the panel at a fractional UI scale, with its top-left corner at <paramref name="origin"/>.</summary>
    public static void Panel(SpriteBatch batch, Texture2D pixel, Point origin, IReadOnlyList<string> lines, float uiScale)
    {
        if (lines.Count == 0) return;
        int textWidth = lines.Max(line => BitmapFont.Measure(line, uiScale, FontWeight.Regular));
        int lineHeight = BitmapFont.LineHeight(uiScale, FontWeight.Regular) + LineGapPx;
        batch.Draw(pixel, new Rectangle(origin.X, origin.Y, textWidth + 2 * Pad, HeightOf(lines.Count, uiScale)), PanelColour);
        for (int i = 0; i < lines.Count; i++)
        {
            var at = new Vector2(origin.X + Pad, origin.Y + Pad / 2 + i * lineHeight);
            BitmapFont.Draw(batch, pixel, lines[i], at, TextColour, uiScale, FontWeight.Regular);
        }
    }
}
