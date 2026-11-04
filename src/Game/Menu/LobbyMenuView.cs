// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Menu;

/// <summary>Where the main menu's parts are on screen, from <see cref="LobbyMenuView.Measure"/>.</summary>
/// <param name="Scale">UI scale used.</param>
/// <param name="Rows">Click area of each entry.</param>
/// <param name="TitleX">Left of the title block.</param>
/// <param name="TitleY">Top of the title block.</param>
/// <param name="TitleCell">Size of one title cell in pixels.</param>
/// <param name="RowWidth">Width of every row.</param>
public sealed record LobbyLayout(int Scale, Rectangle[] Rows, float TitleX, float TitleY, float TitleCell, int RowWidth);

/// <summary>
/// Lays out and draws the main menu as <see cref="DrawOp"/>s: a dark scrim down the left, the block-letter title, the entries (the
/// selected one with an amber bar, disabled ones greyed with a tooltip), the scene caption and the version.
/// </summary>
public static class LobbyMenuView
{
    private static readonly Rgba Amber = new(0xE8, 0xA8, 0x28);
    private static readonly Rgba AmberDeep = new(0xB0, 0x6A, 0x14);
    private static readonly Rgba TextLight = new(0xE6, 0xE6, 0xD8);
    private static readonly Rgba TextDim = new(0x7A, 0x7C, 0x70);

    /// <summary>The game's name as drawn in the title.</summary>
    public const string Title = "RTS_2027";

    /// <summary>The line under the title.</summary>
    // ASSUMED (M3-16): the tagline wording (from the setting doc: forty years after the exchange, ash still falls).
    public const string Tagline = "Forty years after the exchange, the ash still falls.";

    /// <summary>Computes the layout for a window and UI scale.</summary>
    public static LobbyLayout Measure(int viewW, int viewH, int scale, IReadOnlyList<MainMenuEntry> entries)
    {
        int margin = Math.Max(10 * scale, (int)(viewW * 0.06f));
        float cell = scale * 3f;
        float titleH = cell * TitleArt.HeightCells;
        int line = BitmapFont.LineHeight(scale, FontWeight.Regular);
        int rowH = line + 5 * scale;
        int taglineH = line + 4 * scale;
        int block = (int)titleH + taglineH + 8 * scale + entries.Count * rowH;
        // Squeeze the rows when the window is short, never below a readable minimum.
        int avail = viewH - 2 * scale * 8;
        if (block > avail) rowH = Math.Max(line + 2, rowH - (block - avail) / Math.Max(1, entries.Count));
        block = (int)titleH + taglineH + 8 * scale + entries.Count * rowH;
        int top = Math.Max(4 * scale, (viewH - block) / 2 - 2 * scale);
        int rowW = entries.Count == 0 ? 0 : entries.Max(e => BitmapFont.Measure(e.Label, scale, FontWeight.Regular)) + 24 * scale;
        rowW = Math.Max(rowW, 60 * scale);

        var rows = new Rectangle[entries.Count];
        int y = top + (int)titleH + taglineH + 8 * scale;
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new Rectangle(margin, y, rowW, rowH);
            y += rowH;
        }
        return new LobbyLayout(scale, rows, margin, top, cell, rowW);
    }

    /// <summary>Appends the ops for the menu. <paramref name="selected"/> is the highlighted entry; <paramref name="seconds"/> animates the pulse.</summary>
    public static void Build(List<DrawOp> ops, LobbyLayout layout, IReadOnlyList<MainMenuEntry> entries, int selected, float seconds,
        string caption, string version, int viewW, int viewH)
    {
        int s = layout.Scale;

        // Scrim: darkest at the left edge, fading out by 46% of the width, so the menu reads over any scene.
        const int strips = 36;
        float scrimW = viewW * 0.46f;
        for (int i = 0; i < strips; i++)
        {
            float t = i / (float)strips;
            byte a = (byte)(205 * (1f - ParallaxMath.Smooth(t)));
            // Whole-pixel edges, so neighbouring strips neither overlap nor leave a gap.
            int x0 = (int)MathF.Round(scrimW * i / strips), x1 = (int)MathF.Round(scrimW * (i + 1) / strips);
            ops.Add(DrawOp.Rect(x0, 0, x1 - x0, viewH, new Rgba(6, 7, 6, a)));
        }

        TitleArt.Build(ops, Title, layout.TitleX, layout.TitleY, layout.TitleCell, Amber, AmberDeep);
        float tagY = layout.TitleY + layout.TitleCell * TitleArt.HeightCells + 3 * s;
        ops.Add(DrawOp.Rect(layout.TitleX, tagY, layout.TitleCell * TitleArt.WidthCells(Title), Math.Max(1, s / 2), Amber.Fade(0.8f)));
        ops.Add(DrawOp.Text(Tagline, layout.TitleX, tagY + 3 * s, s, new Rgba(0xB4, 0xB4, 0xA4)));

        int line = BitmapFont.LineHeight(s, FontWeight.Regular);
        for (int i = 0; i < entries.Count; i++)
        {
            Rectangle row = layout.Rows[i];
            MainMenuEntry e = entries[i];
            bool sel = i == selected;
            int textY = row.Y + (row.Height - line) / 2;
            if (sel)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin(seconds * 3f);
                ops.Add(DrawOp.Rect(row.X, row.Y, row.Width, row.Height, new Rgba(255, 255, 255, (byte)(e.Enabled ? 20 + 6 * pulse : 10))));
                ops.Add(DrawOp.Rect(row.X, row.Y, Math.Max(2, s), row.Height, e.Enabled ? Amber : TextDim));
            }
            Rgba colour = !e.Enabled ? TextDim : sel ? new Rgba(255, 255, 255) : TextLight;
            ops.Add(DrawOp.Text(e.Label, row.X + 6 * s, textY, s, colour, bold: sel && e.Enabled));
        }

        if (selected >= 0 && selected < entries.Count && entries[selected].Tooltip is { } tip && !entries[selected].Enabled)
        {
            Rectangle row = layout.Rows[selected];
            int tw = BitmapFont.Measure(tip, s, FontWeight.Regular) + 10 * s;
            var box = new Rectangle(row.Right + 4 * s, row.Y + 1, tw, row.Height - 2);
            ops.Add(DrawOp.Rect(box.X, box.Y, box.Width, box.Height, new Rgba(12, 13, 11, 235)));
            ops.Add(DrawOp.Rect(box.X, box.Y, box.Width, Math.Max(1, s / 2), Amber.Fade(0.7f)));
            ops.Add(DrawOp.Text(tip, box.X + 5 * s, row.Y + (row.Height - line) / 2, s, new Rgba(0xF0, 0xD8, 0x98)));
        }

        // Footer.
        int footerY = viewH - line - 6 * s;
        if (caption.Length > 0)
        {
            ops.Add(DrawOp.Rect(layout.TitleX, footerY + line / 2 - s, 2 * s, 2 * s, Amber));
            ops.Add(DrawOp.Text(caption, layout.TitleX + 5 * s, footerY, s, new Rgba(0xA8, 0xA8, 0x98)));
        }
        string right = $"v{version}   GPL v3";
        ops.Add(DrawOp.Text(right, viewW - BitmapFont.Measure(right, s, FontWeight.Regular) - 8 * s, footerY, s, new Rgba(0x80, 0x82, 0x76)));
    }
}
