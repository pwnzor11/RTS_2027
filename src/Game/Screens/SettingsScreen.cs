// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Saves;
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>
/// Settings: full screen, window size, UI scale (75 to 200 %), text size, master / music / effects volume, the Controls page and the About page. Changes apply at once (volume
/// by the audio backend, display through <see cref="GameServices.ApplyDisplay"/>) and are written to settings.toml when the screen
/// closes.
/// </summary>
public sealed class SettingsScreen : IScreen
{
    private const int BarCells = 20;
    private const int VolumeStep = 5;

    /// <summary>The window sizes the Window size row cycles through. ASSUMED (M3-16): common 16:9 sizes.</summary>
    // ASSUMED (M3-16): these four sizes.
    public static readonly IReadOnlyList<(int W, int H)> WindowSizes =
        new[] { (1280, 720), (1600, 900), (1920, 1080), (2560, 1440) };

    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private readonly string? _path;
    private Point _mouse;

    /// <summary>Creates the screen. <paramref name="settingsPath"/> overrides where settings are saved (tests); null = the default file.</summary>
    public SettingsScreen(GameServices services, string? settingsPath = null)
    {
        _s = services;
        _path = settingsPath;
        _panel = new MenuPanel("Settings", new[]
        {
            new MenuItem(() => $"Full screen: {(_s.Settings.Fullscreen ? "On" : "Off")}", ToggleFullscreen, _ => ToggleFullscreen(),
                new MenuButton("Toggle", ToggleFullscreen)),
            new MenuItem(() => $"Window size: {_s.Settings.WindowWidth} x {_s.Settings.WindowHeight}", () => StepWindow(1), StepWindow,
                new MenuButton("<", () => StepWindow(-1)), new MenuButton(">", () => StepWindow(1))),
            new MenuItem(() => UiScaleLine(_s.Settings, _s.UiScaleInfo), () => StepUiScale(1), StepUiScale,
                new MenuButton("<", () => StepUiScale(-1)), new MenuButton(">", () => StepUiScale(1))),
            new MenuItem(() => $"Text size: {UiScale.NameOf(_s.Settings.TextSize)}", () => StepTextSize(1), StepTextSize,
                new MenuButton("<", () => StepTextSize(-1)), new MenuButton(">", () => StepTextSize(1))),
            new MenuItem(() => "Controls", () => _s.Screens.Push(new ControlsScreen(_s, _path))),
            new MenuItem(() => $"Health bars: {(_s.Settings.HealthBarsAlways ? "Always" : "Damaged or selected")}", ToggleHealthBars, _ => ToggleHealthBars(),
                new MenuButton("Toggle", ToggleHealthBars)),
            new MenuItem(() => $"Larger infantry (x1.25): {(_s.Settings.LargerInfantry ? "On" : "Off")}", ToggleLargerInfantry, _ => ToggleLargerInfantry(),
                new MenuButton("Toggle", ToggleLargerInfantry)),   // D-184
            new MenuItem(() => $"Weather: {(_s.Settings.Weather ? "On" : "Off")}", ToggleWeather, _ => ToggleWeather(),
                new MenuButton("Toggle", ToggleWeather)),   // M10-12
            new MenuItem(() => $"Screen shake: {(_s.Settings.ScreenShake ? "On" : "Off")}", ToggleScreenShake, _ => ToggleScreenShake(),
                new MenuButton("Toggle", ToggleScreenShake)),   // M10-14
            Volume("Master volume", s => s.MasterVolume, (s, v) => s with { MasterVolume = v }),
            Volume("Music volume", s => s.MusicVolume, (s, v) => s with { MusicVolume = v }),
            Volume("Effects volume", s => s.SfxVolume, (s, v) => s with { SfxVolume = v }),
            new MenuItem(() => UiText.Format(UiText.AutosaveSetting, "interval", Autosave.IntervalText(_s.Settings.AutosaveMinutes)),
                () => StepAutosave(1), StepAutosave, new MenuButton("<", () => StepAutosave(-1)), new MenuButton(">", () => StepAutosave(1))),   // D-205
            new MenuItem(() => $"Camera cue on new objective: {(_s.Settings.CameraCueOnNewObjective ? "On" : "Off")}", ToggleCameraCue, _ => ToggleCameraCue(),
                new MenuButton("Toggle", ToggleCameraCue)),   // M11-05
            new MenuItem(() => $"Check for updates at start: {(_s.Settings.CheckForUpdates ? "On" : "Off")}", ToggleCheckForUpdates, _ => ToggleCheckForUpdates(),
                new MenuButton("Toggle", ToggleCheckForUpdates)),   // M10-23
            new MenuItem(() => Rts.Game.Ui.Llm.LlmUiText.SectionRow, () => _s.Screens.Push(new Rts.Game.Ui.Llm.LlmSettingsScreen(_s, _path))),
            new MenuItem(() => "About", () => _s.Screens.Push(new AboutScreen(_s))),
            new MenuItem(() => "Credits", () => _s.Screens.Push(new CreditsScreen(_s))),   // M10-20
            new MenuItem(() => "Back", Back),
        });
    }

