// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Screens;

/// <summary>
/// The Controls page (M11-03, D-172 (6)): the Edge scroll, Scroll speed and Show diagnostics settings, "Reset to defaults", then every
/// control grouped Camera / Selection / Orders / Build &amp; System. Click a key (or select it and press Enter), then press the new key;
/// a key that another action uses is shown as a conflict and is only taken after a swap (Enter) or dropped (Esc). Opened from Settings,
/// from the pause menu's Settings, and in play with F1 (or ?). Changes apply at once and are saved when the page closes.
/// </summary>
public sealed class ControlsScreen : IScreen
{
    /// <summary>What one line of the page is.</summary>
    public enum ItemKind
    {
        /// <summary>One of the three settings (edge scroll, scroll speed, diagnostics).</summary>
        Setting,

        /// <summary>The "Reset to defaults" line.</summary>
        Reset,

        /// <summary>A group heading.</summary>
        Heading,

        /// <summary>A fixed input shown for reference (mouse, Shift, Esc).</summary>
        Fixed,

        /// <summary>A rebindable action.</summary>
        Binding,
    }

    /// <summary>One line of the page.</summary>
    /// <param name="Kind">What it is.</param>
    /// <param name="Index">The setting number for a Setting line.</param>
    /// <param name="Row">The control row for a Fixed or Binding line.</param>
    /// <param name="Heading">The group name for a Heading line.</param>
    public sealed record Item(ItemKind Kind, int Index = 0, ControlRow? Row = null, string Heading = "")
    {
        /// <summary>True for the lines the cursor can rest on.</summary>
        public bool Selectable => Kind is ItemKind.Setting or ItemKind.Reset or ItemKind.Binding;
    }

    private const int SpeedStep = 100;
    private const int SettingCount = 3;
    private static readonly Color PanelColour = new(0x12, 0x14, 0x10, 255);
    private static readonly Color BorderColour = new(0x6A, 0x72, 0x60);
    private static readonly Color TitleColour = new(0xD8, 0xC8, 0x8A);
    private static readonly Color TextColour = new(0xDC, 0xDE, 0xD0);
    private static readonly Color KeyColour = new(0xE8, 0xA8, 0x28);
    private static readonly Color ChangedColour = new(0x7A, 0xD0, 0x7A);
    private static readonly Color WarnColour = new(0xF0, 0x70, 0x50);
    private static readonly Color HintColour = new(0x90, 0x94, 0x86);
    private static readonly Color SelectColour = new(0x2A, 0x2E, 0x26);

    private static readonly IReadOnlyList<Item> AllItems = BuildItems();

    private readonly GameServices _s;
    private readonly string? _path;
    private readonly RebindSession _rebind = new();
    private int _sel;
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

    /// <summary>The settings after "Reset to defaults": every key back to its factory binding (other settings are not touched).</summary>
    public static Settings ResetKeys(Settings settings) => settings.WithBindings(KeyBindings.Default);

    /// <summary>All lines of the page in order: the settings, Reset, then a heading and the rows of each group.</summary>
    public static IReadOnlyList<Item> Items() => AllItems;

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
        if (_rebind.State != RebindSession.Phase.Idle)
        {
            UpdateRebind(input);
            input.ConsumeKeyboard();
            input.ConsumeMouse();
            return;
        }

        if (input.ConsumePressed(Keys.Escape) || input.ConsumePressed(Keys.Back) || input.ConsumePressed(Keys.F1))
        {
            MenuSounds.Play(_s.Audio, MenuSounds.Back);
            _s.Screens.Pop();
            return;
        }

        if (input.ConsumePressed(Keys.Up)) MoveSelection(-1);
        if (input.ConsumePressed(Keys.Down)) MoveSelection(1);
        if (input.ConsumePressed(Keys.Left)) Activate(_sel, -1);
        if (input.ConsumePressed(Keys.Right) || input.ConsumePressed(Keys.Enter)) Activate(_sel, 1);

        int max = Math.Max(0, AllItems.Count - _visibleLines);
        if (input.ConsumePressed(Keys.PageDown)) _scroll += _visibleLines - 1;
        if (input.ConsumePressed(Keys.PageUp)) _scroll -= _visibleLines - 1;
        if (input.ConsumePressed(Keys.Home)) _scroll = 0;
        if (input.ConsumePressed(Keys.End)) _scroll = max;
        if (input.WheelDelta != 0) _scroll -= Math.Sign(input.WheelDelta) * 3;
        _scroll = Math.Clamp(_scroll, 0, max);

        Layout(out Rectangle list, out Rectangle back, out _);
        if (input.LeftPressed || input.RightPressed)
        {
            int line = Line(_s.UiScale);
            Point at = input.MousePosition;
            if (list.Contains(at))
            {
                int i = _scroll + (at.Y - list.Y) / line;
                if (i >= 0 && i < AllItems.Count && AllItems[i].Selectable)
                {
                    _sel = i;
                    Activate(i, input.RightPressed ? -1 : 1);
                }
            }
            else if (input.LeftPressed && back.Contains(at))
            {
                _s.Screens.Pop();
            }
        }

