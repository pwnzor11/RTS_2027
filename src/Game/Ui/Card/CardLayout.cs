// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;

namespace Rts.Game.Ui.Card;

/// <summary>
/// Where everything on the selection card goes (M11-04): pure integer arithmetic from the map view rectangle, the card scale and the
/// line height, so it is unit-tested. The card is centred along the bottom of the MAP VIEW (never over the sidebar), sits clear of
/// the edge-scroll band at the window's bottom edge, and shrinks (down to scale 1, or the UI scale when that is lower) when the map view is narrow.
/// </summary>
public sealed class CardLayout
{
    /// <summary>Card width at scale 1, in pixels.</summary>
    public const int WidthUnits = 580;

    /// <summary>Text rows of a single unit or building card (name, type, health, rank, up to three stat lines).</summary>
    public const int TextRows = 7;

    private CardLayout(Rectangle card, Rectangle portrait, Rectangle text, Rectangle header, Rectangle hint, Rectangle[] buttons, Rectangle[] tiles, int tileCount, int overflow, int lineStep)
    {
        Card = card; Portrait = portrait; Text = text; Header = header; Hint = hint; Buttons = buttons; Tiles = tiles; TileCount = tileCount; Overflow = overflow; LineStep = lineStep;
    }

    /// <summary>The whole card (empty when nothing is selected).</summary>
    public Rectangle Card { get; }

    /// <summary>The portrait square (single unit and building).</summary>
    public Rectangle Portrait { get; }

    /// <summary>The text column (single unit and building).</summary>
    public Rectangle Text { get; }

    /// <summary>The header line (group).</summary>
    public Rectangle Header { get; }

    /// <summary>The hint line under the grid (group).</summary>
    public Rectangle Hint { get; }

    /// <summary>One rectangle per button of the model, in order.</summary>
    public IReadOnlyList<Rectangle> Buttons { get; }

    /// <summary>The tiles of the group grid in order: unit cameos first, then the "+N" tile when there is overflow.</summary>
    public IReadOnlyList<Rectangle> Tiles { get; }

    /// <summary>How many of <see cref="Tiles"/> are unit cameos.</summary>
    public int TileCount { get; }

    /// <summary>The N of the "+N" tile; 0 when there is none.</summary>
    public int Overflow { get; }

    /// <summary>Vertical distance between text rows.</summary>
    public int LineStep { get; }

    /// <summary>The empty layout: no card, nothing hit.</summary>
    public static CardLayout None { get; } = new(Rectangle.Empty, Rectangle.Empty, Rectangle.Empty, Rectangle.Empty, Rectangle.Empty,
        Array.Empty<Rectangle>(), Array.Empty<Rectangle>(), 0, 0, 0);

    /// <summary>
    /// S18 UXG-03: the lowest y that other bottom-left panels (mission ticker and dialogue) may end at: <paramref name="defaultBottom"/>, or
    /// just above the card when the card is showing, so the card never covers them.
    /// </summary>
    public int ClearBottom(int defaultBottom, int gap) => Card.IsEmpty ? defaultBottom : Math.Min(defaultBottom, Card.Top - gap);

    /// <summary>
    /// The scale the card draws at: <paramref name="ui"/> (the sidebar's UI scale) while the card fits the map view, otherwise smaller,
    /// never below 1 (or the UI scale itself when that is under 1, M11-10h).
    /// </summary>
    public static float ScaleFor(int mapViewWidth, float ui)
    {
        float s = ui;
        int room = mapViewWidth - 2 * Margin(Math.Min(1f, ui));
        if (WidthUnits * s > room) s = room / (float)WidthUnits;
        return Math.Max(Math.Min(ui, 1f), Math.Min(ui, s));
    }

    /// <summary>
    /// How many text rows a single unit or building card needs: name, type and health, then the rank line, the stat lines (two, or three
    /// on a card too narrow for two) and any extra lines.
    /// </summary>
    public static int RowsFor(CardModel model, int cardWidth, float scale)
    {
        int rows = 3 + model.Lines.Count + (model.RankLine is null ? 0 : 1);
        if (model.Kind == CardKind.Unit) rows += cardWidth >= (int)MathF.Round(WidthUnits * 0.8f * scale) ? 2 : 3;
        return rows;
    }

    private static int Margin(float s) => (int)MathF.Round(8f * s);

