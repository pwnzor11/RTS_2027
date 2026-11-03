using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Game.Ui;
using Rts.Sim.Core;

namespace Rts.Game;

/// <summary>
/// Window and main loop for the M1 battle sandbox. Shows the real simulation (or the --demo source) and steps it only
/// through <see cref="SimSnapshotSource"/>. Controls: arrows or middle-drag to pan, wheel to zoom, left-click to select
/// or drag a box, right-click to move, attack or (after A) attack-move, S stop, G guard, X scatter, F2 fog viewer,
/// F3 occupancy, F4 paths, F5 ranges, Space pause, '.' step, Esc quit (which saves the replay).
/// F1 opens the debug console (sandbox only); while it is open, typing goes to it and Esc closes it instead of quitting.
/// </summary>
public sealed class RtsGame : Microsoft.Xna.Framework.Game
{
    private const float PanScreenPixelsPerSecond = 320f;
    private const float DragThresholdPx = 4f;
    private const int HudScale = 2;
    private const int HelpScale = 2;
    private const string TankSheetFile = "tank_v2.toml";
    private static readonly Color BackgroundColour = new(0x14, 0x16, 0x11);
    private static readonly Keys[] OrderKeys = { Keys.A, Keys.S, Keys.G, Keys.X };
    private static readonly string[] HelpLines =
    {
        "arrows / middle-drag pan   wheel zoom   LMB select   RMB move or attack",
        "A then RMB: attack-move   S stop   G guard here   X scatter   Space pause   . step",
        "F1 console   F2 fog viewer   F3 occupancy   F4 paths   F5 ranges   Esc quit (saves the replay)",
    };

    private readonly GraphicsDeviceManager _graphics;
    private readonly string _dataDir;
    private readonly ISnapshotSource _source;
    private readonly SimSnapshotSource? _sim; // null with --demo
    private readonly Selection _selection = new();
    private readonly OrderController? _orders; // null with --demo
    private readonly OverlayToggles _overlays = new();
    private readonly Camera _camera = new();
    private readonly DebugConsole? _console; // null with --demo
    private bool _escapeClaimed; // Esc closed the console; no quit until Esc is released
    private readonly List<RenderUnit> _renderUnits = new();
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private SceneRenderer _scene = null!;
    private MouseState _mouse;
    private MouseState _previousMouse;
    private KeyboardState _previousKeys;
    private Point? _dragStart;
    private bool _dragging;
    private int _fps;
    private int _framesCounted;
    private double _fpsWindowSeconds;

