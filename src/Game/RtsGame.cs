using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Sprites;
using Rts.Game.Ui;
using Rts.Sim.Core;

namespace Rts.Game;

/// <summary>
/// Window and main loop for the M1 debug view. Shows the real simulation (or the --demo source) and never steps it
/// itself. Controls: WASD or arrows and middle-drag to pan, wheel to zoom, left-click to select an owned unit or
/// drag a box, right-click the ground to move the selection, Space to pause, '.' to step while paused, Esc to quit.
/// </summary>
public sealed class RtsGame : Microsoft.Xna.Framework.Game
{
    private const float PanScreenPixelsPerSecond = 320f;
    private const float DragThresholdPx = 4f;
    private const int HudScale = 2;
    private const string TankSheetFile = "tank_v2.toml";
    private static readonly Color BackgroundColour = new(0x14, 0x16, 0x11);
    private static readonly Color HudPanel = new(0, 0, 0, 170);
    private static readonly Color HudText = new(0xD8, 0xC8, 0x8A);

    private readonly GraphicsDeviceManager _graphics;
    private readonly string _dataDir;
    private readonly ISnapshotSource _source;
    private readonly SimSnapshotSource? _sim; // null with --demo
    private readonly Selection _selection = new();
    private readonly List<RenderUnit> _renderUnits = new();
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private GridRenderer _renderer = null!;
    private MouseState _mouse;
    private MouseState _previousMouse;
    private KeyboardState _previousKeys;
    private Point? _dragStart;
    private bool _dragging;
    private int _fps;
    private int _framesCounted;
    private double _fpsWindowSeconds;

    /// <summary>Creates the window. With <paramref name="demo"/> it draws the fabricated demo source instead of the sim.</summary>
    /// <param name="demo">True to use <see cref="DemoSnapshotSource"/>.</param>
    public RtsGame(bool demo)
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
            var sim = new SimSnapshotSource(rulesDir);
            _sim = sim;
            _source = sim;
        }
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        Window.Title = "RTS_2027 debug view";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;

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
        _renderer = new GridRenderer(_pixel, sheet);
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        KeyboardState keys = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();
        _mouse = mouse;

        if (keys.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (_sim is not null)
        {
            if (Pressed(keys, Keys.Space)) _sim.Paused = !_sim.Paused;
            if (Pressed(keys, Keys.OemPeriod)) _sim.StepOnce();
        }

        Vector2 keyDirection = Vector2.Zero;
        if (keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left)) keyDirection.X -= 1f;
        if (keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right)) keyDirection.X += 1f;
        if (keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up)) keyDirection.Y -= 1f;
        if (keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down)) keyDirection.Y += 1f;
        if (keyDirection != Vector2.Zero)
        {
            keyDirection.Normalize();
            float seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _renderer.PanScreen(keyDirection * PanScreenPixelsPerSecond * seconds);
        }

        // Middle-drag: the map follows the cursor, so the view moves opposite to the mouse.
        if (mouse.MiddleButton == ButtonState.Pressed && _previousMouse.MiddleButton == ButtonState.Pressed)
        {
            Vector2 mouseDelta = (mouse.Position - _previousMouse.Position).ToVector2();
            _renderer.PanScreen(-mouseDelta);
        }

        int wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            _renderer.ZoomBy(wheelDelta > 0 ? 1 : -1);
        }

        _source.Update();
        _renderer.ClampCamera(_source.Current, GraphicsDevice.Viewport);
        GridRenderer.Interpolate(_source.Current, _source.Previous, _source.Alpha, _renderUnits);

        HandleMouse(mouse);
        _selection.Prune(_source.Current.Units);

        _previousKeys = keys;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BackgroundColour);

        SimSnapshot snapshot = _source.Current;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _renderer.Draw(_spriteBatch, snapshot, GraphicsDevice.Viewport, _renderUnits, _selection, DragRectangle());
        DrawHud(snapshot);
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
                Vector2 a = _renderer.WorldFromScreen(begin);
                Vector2 b = _renderer.WorldFromScreen(mouse.Position);
                _selection.Replace(Selection.Inside(_renderUnits, Vector2.Min(a, b), Vector2.Max(a, b), SimSnapshotSource.LocalPlayer));
            }
            else
            {
                EntityId hit = Selection.Pick(_renderUnits, _renderer.WorldFromScreen(mouse.Position), SimSnapshotSource.LocalPlayer);
                if (hit.IsNone) _selection.Clear();
                else _selection.Replace(new[] { hit });
            }

            _dragStart = null;
            _dragging = false;
        }

        bool rightPressed = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        if (rightPressed && _sim is not null && _selection.Count > 0)
        {
            Vector2 world = _renderer.WorldFromScreen(mouse.Position);
            var cell = new CellCoord((int)MathF.Floor(world.X / GridRenderer.CellPx), (int)MathF.Floor(world.Y / GridRenderer.CellPx));
            _sim.EnqueueMove(_selection.Ids, cell);
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

    /// <summary>Top-left text: tick, state hash, frames per second and zoom. A second line appears while paused.</summary>
    private void DrawHud(SimSnapshot snapshot)
    {
        string line = $"tick {snapshot.Tick}  hash {snapshot.StateHash:x16}  fps {_fps}  zoom {_renderer.ZoomLabel}";
        bool paused = _sim is { Paused: true };
        string? second = paused ? "paused  (. = step)" : null;

        const int LineHeight = BitmapFont.GlyphHeight * HudScale + 4;
        int width = BitmapFont.Measure(line, HudScale);
        if (second is not null) width = Math.Max(width, BitmapFont.Measure(second, HudScale));
        int height = LineHeight * (second is null ? 1 : 2) + 8;
        _spriteBatch.Draw(_pixel, new Rectangle(8, 8, width + 16, height), HudPanel);

        BitmapFont.Draw(_spriteBatch, _pixel, line, new Vector2(16, 12), HudText, HudScale);
        if (second is not null)
        {
            BitmapFont.Draw(_spriteBatch, _pixel, second, new Vector2(16, 12 + LineHeight), HudText, HudScale);
        }
    }
}