        input.ConsumeKeyboard();
        input.ConsumeMouse();
    }

    private void UpdateRebind(InputFrame input)
    {
        if (input.LeftPressed || input.RightPressed)
        {
            _rebind.Cancel();   // a click elsewhere leaves the flow
            return;
        }

        if (_rebind.State == RebindSession.Phase.Conflict)
        {
            if (input.ConsumePressed(Keys.Escape)) _rebind.Cancel();
            else if (input.ConsumePressed(Keys.Enter) && _rebind.Swap(_s.Settings.Bindings) is { } swapped) Commit(swapped);
            return;
        }

        foreach (Keys key in input.Keyboard.GetPressedKeys())
        {
            if (!input.Pressed(key)) continue;
            if (_rebind.Press(_s.Settings.Bindings, key) is { } changed) Commit(changed);
            return;
        }
    }

    private void Commit(KeyBindings bindings)
    {
        _s.Settings = _s.Settings.WithBindings(bindings);
        MenuSounds.Play(_s.Audio, MenuSounds.Move);
    }

    private void MoveSelection(int dir)
    {
        for (int step = 1; step <= AllItems.Count; step++)
        {
            int i = ((_sel + dir * step) % AllItems.Count + AllItems.Count) % AllItems.Count;
            if (!AllItems[i].Selectable) continue;
            _sel = i;
            break;
        }

        if (_sel < _scroll) _scroll = _sel - (AllItems[Math.Max(0, _sel - 1)].Kind == ItemKind.Heading ? 1 : 0);
        if (_sel >= _scroll + _visibleLines) _scroll = _sel - _visibleLines + 1;
        _scroll = Math.Max(0, _scroll);
    }

    private void Activate(int index, int direction)
    {
        Item item = AllItems[index];
        switch (item.Kind)
        {
            case ItemKind.Setting:
                _s.Settings = Step(_s.Settings, item.Index, direction);
                MenuSounds.Play(_s.Audio, MenuSounds.Move);
                break;
            case ItemKind.Reset:
                _s.Settings = ResetKeys(_s.Settings);
                MenuSounds.Play(_s.Audio, MenuSounds.Move);
                break;
            case ItemKind.Binding when direction > 0 && item.Row?.Action is { } action:
                _rebind.Begin(action);
                break;
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillDim(sb, _s);
        int scale = _s.UiScale;
        int line = Line(scale);
        int pad = 5 * scale;
        Layout(out Rectangle list, out Rectangle back, out Rectangle panel);
        Shapes.Fill(sb, _s.Pixel, panel, PanelColour);
        Shapes.Outline(sb, _s.Pixel, panel, BorderColour, Math.Max(1, scale / 2));
        BitmapFont.Draw(sb, _s.Pixel, "Controls", new Vector2(panel.X + pad, panel.Y + pad), TitleColour, scale, FontWeight.Bold);

        KeyBindings bindings = _s.Settings.Bindings;
        _visibleLines = Math.Max(3, list.Height / line);
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, AllItems.Count - _visibleLines));
        int keyCol = 0;
        foreach (ControlRow r in GameKeys.Rows) keyCol = Math.Max(keyCol, BitmapFont.Measure(r.LabelFor(KeyBindings.Default), scale));
        keyCol = Math.Max(keyCol, BitmapFont.Measure("Left click (attack-move armed)", scale)) + 12 * scale;
        for (int i = 0; i < _visibleLines && _scroll + i < AllItems.Count; i++)
        {
            int index = _scroll + i;
            Item item = AllItems[index];
            var at = new Vector2(list.X, list.Y + i * line);
            var rowRect = new Rectangle(list.X, list.Y + i * line, list.Width, line);
            if (index == _sel && item.Selectable) Shapes.Fill(sb, _s.Pixel, rowRect, SelectColour);
            switch (item.Kind)
            {
                case ItemKind.Setting:
                    BitmapFont.Draw(sb, _s.Pixel, RowText(_s.Settings, item.Index), at + new Vector2(2 * scale, 0), TextColour, scale);
                    break;
                case ItemKind.Reset:
                    BitmapFont.Draw(sb, _s.Pixel, "Reset all keys to defaults", at + new Vector2(2 * scale, 0), bindings.IsAllDefault ? HintColour : KeyColour, scale);
                    break;
                case ItemKind.Heading:
                    BitmapFont.Draw(sb, _s.Pixel, item.Heading, at, TitleColour, scale, FontWeight.Bold);
                    break;
                default:
                    DrawRow(sb, item.Row!, bindings, at, keyCol, scale);
                    break;
            }
        }

        int maxScroll = Math.Max(0, AllItems.Count - _visibleLines);
        if (maxScroll > 0)
        {
            Rectangle track = ScrollTrack(list, scale);
            Shapes.Fill(sb, _s.Pixel, track, SelectColour);
            Rectangle thumb = ThumbRect(track, AllItems.Count, _visibleLines, _scroll);
            Shapes.Fill(sb, _s.Pixel, thumb, BorderColour);
        }

        string message = _rebind.Message;
        if (_rebind.State == RebindSession.Phase.Listening && message.Length == 0) message = "Press the new key for this action.  Esc: cancel.";
        string hint = message.Length > 0 ? message : "Up/Down select   Enter or click: change / rebind   Wheel: scroll   Esc: back";
        if (maxScroll > 0 && _scroll < maxScroll && message.Length == 0) hint = "More below.  " + hint;
        int room = back.X - panel.X - 2 * pad;
        while (BitmapFont.Measure(hint, scale) > room && hint.LastIndexOf("   ", StringComparison.Ordinal) is var cut and > 0) hint = hint[..cut];
        Color hintColour = _rebind.State == RebindSession.Phase.Conflict ? WarnColour : message.Length > 0 ? KeyColour : HintColour;
        BitmapFont.Draw(sb, _s.Pixel, hint, new Vector2(panel.X + pad, panel.Bottom - pad - line + scale), hintColour, scale);
        Shapes.Fill(sb, _s.Pixel, back, SelectColour);
        Shapes.Outline(sb, _s.Pixel, back, BorderColour);
        BitmapFont.Draw(sb, _s.Pixel, "Back", new Vector2(back.X + 3 * scale, back.Y + scale), TextColour, scale);
    }

    /// <summary>The scrollbar track rectangle at the right edge of the list.</summary>
    public static Rectangle ScrollTrack(Rectangle list, int scale) => new(list.Right + scale, list.Y, 3 * scale, list.Height);

    /// <summary>The scrollbar thumb inside <paramref name="track"/> for <paramref name="total"/> lines, <paramref name="visible"/> shown, scrolled by <paramref name="scroll"/>.</summary>
    public static Rectangle ThumbRect(Rectangle track, int total, int visible, int scroll)
    {
        if (total <= visible || total <= 0) return track;
        int h = Math.Max(track.Width * 2, track.Height * visible / total);
        int max = total - visible;
        int y = track.Y + (track.Height - h) * Math.Clamp(scroll, 0, max) / max;
        return new Rectangle(track.X, y, track.Width, h);
    }

    private void DrawRow(SpriteBatch sb, ControlRow row, KeyBindings bindings, Vector2 at, int keyCol, int scale)
    {
        bool editing = _rebind.State != RebindSession.Phase.Idle && row.Action == _rebind.Action;
        string label = editing ? (_rebind.State == RebindSession.Phase.Conflict ? KeyNames.Of(_rebind.PendingKey) + " ?" : "press a key") : row.LabelFor(bindings);
        Color colour = editing ? WarnColour : row.Action is { } a && !bindings.IsDefault(a) ? ChangedColour : KeyColour;
        BitmapFont.Draw(sb, _s.Pixel, label, at + new Vector2(2 * scale, 0), colour, scale);
        BitmapFont.Draw(sb, _s.Pixel, row.Description, at + new Vector2(keyCol, 0), TextColour, scale);
    }

    private static List<Item> BuildItems()
    {
        var items = new List<Item>();
        for (int i = 0; i < SettingCount; i++) items.Add(new Item(ItemKind.Setting, i));
        items.Add(new Item(ItemKind.Reset));
        foreach (string group in GameKeys.Groups)
        {
            items.Add(new Item(ItemKind.Heading, Heading: group));
            foreach (ControlRow row in GameKeys.InGroup(group)) items.Add(new Item(row.Action is null ? ItemKind.Fixed : ItemKind.Binding, Row: row));
        }

        return items;
    }

    private static int Line(int scale) => BitmapFont.LineHeight(scale, FontWeight.Regular) + scale;

    private void Layout(out Rectangle list, out Rectangle back, out Rectangle panel)
    {
        int scale = _s.UiScale;
        Viewport vp = _s.Viewport;
        int line = Line(scale);
        int pad = 5 * scale;
        int width = Math.Min(vp.Width - 8 * scale, 140 * scale * BitmapFont.Advance / 2);
        int height = vp.Height - 4 * scale;
        panel = new Rectangle((vp.Width - width) / 2, (vp.Height - height) / 2, width, height);
        int top = panel.Y + pad + line + scale;
        int bottom = panel.Bottom - pad - line - scale;
        list = new Rectangle(panel.X + pad, top, width - 2 * pad - 5 * scale, Math.Max(line * 3, bottom - top));
        int backW = BitmapFont.Measure("Back", scale) + 8 * scale;
        back = new Rectangle(panel.Right - pad - backW, panel.Bottom - pad - line, backW, line);
    }
}
