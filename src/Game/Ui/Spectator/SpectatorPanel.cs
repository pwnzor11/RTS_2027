// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Game.Ui.Fonts;
using Rts.Sim;

namespace Rts.Game.Ui.Spectator;

/// <summary>
/// The spectator stats strip along the top (design 08 §2): one row per player with its house colour swatch, greyed out when
/// defeated; a click on a row picks that player for F2. Tab swaps the compact lines for the detail table. The speed and the view
/// show in the first line. Reads <see cref="SpectatorTable"/> rows once per tick.
/// </summary>
public sealed class SpectatorPanel
{
    private const int Pad = 8;
    private const int LineGap = 4;
    private static readonly Color PanelColour = new(0, 0, 0, 170);

    private readonly SpectatorController _controller;
    private readonly IncomeTracker _income = new();
    private IReadOnlyList<SpectatorRow> _rows = Array.Empty<SpectatorRow>();
    private int _rowsTick = -1;

    /// <summary>Creates the panel over a controller.</summary>
    public SpectatorPanel(SpectatorController controller) => _controller = controller;

    /// <summary>Top-left corner of the strip in screen pixels; the screen sets it each frame (below its own HUD).</summary>
    public Point Origin { get; set; } = new(8, 8);

    /// <summary>The rows as of the last <see cref="Update"/>.</summary>
    public IReadOnlyList<SpectatorRow> Rows => _rows;

    /// <summary>Refreshes the rows when the sim has reached a new tick.</summary>
    public void Update(Simulation sim)
    {
        if (sim.Tick == _rowsTick) return;
        _rowsTick = sim.Tick;
        _income.Observe(sim);
        _rows = SpectatorTable.Collect(sim, _income);
    }

    /// <summary>The screen rectangle of the strip, so clicks on it never reach the map.</summary>
    public Rectangle Bounds(int scale)
    {
        int lines = 1 + _rows.Count + (_controller.ShowDetail ? 1 : 0);
        int lineH = BitmapFont.LineHeight(scale, FontWeight.Regular) + LineGap;
        int widest = Measure(scale);
        return new Rectangle(Origin.X, Origin.Y, widest + 2 * Pad, lines * lineH + Pad);
    }

    /// <summary>The player whose row contains <paramref name="point"/>, or -1.</summary>
    public int RowAt(Point point, int scale)
    {
        Rectangle b = Bounds(scale);
        if (!b.Contains(point)) return -1;
        int lineH = BitmapFont.LineHeight(scale, FontWeight.Regular) + LineGap;
        int first = 1 + (_controller.ShowDetail ? 1 : 0);
        int index = (point.Y - b.Y - Pad / 2) / lineH - first;
        return index >= 0 && index < _rows.Count ? _rows[index].Player : -1;
    }

    /// <summary>Draws the strip.</summary>
    public void Draw(SpriteBatch sb, Texture2D pixel, int scale)
    {
        Rectangle b = Bounds(scale);
        sb.Draw(pixel, b, PanelColour);
        int lineH = BitmapFont.LineHeight(scale, FontWeight.Regular) + LineGap;
        int y = b.Y + Pad / 2;
        int x = b.X + Pad;
        BitmapFont.Draw(sb, pixel, $"SPECTATING  speed {_controller.SpeedLabel}  view {_controller.ViewLabel}  (V view  F2 vision  1-4 speed  Space pause  Tab detail)",
            new Vector2(x, y), new Color(0xD8, 0xC8, 0x8A), scale);
        y += lineH;
        if (_controller.ShowDetail)
        {
            BitmapFont.Draw(sb, pixel, SpectatorTable.DetailHeader, new Vector2(x + lineH, y), new Color(0xA0, 0xA0, 0xA0), scale);
            y += lineH;
        }

        foreach (SpectatorRow r in _rows)
        {
            Color house = PlayerColours.Base(r.Player);
            Color text = r.Defeated ? new Color(0x80, 0x80, 0x80) : Color.White;
            if (r.Defeated) house = new Color(0x60, 0x60, 0x60);
            sb.Draw(pixel, new Rectangle(x, y, lineH - LineGap, lineH - LineGap), house);
            if (r.Player == _controller.PickedPlayer)
                Shapes.Outline(sb, pixel, new Rectangle(x - 1, y - 1, lineH - LineGap + 2, lineH - LineGap + 2), Color.White, 1);
            string line = _controller.ShowDetail ? SpectatorTable.Detail(r) : SpectatorTable.Compact(r);
            BitmapFont.Draw(sb, pixel, line, new Vector2(x + lineH, y), text, scale);
            y += lineH;
        }
    }

    private int Measure(int scale)
    {
        int lineH = BitmapFont.LineHeight(scale, FontWeight.Regular) + LineGap;
        int w = BitmapFont.Measure("SPECTATING  speed PAUSED  view full vision  (V view  F2 vision  1-4 speed  Space pause  Tab detail)", scale);
        foreach (SpectatorRow r in _rows)
            w = Math.Max(w, BitmapFont.Measure(_controller.ShowDetail ? SpectatorTable.Detail(r) : SpectatorTable.Compact(r), scale));
        if (_controller.ShowDetail) w = Math.Max(w, BitmapFont.Measure(SpectatorTable.DetailHeader, scale));
        return w + lineH;
    }
}
