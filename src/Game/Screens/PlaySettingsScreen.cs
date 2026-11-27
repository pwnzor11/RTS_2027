// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;

namespace Rts.Game.Screens;

/// <summary>
/// S47 U-13: Settings > Play options. The on/off and step rows of the quality-of-life features (selection, sidebar, camera, sound in the
/// background, window). Changes apply at once and are saved when the screen closes.
/// </summary>
public sealed class PlaySettingsScreen : IScreen
{
    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private readonly string? _path;
    private Point _mouse;

    /// <summary>Creates the screen. <paramref name="settingsPath"/> overrides where settings are saved (tests); null = the default file.</summary>
    public PlaySettingsScreen(GameServices services, string? settingsPath = null)
    {
        _s = services;
        _path = settingsPath;
        var rows = new List<MenuItem>();
        Toggle(rows, "Sidebar follows selection", s => s.SidebarFollowsSelection, (s, v) => s with { SidebarFollowsSelection = v });
        Toggle(rows, "Box select favours fighters", s => s.BoxSelectFightersFirst, (s, v) => s with { BoxSelectFightersFirst = v });   // S47 cnc#8
        Toggle(rows, "Mute sound in the background", s => s.MuteInBackground, (s, v) => s with { MuteInBackground = v });   // S47 cnc#35
        Toggle(rows, "Pause when the window loses focus", s => s.PauseOnFocusLoss, (s, v) => s with { PauseOnFocusLoss = v });   // S47 X8
        Toggle(rows, "VSync", s => s.VSync, (s, v) => s with { VSync = v });   // S47 cnc#41
        rows.Add(new MenuItem(() => $"Start zoom: {ZoomText(_s.Settings.StartZoomPercent)}", () => StepZoom(1), StepZoom,   // S47 cnc#42
            new MenuButton("<", () => StepZoom(-1)), new MenuButton(">", () => StepZoom(1))));
        rows.Add(new MenuItem(() => "Back", Back));
        _panel = new MenuPanel("Play options", rows);
    }

    /// <summary>The row text of an on/off setting: "Name: On".</summary>
    public static string OnOff(string name, bool on) => $"{name}: {(on ? "On" : "Off")}";

    /// <summary>The start-zoom text: "Wide" (0.5), "Normal" (1) or "Close" (2).</summary>
    public static string ZoomText(int percent) => Settings.SnapStartZoom(percent) switch { 50 => "Wide (0.5)", 200 => "Close (2)", _ => "Normal (1)" };

    /// <summary>The settings with the start zoom stepped by <paramref name="direction"/> (wraps round the three levels).</summary>
    public static Settings StepStartZoom(Settings settings, int direction)
    {
        int at = 0;
        for (int i = 0; i < Settings.StartZooms.Count; i++) if (Settings.StartZooms[i] == Settings.SnapStartZoom(settings.StartZoomPercent)) at = i;
        int n = Settings.StartZooms.Count;
        return settings with { StartZoomPercent = Settings.StartZooms[((at + direction) % n + n) % n] };
    }

    private void StepZoom(int direction)
    {
        _s.Settings = StepStartZoom(_s.Settings, direction);
        MenuSounds.Play(_s.Audio, MenuSounds.Move);
    }

    private void Toggle(List<MenuItem> rows, string name, Func<Settings, bool> get, Func<Settings, bool, Settings> set)
    {
        void Flip()
        {
            _s.Settings = set(_s.Settings, !get(_s.Settings));
            MenuSounds.Play(_s.Audio, MenuSounds.Move);
        }

        rows.Add(new MenuItem(() => OnOff(name, get(_s.Settings)), Flip, _ => Flip(), new MenuButton("Toggle", Flip)));
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter()
    {
    }

    /// <inheritdoc />
    public void Exit() => _s.Settings.Save(_path);

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        if (input.ConsumePressed(Keys.Escape))
        {
            Back();
            return;
        }

        _panel.Update(input, _s.Viewport, _s.UiScale);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
    }

    private void Back() => _s.Screens.Pop();
}
