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
        Settings = settings;
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
    public Settings Settings { get; set; }

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

    /// <summary>Frames drawn in the last second, set by the shell.</summary>
    public int Fps { get; set; }

    /// <summary>The window viewport now.</summary>
    public Viewport Viewport => Graphics.Viewport;

    /// <summary>UI scale for the current window size (see <see cref="Settings.EffectiveUiScale"/>).</summary>
    public int UiScale => Settings.EffectiveUiScale(Viewport.Height);
}
