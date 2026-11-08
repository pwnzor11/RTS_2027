// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Audio;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Sprites;

namespace Rts.Game.Screens;

/// <summary>What every screen needs from the shell: graphics, audio, settings, the stack itself and a way to quit.</summary>
public sealed class GameServices
{
    /// <summary>Creates the holder. <paramref name="quit"/> asks the shell to close the window.</summary>
    public GameServices(GraphicsDevice graphics, Texture2D pixel, SceneRenderer scene, string rulesDir, IAudio audio,
        Settings settings, ScreenStack screens, Action quit)
    {
        Graphics = graphics;
        Pixel = pixel;
        Scene = scene;
        RulesDir = rulesDir;
        Audio = audio;
        _settings = settings;
        Ui.UiScale.TextFactor = Ui.UiScale.FactorOf(settings.TextSize);
        Controls.GameKeys.Active = settings.Bindings;
        Screens = screens;
        Quit = quit;
    }

    /// <summary>The graphics device (viewport size).</summary>
    public GraphicsDevice Graphics { get; }

    /// <summary>A 1x1 white texture for shapes and text.</summary>
    public Texture2D Pixel { get; }

    /// <summary>Draws the map, units and overlays.</summary>
    public SceneRenderer Scene { get; }

    /// <summary>The data/rules directory.</summary>
    public string RulesDir { get; }

    /// <summary>Sound output (a null implementation until the audio package lands).</summary>
    public IAudio Audio { get; }

    /// <summary>The player's settings.</summary>
    public Settings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            Ui.UiScale.TextFactor = Ui.UiScale.FactorOf(value.TextSize);   // the Text size setting reaches every font at once
            Controls.GameKeys.Active = value.Bindings;   // game input reads its keys through the bindings in force
        }
    }

    private Settings _settings;

    /// <summary>The screen stack.</summary>
    public ScreenStack Screens { get; }

    /// <summary>Asks the shell to close the window.</summary>
    public Action Quit { get; }

    /// <summary>The animated menu background, or null in headless runs (menu screens then fill a plain colour).</summary>
    public MenuBackground? Background { get; set; }

    /// <summary>The loaded sprite library (the How to Play illustrations draw from it), or null.</summary>
    public SpriteLibrary? Sprites { get; set; }

    /// <summary>Asks the shell to apply window settings (full screen, window size) now. Null when there is no window to change.</summary>
    public Action<Settings>? ApplyDisplay { get; set; }

    /// <summary>True when the program was started with --empty: the Sandbox menu entry then uses the legacy 3-tank setup.</summary>
    public bool LegacyEmptySandbox { get; set; }

    /// <summary>True while the game window has focus (the shell sets it); edge scrolling is off otherwise.</summary>
    public Func<bool> WindowActive { get; set; } = static () => true;

    /// <summary>The window's client size in pixels (the shell sets it); zero = unknown, the back buffer size is used. Edge scroll measures its band against it (M11-10b).</summary>
    public Func<Microsoft.Xna.Framework.Point> ClientSize { get; set; } = static () => Microsoft.Xna.Framework.Point.Zero;

    /// <summary>Frames drawn in the last second, set by the shell.</summary>
    public int Fps { get; set; }

    /// <summary>The window viewport now.</summary>
    public Viewport Viewport => Graphics.Viewport;

    /// <summary>THE UI scale for the current window and settings: automatic from the height times the UI scale setting, clamped to what fits.</summary>
    public Ui.UiScaleResult UiScaleInfo => Settings.ResolveUiScale(Viewport.Width, Viewport.Height);

    /// <summary>The effective UI scale as a number (1 = a 12 px font and a 20 px sidebar row). Sidebar, card, alerts and objectives lay out from this.</summary>
    public float UiFactor => UiScaleInfo.Scale;

    /// <summary>The same scale for screens that lay out in 5x7 font pixels (menus, pages): fractional, but whole-pixel in arithmetic. See <see cref="Ui.UiUnit"/>.</summary>
    public Ui.UiUnit UiScale => Ui.UiUnit.FromScale(UiFactor);
}
