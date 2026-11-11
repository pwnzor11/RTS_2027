// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Audio;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Screens;
using Rts.Game.Sprites;

namespace Rts.Game;

/// <summary>
/// The window shell: loads settings and shared content, collects input into an <see cref="InputFrame"/> each Update,
/// and hands update and draw to the <see cref="ScreenStack"/>. Everything the player sees lives in the screens
/// (main menu, skirmish setup, game, pause, game over).
/// </summary>
public sealed class RtsGame : Microsoft.Xna.Framework.Game
{
    private const string TankSheetFile = "tank_v2.sprite.toml";
    private static readonly Color BackgroundColour = new(0x14, 0x16, 0x11);

    private readonly GraphicsDeviceManager _graphics;
    private readonly string _dataDir;
    private readonly string _rulesDir;
    private readonly Settings _settings;
    private readonly bool _demo;
    private readonly bool _direct;
    private readonly bool _empty;
    private readonly ScreenStack _screens = new();
    private readonly FocusPause _focusPause = new();
    private readonly List<char> _typed = new();
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private GameServices _services = null!;
    private KeyboardState _previousKeys;
    private MouseState _previousMouse;
    private int _framesCounted;
    private double _fpsWindowSeconds;

    /// <summary>
    /// Creates the window. With <paramref name="demo"/> it opens the fabricated demo source instead of the sim. With
    /// <paramref name="empty"/> the sandbox uses the legacy 3-tank setup. With <paramref name="direct"/> (or demo) it
    /// starts in the game and skips the main menu.
    /// </summary>
    public RtsGame(bool demo, bool empty, bool direct = false)
    {
        _demo = demo;
        _empty = empty;
        _direct = direct;
        _settings = Settings.Load();
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _settings.WindowWidth,
            PreferredBackBufferHeight = _settings.WindowHeight,
            IsFullScreen = _settings.Fullscreen,
            SynchronizeWithVerticalRetrace = true,
        };

        _rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException($"data/rules not found above {AppContext.BaseDirectory}");
        _dataDir = Path.GetDirectoryName(_rulesDir)!;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        Window.Title = "RTS_2027";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        Window.TextInput += (_, e) => _typed.Add(e.Character);

        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromTicks(166_667); // 60 Hz draw

        _graphics.ApplyChanges();
        base.Initialize();
    }

    /// <inheritdoc />
    protected override void LoadContent()
    {
        // No content pipeline: shapes use one runtime-made white pixel; the vehicle sheet is read from data/sprites.
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Sprites by rules id from data/sprites (M2-17); fall back to the tank sheet for every vehicle if the library fails.
        SceneRenderer scene;
        SpriteLibrary? spriteLibrary = null;
        try
        {
            // S18 GAMEREND-02: a bad manifest or sheet is skipped and logged, the rest still load.
            SpriteLibrary library = SpriteLibrary.ScanTolerant(Path.Combine(_dataDir, "sprites"), Console.Error.WriteLine);
            library.LoadAll(GraphicsDevice, Console.Error.WriteLine);
            spriteLibrary = library;
            scene = new SceneRenderer(library);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or InvalidOperationException)
        {
            Console.Error.WriteLine($"sprite library failed, using the legacy tank sheet: {ex.Message}");
            scene = new SceneRenderer(SpriteSheet.Load(GraphicsDevice, Path.Combine(_dataDir, "sprites", "vehicles", TankSheetFile)));
        }
        IAudio audio = AudioBootstrap.Create(this, _dataDir, _settings, _screens);
        AudioSettings.Apply(audio, _settings);
        _services = new GameServices(GraphicsDevice, _pixel, scene, _rulesDir, audio, _settings, _screens, Exit)
        {
            LegacyEmptySandbox = _empty,
            Sprites = spriteLibrary,
            ApplyDisplay = ApplyDisplay,
            WindowActive = () => IsActive,
            ClientSize = () => new Point(Window.ClientBounds.Width, Window.ClientBounds.Height),
        };
        // The animated menu background (M3-16). It is optional: if it cannot be built the menus use a plain colour.
        try
        {
            _services.Background = new MenuBackground(GraphicsDevice, _pixel, spriteLibrary, _dataDir);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException)
        {
            Console.Error.WriteLine($"menu background disabled: {ex.Message}");
        }

        if (_demo) _screens.Push(GameScreen.CreateDemo(_services));
        else if (_direct) _screens.Push(GameScreen.CreateSandbox(_services, _empty, null));
        else _screens.Push(new MainMenuScreen(_services));
    }

    // Settings > Full screen / Window size: apply to the live window.
    private void ApplyDisplay(Settings settings)
    {
        _graphics.IsFullScreen = settings.Fullscreen;
        _graphics.PreferredBackBufferWidth = settings.WindowWidth;
        _graphics.PreferredBackBufferHeight = settings.WindowHeight;
        _graphics.ApplyChanges();
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        KeyboardState keys = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();
        var input = new InputFrame(keys, _previousKeys, mouse, _previousMouse, _typed);
        _typed.Clear();

        _focusPause.Update(IsActive, _screens);   // S19: a single-player game pauses while the window is in the background
        _screens.Update(input, gameTime);

        _previousKeys = keys;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BackgroundColour);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _screens.Draw(_spriteBatch);
        _spriteBatch.End();

        _framesCounted++;
        _fpsWindowSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        if (_fpsWindowSeconds >= 1.0)
        {
            _services.Fps = _framesCounted;
            _framesCounted = 0;
            _fpsWindowSeconds = 0;
        }

        base.Draw(gameTime);
    }

    /// <inheritdoc />
    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        _screens.Clear(); // every screen gets its Exit; a running game saves its replay
        base.OnExiting(sender, args);
    }
}