    /// <summary>Computes the layout.</summary>
    /// <param name="mapView">The map view rectangle (the window minus the sidebar).</param>
    /// <param name="scale">Card scale, from <see cref="ScaleFor"/>.</param>
    /// <param name="lineHeight">Height of one text line at that scale.</param>
    /// <param name="kind">What the card shows.</param>
    /// <param name="buttonCount">Number of buttons.</param>
    /// <param name="unitCount">Selected units (group only).</param>
    /// <param name="textRows">Text rows of a single unit or building card (see <see cref="RowsFor"/>).</param>
    public static CardLayout Compute(Rectangle mapView, float scale, int lineHeight, CardKind kind, int buttonCount, int unitCount, int textRows = TextRows)
    {
        if (kind == CardKind.None) return None;
        int margin = Margin(scale), pad = (int)MathF.Round(6f * scale), gap = (int)MathF.Round(4f * scale);
        int lineStep = lineHeight + (int)MathF.Round(2f * scale);
        int maxW = (int)MathF.Round(WidthUnits * scale);
        int w = Math.Max(120, Math.Min(maxW, mapView.Width - 2 * margin));

        int cols = w >= (int)MathF.Round(430f * scale) ? 3 : 2;
        int bw = (int)MathF.Round(58f * scale * Math.Max(1f, UiScale.TextFactor));   // larger text: wider buttons, so "Repair" still fits
        int bh = lineHeight + (int)MathF.Round(8f * scale);
        int buttonRows = (buttonCount + cols - 1) / cols;
        int usedCols = Math.Min(cols, Math.Max(1, buttonCount));
        int buttonsW = buttonCount == 0 ? 0 : usedCols * bw + (usedCols - 1) * gap;
        int buttonsH = buttonRows * bh + Math.Max(0, buttonRows - 1) * gap;

        int portrait = (int)MathF.Round(64f * scale);
        int contentH;
        int shown = 0, overflow = 0, tileSide = (int)MathF.Round(34f * scale), tileCols = 1, tileRows = 0;
        int gridW = w - 2 * pad - (buttonsW > 0 ? buttonsW + pad : 0);
        if (kind == CardKind.Multi)
        {
            (shown, overflow) = CardGrouping.Visible(unitCount);
            int tiles = shown + (overflow > 0 ? 1 : 0);
            tileCols = Math.Max(1, (gridW + gap) / (tileSide + gap));
            tileRows = Math.Max(1, (tiles + tileCols - 1) / tileCols);
            contentH = lineStep + tileRows * (tileSide + gap) + lineStep;
        }
        else
        {
            textRows = Math.Clamp(textRows, 3, TextRows + 2);
            contentH = Math.Max(portrait, textRows * lineStep);
        }

        int h = 2 * pad + Math.Max(contentH, buttonsH);
        int bottomGap = Math.Max(margin, CameraScroll.EdgeBandPx + gap);
        int x = mapView.X + (mapView.Width - w) / 2;
        int y = Math.Max(mapView.Y, mapView.Bottom - bottomGap - h);
        var card = new Rectangle(x, y, w, h);

        var buttons = new Rectangle[buttonCount];
        int bx0 = card.Right - pad - buttonsW;
        for (int i = 0; i < buttonCount; i++)
            buttons[i] = new Rectangle(bx0 + (i % cols) * (bw + gap), card.Y + pad + (i / cols) * (bh + gap), bw, bh);

        Rectangle portraitRect = Rectangle.Empty, text = Rectangle.Empty, header = Rectangle.Empty, hint = Rectangle.Empty;
        var tileRects = Array.Empty<Rectangle>();
        if (kind == CardKind.Multi)
        {
            header = new Rectangle(card.X + pad, card.Y + pad, w - 2 * pad - (buttonsW > 0 ? buttonsW + pad : 0), lineHeight);
            int total = shown + (overflow > 0 ? 1 : 0);
            tileRects = new Rectangle[total];
            for (int i = 0; i < total; i++)
                tileRects[i] = new Rectangle(card.X + pad + (i % tileCols) * (tileSide + gap), card.Y + pad + lineStep + (i / tileCols) * (tileSide + gap), tileSide, tileSide);
            hint = new Rectangle(card.X + pad, card.Bottom - pad - lineHeight, gridW, lineHeight);
        }
        else
        {
            portraitRect = new Rectangle(card.X + pad, card.Y + pad, portrait, portrait);
            int tx = portraitRect.Right + pad;
            text = new Rectangle(tx, card.Y + pad, Math.Max(10, bx0 - (buttonsW > 0 ? pad : 0) - tx), textRows * lineStep);
        }

        return new CardLayout(card, portraitRect, text, header, hint, buttons, tileRects, shown, overflow, lineStep);
    }
}
