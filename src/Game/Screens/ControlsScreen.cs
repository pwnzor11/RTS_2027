// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Screens;

/// <summary>
/// The Controls page (M11-03): the Edge scroll, Scroll speed and Show diagnostics settings at the top, then the read-only key table
/// from <see cref="GameKeys"/> grouped Camera / Selection / Orders / Build &amp; System. Opened from Settings, from the pause menu's
/// Settings, and in play with F1 (or ?). Changes apply at once and are saved when the page closes.
/// </summary>
public sealed class ControlsScreen : IScreen
{
    private const int SpeedStep = 100;
    private const int RowCount = 3;
    private static readonly Color PanelColour = new(0x12, 0x14, 0x10, 238);
    private static readonly Color BorderColour = new(0x6A, 0x72, 0x60);
    private static readonly Color TitleColour = new(0xD8, 0xC8, 0x8A);
    private static readonly Color TextColour = new(0xDC, 0xDE, 0xD0);
    private static readonly Color KeyColour = new(0xE8, 0xA8, 0x28);
    private static readonly Color HintColour = new(0x90, 0x94, 0x86);
    private static readonly Color SelectColour = new(0x2A, 0x2E, 0x26);

    private readonly GameServices _s;
    private readonly string? _path;
    private int _row;
    private int _scroll;
    private int _visibleLines = 10;

    /// <summary>Creates the page. <paramref name="settingsPath"/> overrides where settings are saved (tests); null = the default file.</summary>
    public ControlsScreen(GameServices services, string? settingsPath = null)
    {
        _s = services;
        _path = settingsPath;
    }

    /// <summary>The text of the setting row <paramref name="row"/> (0 edge scroll, 1 scroll speed, 2 diagnostics).</summary>
    public static string RowText(Settings settings, int row) => row switch
    {
        0 => $"Edge scroll: {(settings.EdgeScroll ? "On" : "Off")}",
        1 => $"Scroll speed: {settings.ScrollSpeed} px/s  (Shift = 2x)",
        _ => $"Show diagnostics (tick, hash, fps): {(settings.ShowDiagnostics ? "On" : "Off")}",
    };

    /// <summary>The settings after stepping setting row <paramref name="row"/> by <paramref name="direction"/> (+1 / -1; a toggle flips either way).</summary>
    public static Settings Step(Settings settings, int row, int direction) => row switch
    {
        0 => settings with { EdgeScroll = !settings.EdgeScroll },
        1 => settings with
        {
            ScrollSpeed = Math.Clamp(settings.ScrollSpeed + direction * SpeedStep, Settings.MinScrollSpeed, Settings.MaxScrollSpeed),
        },
        _ => settings with { ShowDiagnostics = !settings.ShowDiagnostics },
    };

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => false;

    /// <inheritdoc />
    public void Enter()
    {
    }

    /// <inheritdoc />
    public void Exit() => _s.Settings.Save(_path);

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        if (input.ConsumePressed(Keys.Escape) || input.ConsumePressed(Keys.Back) || input.ConsumePressed(Keys.F1))
        {
            MenuSounds.Play(_s.Audio, MenuSounds.Back);
            _s.Screens.Pop();
            return;
        }

        if (input.ConsumePressed(Keys.Up)) _row = (_row + RowCount - 1) % RowCount;
        if (input.ConsumePressed(Keys.Down)) _row = (_row + 1) % RowCount;
        if (input.ConsumePressed(Keys.Left)) Apply(_row, -1);
        if (input.ConsumePressed(Keys.Right) || input.ConsumePressed(Keys.Enter)) Apply(_row, 1);

        int max = Math.Max(0, TableRows().Count - _visibleLines);
        if (input.ConsumePressed(Keys.PageDown)) _scroll += _visibleLines - 1;
        if (input.ConsumePressed(Keys.PageUp)) _scroll -= _visibleLines - 1;
        if (input.ConsumePressed(Keys.Home)) _scroll = 0;
        if (input.ConsumePressed(Keys.End)) _scroll = max;
        if (input.WheelDelta != 0) _scroll -= Math.Sign(input.WheelDelta) * 3;
        _scroll = Math.Clamp(_scroll, 0, max);

        Layout(out Rectangle[] rows, out Rectangle back, out _, out _);
        if (input.LeftPressed || input.RightPressed)
        {
            int dir = input.RightPressed ? -1 : 1;
            for (int i = 0; i < RowCount; i++)
            {
                if (!rows[i].Contains(input.MousePosition)) continue;
                _row = i;
                Apply(i, dir);
            }

            if (input.LeftPressed && back.Contains(input.MousePosition)) _s.Screens.Pop();
        }