    /// <summary>The text bar for a volume 0..100, for example <c>[##########----------]  50%</c>.</summary>
    public static string VolumeBar(int volume)
    {
        int filled = (Math.Clamp(volume, 0, 100) * BarCells + 50) / 100;
        return $"[{new string('#', filled)}{new string('-', BarCells - filled)}] {volume,3}%";
    }

    /// <summary>The next window size after the current one in <paramref name="direction"/> (wraps); a size not in the list starts at the first.</summary>
    public static (int W, int H) NextWindowSize(int width, int height, int direction)
    {
        int i = -1;
        for (int k = 0; k < WindowSizes.Count; k++)
            if (WindowSizes[k] == (width, height)) i = k;
        if (i < 0) return WindowSizes[direction >= 0 ? 0 : WindowSizes.Count - 1];
        return WindowSizes[(i + direction + WindowSizes.Count) % WindowSizes.Count];
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

        int before = _panel.Selected;
        _panel.Update(input, _s.Viewport, _s.UiScale);
        if (_panel.Selected != before) MenuSounds.Play(_s.Audio, MenuSounds.Move);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
    }

    private MenuItem Volume(string name, Func<Settings, int> get, Func<Settings, int, Settings> set)
    {
        void Step(int d) => SetVolume(set, Math.Clamp(get(_s.Settings) + d * VolumeStep, 0, 100));
        return new MenuItem(() => $"{name}: {VolumeBar(get(_s.Settings))}", null, Step,
            new MenuButton("-", () => Step(-1)), new MenuButton("+", () => Step(1)));
    }

    private void SetVolume(Func<Settings, int, Settings> set, int value)
    {
        _s.Settings = set(_s.Settings, value);
        AudioSettings.Apply(_s.Audio, _s.Settings);
        MenuSounds.Play(_s.Audio, MenuSounds.Move);
    }

    private void ToggleCheckForUpdates() =>
        _s.Settings = _s.Settings with { CheckForUpdates = !_s.Settings.CheckForUpdates };

    private void StepAutosave(int d) =>
        _s.Settings = _s.Settings with { AutosaveMinutes = Autosave.StepMinutes(_s.Settings.AutosaveMinutes, d) };

    private void ToggleCameraCue() => _s.Settings = _s.Settings with { CameraCueOnNewObjective = !_s.Settings.CameraCueOnNewObjective };

    private void ToggleFullscreen()
    {
        _s.Settings = _s.Settings with { Fullscreen = !_s.Settings.Fullscreen };
        _s.ApplyDisplay?.Invoke(_s.Settings);
    }

    private void ToggleLargerInfantry() => _s.Settings = _s.Settings with { LargerInfantry = !_s.Settings.LargerInfantry };

    private void ToggleWeather() => _s.Settings = _s.Settings with { Weather = !_s.Settings.Weather };

    private void ToggleScreenShake() => _s.Settings = _s.Settings with { ScreenShake = !_s.Settings.ScreenShake };

    private void ToggleHealthBars() => _s.Settings = _s.Settings with { HealthBarsAlways = !_s.Settings.HealthBarsAlways };

    private void StepWindow(int d)
    {
        (int w, int h) = NextWindowSize(_s.Settings.WindowWidth, _s.Settings.WindowHeight, d);
        _s.Settings = _s.Settings with { WindowWidth = w, WindowHeight = h };
        _s.ApplyDisplay?.Invoke(_s.Settings);
    }

    // M11-10h: the UI scale and text size apply at once (every screen reads the one rule each frame) and are saved when this screen closes.
    private void StepUiScale(int d)
    {
        IReadOnlyList<int> steps = UiScale.Percents;
        int i = 0;
        for (int k = 0; k < steps.Count; k++)
            if (steps[k] == UiScale.SnapPercent(_s.Settings.UiScalePercent)) i = k;
        _s.Settings = _s.Settings with { UiScalePercent = steps[(i + d + steps.Count) % steps.Count] };
    }

    private void StepTextSize(int d) =>
        _s.Settings = _s.Settings with { TextSize = (TextSize)(((int)_s.Settings.TextSize + d + 4) % 4) };

    /// <summary>The UI scale row: "UI scale: 150%", with "(limited by window size)" when the window cannot hold that scale.</summary>
    public static string UiScaleLine(Settings settings, UiScaleResult result)
    {
        string line = $"UI scale: {settings.UiScalePercent}%";
        return result.Limited ? $"{line} (limited by window size)" : line;
    }

    private void Back()
    {
        MenuSounds.Play(_s.Audio, MenuSounds.Back);
        _s.Screens.Pop();
    }
}