    /// <summary>
    /// Creates the window. With <paramref name="demo"/> it draws the fabricated demo source instead of the sim. With
    /// <paramref name="empty"/> the sim starts with the legacy 3-tank setup instead of the sandbox.
    /// </summary>
    public RtsGame(bool demo, bool empty)
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true,
        };

        string rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException($"data/rules not found above {AppContext.BaseDirectory}");
        _dataDir = Path.GetDirectoryName(rulesDir)!;

        if (demo)
        {
            _source = new DemoSnapshotSource();
        }
        else
        {
            var sim = new SimSnapshotSource(rulesDir, empty);
            _sim = sim;
            _source = sim;
            _orders = new OrderController(sim, _selection);
            _console = new DebugConsole(sim, _selection, _camera);
            Exiting += (_, _) => { sim.SaveReplay(); };
        }
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        Window.Title = "RTS_2027 battle sandbox";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        Window.TextInput += OnTextInput;

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

        SpriteSheet sheet = SpriteSheet.Load(GraphicsDevice, Path.Combine(_dataDir, "sprites", "vehicles", TankSheetFile));
        _scene = new SceneRenderer(sheet);
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        KeyboardState keys = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();
        _mouse = mouse;

        bool escapeUsedByConsole = false;
        if (_console is { } console)
        {
            if (Pressed(keys, Keys.F1)) console.Toggle();
            else if (console.IsOpen) escapeUsedByConsole = console.Update(keys, _previousKeys, mouse.Position);
        }

        if (keys.IsKeyUp(Keys.Escape)) _escapeClaimed = false;
        if (escapeUsedByConsole) _escapeClaimed = true;
        bool typing = _console is { IsOpen: true };

        if (keys.IsKeyDown(Keys.Escape) && !typing && !_escapeClaimed)
        {
            Exit();
        }

        if (_sim is not null && !typing)
        {
            if (Pressed(keys, Keys.Space)) _sim.Paused = !_sim.Paused;
            if (Pressed(keys, Keys.OemPeriod)) _sim.StepOnce();
            if (Pressed(keys, Keys.F2)) _sim.CycleViewer();
        }

        if (!typing && Pressed(keys, Keys.F3)) _overlays.Occupancy = !_overlays.Occupancy;
        if (!typing && Pressed(keys, Keys.F4)) _overlays.Paths = !_overlays.Paths;
        if (!typing && Pressed(keys, Keys.F5)) _overlays.Ranges = !_overlays.Ranges;

        if (_orders is not null && !typing)
        {
            foreach (Keys key in OrderKeys)
            {
                if (Pressed(keys, key)) _orders.KeyPressed(key);
            }
        }

        Vector2 keyDirection = Vector2.Zero;
        if (keys.IsKeyDown(Keys.Left)) keyDirection.X -= 1f;
        if (keys.IsKeyDown(Keys.Right)) keyDirection.X += 1f;
        if (keys.IsKeyDown(Keys.Up)) keyDirection.Y -= 1f;
        if (keys.IsKeyDown(Keys.Down)) keyDirection.Y += 1f;
        if (keyDirection != Vector2.Zero && !typing)
        {
            keyDirection.Normalize();
            float seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _camera.PanScreen(keyDirection * PanScreenPixelsPerSecond * seconds);
        }

        // Middle-drag: the map follows the cursor, so the view moves opposite to the mouse.
        if (mouse.MiddleButton == ButtonState.Pressed && _previousMouse.MiddleButton == ButtonState.Pressed)
        {
            Vector2 mouseDelta = (mouse.Position - _previousMouse.Position).ToVector2();
            _camera.PanScreen(-mouseDelta);
        }

        int wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            _camera.ZoomBy(wheelDelta > 0 ? 1 : -1);
        }

        _source.Update();
        _camera.Clamp(_source.Current.MapWidth, _source.Current.MapHeight, GraphicsDevice.Viewport);
        UnitInterpolator.Build(_source.Current, _source.Previous, _source.Alpha, _renderUnits);

        if (_sim is { } live)
        {
            _selection.Prune(key => live.IsOwnedAlive(key, SimSnapshotSource.LocalPlayer));
        }
        else
        {
            SimSnapshot shown = _source.Current;
            _selection.Prune(key => shown.Units.Any(unit => UnitKey.Of(unit) == key));
        }

        HandleMouse(mouse);

        _previousKeys = keys;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BackgroundColour);

        SimSnapshot snapshot = _source.Current;
        IReadOnlyList<LeptonCoord> projectiles = _sim is null ? Array.Empty<LeptonCoord>() : _sim.Projectiles;
        var frame = new SceneFrame(snapshot, _renderUnits, projectiles, _sim?.Sim, _sim?.Rules, _overlays, _selection, DragRectangle());

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _scene.Draw(_spriteBatch, _pixel, frame, _camera, GraphicsDevice.Viewport);
        DrawHud(snapshot);
        _console?.Draw(_spriteBatch, _pixel, GraphicsDevice.Viewport);
        _spriteBatch.End();

        _framesCounted++;
        _fpsWindowSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        if (_fpsWindowSeconds >= 1.0)
        {
            _fps = _framesCounted;
            _framesCounted = 0;
            _fpsWindowSeconds = 0;
        }

        base.Draw(gameTime);
    }

    // Text typed in the window goes to the debug console while it is open.
    private void OnTextInput(object? sender, TextInputEventArgs e) => _console?.OnTextInput(e.Character);

    // True only on the frame the key goes down (edge detection against the state saved at the end of the last Update).
    private bool Pressed(KeyboardState keys, Keys key) => keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);

    private void HandleMouse(MouseState mouse)
    {
        bool leftDown = mouse.LeftButton == ButtonState.Pressed;
        bool leftWasDown = _previousMouse.LeftButton == ButtonState.Pressed;

        if (leftDown && !leftWasDown)
        {
            _dragStart = mouse.Position;
            _dragging = false;
        }
        else if (leftDown && _dragStart is Point start && !_dragging)
        {
            _dragging = Vector2.Distance(start.ToVector2(), mouse.Position.ToVector2()) > DragThresholdPx;
        }
        else if (!leftDown && leftWasDown && _dragStart is Point begin)
        {
            if (_dragging)
            {
                Vector2 a = _camera.WorldFromScreen(begin);
                Vector2 b = _camera.WorldFromScreen(mouse.Position);
                _selection.Replace(Selection.Inside(_renderUnits, Vector2.Min(a, b), Vector2.Max(a, b), SimSnapshotSource.LocalPlayer));
            }
            else
            {
                UnitKey? hit = Selection.Pick(_renderUnits, _camera.WorldFromScreen(mouse.Position), SimSnapshotSource.LocalPlayer);
                if (hit is UnitKey key) _selection.Replace(new[] { key });
                else _selection.Clear();
            }

            _dragStart = null;
            _dragging = false;
        }

        bool rightPressed = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        if (rightPressed && _orders is not null && _console is not { IsOpen: true })
        {
            _orders.RightClick(mouse.Position, _camera, _renderUnits);
        }
    }

    private Rectangle? DragRectangle()
    {
        if (!_dragging || _dragStart is not Point start) return null;
        int left = Math.Min(start.X, _mouse.Position.X);
        int top = Math.Min(start.Y, _mouse.Position.Y);
        int right = Math.Max(start.X, _mouse.Position.X);
        int bottom = Math.Max(start.Y, _mouse.Position.Y);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Top-left: tick, state hash, frames per second, zoom (and PAUSED). Second line: alive units per player, selection,
    /// overlays, fog viewer. Bottom-left: the key help.
    /// </summary>
    private void DrawHud(SimSnapshot snapshot)
    {
        var lines = new List<string>
        {
            $"tick {snapshot.Tick}  hash {snapshot.StateHash:x16}  fps {_fps}  zoom {_camera.ZoomLabel}"
                + (_sim is { Paused: true } ? "  PAUSED (. = step)" : ""),
        };

        if (_sim is not null && _orders is not null)
        {
            int p0 = _sim.AliveUnits(0).Count;
            int p1 = _sim.AliveUnits(1).Count;
            string viewer = _sim.Viewer < 0 ? "omniscient" : _sim.Viewer.ToString();
            string order = _orders.AttackMoveArmed ? "  ATTACK-MOVE" : "";
            lines.Add($"p0 {p0} p1 {p1}  selected {_selection.Count}  [{_overlays.Describe()}]  viewer {viewer}{order}");
        }

        Hud.Panel(_spriteBatch, _pixel, new Point(8, 8), lines, HudScale);

        if (_sim is not null)
        {
            int top = GraphicsDevice.Viewport.Height - Hud.HeightOf(HelpLines.Length, HelpScale) - 8;
            Hud.Panel(_spriteBatch, _pixel, new Point(8, top), HelpLines, HelpScale);
        }
    }
}