        input.ConsumeKeyboard();
        input.ConsumeMouse();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillDim(sb, _s);
        int scale = _s.UiScale;
        int line = Line(scale);
        int pad = 5 * scale;
        Layout(out Rectangle[] rows, out Rectangle back, out Rectangle panel, out Rectangle table);
        Shapes.Fill(sb, _s.Pixel, panel, PanelColour);
        Shapes.Outline(sb, _s.Pixel, panel, BorderColour, Math.Max(1, scale / 2));
        BitmapFont.Draw(sb, _s.Pixel, "Controls", new Vector2(panel.X + pad, panel.Y + pad), TitleColour, scale, FontWeight.Bold);

        for (int i = 0; i < RowCount; i++)
        {
            if (i == _row) Shapes.Fill(sb, _s.Pixel, rows[i], SelectColour);
            BitmapFont.Draw(sb, _s.Pixel, RowText(_s.Settings, i), new Vector2(rows[i].X + scale * 2, rows[i].Y + scale), TextColour, scale);
        }

        List<(string Heading, string Key, string Action)> list = TableRows();
        _visibleLines = Math.Max(3, table.Height / line);
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, list.Count - _visibleLines));
        int keyCol = 0;
        foreach (GameKeyBinding b in GameKeys.Table) keyCol = Math.Max(keyCol, BitmapFont.Measure(b.Label, scale));
        keyCol += 4 * scale;
        for (int i = 0; i < _visibleLines && _scroll + i < list.Count; i++)
        {
            (string heading, string key, string action) = list[_scroll + i];
            var at = new Vector2(table.X, table.Y + i * line);
            if (heading.Length > 0)
            {
                BitmapFont.Draw(sb, _s.Pixel, heading, at, TitleColour, scale, FontWeight.Bold);
                continue;
            }

            BitmapFont.Draw(sb, _s.Pixel, key, at + new Vector2(2 * scale, 0), KeyColour, scale);
            BitmapFont.Draw(sb, _s.Pixel, action, at + new Vector2(keyCol, 0), TextColour, scale);
        }

        string hint = "Up/Down Left/Right: settings   Wheel: scroll the list   Esc: back";
        BitmapFont.Draw(sb, _s.Pixel, hint, new Vector2(panel.X + pad, panel.Bottom - pad - line + scale), HintColour, scale);
        Shapes.Fill(sb, _s.Pixel, back, SelectColour);
        Shapes.Outline(sb, _s.Pixel, back, BorderColour);
        BitmapFont.Draw(sb, _s.Pixel, "Back", new Vector2(back.X + 3 * scale, back.Y + scale), TextColour, scale);
    }

    /// <summary>The key table as display rows: a heading row per group, then one row per binding.</summary>
    public static List<(string Heading, string Key, string Action)> TableRows()
    {
        var rows = new List<(string, string, string)>();
        foreach (string group in GameKeys.Groups)
        {
            rows.Add((group, "", ""));
            foreach (GameKeyBinding b in GameKeys.InGroup(group)) rows.Add(("", b.Label, b.Action));
        }

        return rows;
    }

    private void Apply(int row, int direction)
    {
        _s.Settings = Step(_s.Settings, row, direction);
        MenuSounds.Play(_s.Audio, MenuSounds.Move);
    }

    private static int Line(int scale) => BitmapFont.LineHeight(scale, FontWeight.Regular) + scale;

    private void Layout(out Rectangle[] rows, out Rectangle back, out Rectangle panel, out Rectangle table)
    {
        int scale = _s.UiScale;
        Viewport vp = _s.Viewport;
        int line = Line(scale);
        int pad = 5 * scale;
        int width = Math.Min(vp.Width - 8 * scale, 130 * scale * BitmapFont.Advance / 2);
        int height = vp.Height - 12 * scale;
        panel = new Rectangle((vp.Width - width) / 2, (vp.Height - height) / 2, width, height);
        rows = new Rectangle[RowCount];
        int y = panel.Y + pad + line + scale;
        for (int i = 0; i < RowCount; i++) rows[i] = new Rectangle(panel.X + pad, y + i * (line + scale), width - 2 * pad, line);
        int tableTop = y + RowCount * (line + scale) + 2 * scale;
        int bottom = panel.Bottom - pad - line - scale;
        table = new Rectangle(panel.X + pad, tableTop, width - 2 * pad, Math.Max(line * 3, bottom - tableTop));
        back = new Rectangle(panel.Right - pad - 12 * scale, panel.Bottom - pad - line, 12 * scale, line);
    }
}
