// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Ui.Alerts;

namespace Rts.Game.Screens;

/// <summary>
/// QOL-12 (D-313): Settings > Alerts. One row per <see cref="AlertCategory"/> with its volume in 25 percent steps; 0 mutes the category (its sound,
/// its line in the alert stack and its minimap ping). Changes apply at once and are saved when the screen closes.
/// </summary>
public sealed class AlertsSettingsScreen : IScreen
{
    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private readonly string? _path;
    private Point _mouse;

    /// <summary>Creates the screen. <paramref name="settingsPath"/> overrides where settings are saved (tests); null = the default file.</summary>
    public AlertsSettingsScreen(GameServices services, string? settingsPath = null)
    {
        _s = services;
        _path = settingsPath;
        var rows = new List<MenuItem>();
        foreach (AlertCategory category in Enum.GetValues<AlertCategory>())
        {
            AlertCategory c = category;
            rows.Add(new MenuItem(() => RowText(_s.Settings, c), null, d => Step(c, d),
                new MenuButton("-", () => Step(c, -1)), new MenuButton("+", () => Step(c, 1))));
        }

        rows.Add(new MenuItem(() => "Back", Back));
        _panel = new MenuPanel("Alerts", rows);
    }

    /// <summary>The text of a category row: "Under attack: 75%" or "Under attack: muted".</summary>
    public static string RowText(Settings settings, AlertCategory category)
    {
        int v = settings.Alerts.Get(category);
        return $"{AlertMix.NameOf(category)}: {(v == 0 ? "muted" : v + "%")}";
    }

    /// <summary>The settings after stepping a category's volume by <paramref name="direction"/> (+1 / -1; 25 percent a step, clamped to 0..100).</summary>
    public static Settings StepVolume(Settings settings, AlertCategory category, int direction) =>
        settings.WithAlerts(settings.Alerts.With(category, AlertMix.Stepped(settings.Alerts.Get(category), direction)));

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

    private void Step(AlertCategory category, int direction)
    {
        _s.Settings = StepVolume(_s.Settings, category, direction);
        MenuSounds.Play(_s.Audio, MenuSounds.Move);
    }

    private void Back() => _s.Screens.Pop();
}
