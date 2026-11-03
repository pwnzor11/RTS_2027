using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Sim.Core;

namespace Rts.Game;

/// <summary>
/// Window and main loop for the RTS skeleton. Draws snapshots from an <see cref="ISnapshotSource"/>
/// and never calls the simulation. Controls: WASD or arrows and middle-drag to pan, wheel to zoom,
/// F1 to toggle the overlay, Esc to quit.
/// </summary>
public sealed class RtsGame : Microsoft.Xna.Framework.Game
{
    private const float PanScreenPixelsPerSecond = 320f;
    private static readonly Color BackgroundColour = new(0x14, 0x16, 0x11);
    private static readonly Color OverlayPanel = new(0, 0, 0, 170);
    private static readonly Color OverlayBar = new(0xD8, 0xC8, 0x8A);

    private readonly GraphicsDeviceManager _graphics;
    private readonly ISnapshotSource _source;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private GridRenderer _renderer = null!;
    private MouseState _previousMouse;
    private KeyboardState _previousKeys;
    private bool _overlayVisible;

    /// <summary>Creates the game window with its demo snapshot source.</summary>
    public RtsGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true,
        };
        _source = new DemoSnapshotSource(); // M1: replace with Simulation-driven source
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        Window.Title = "RTS_2027 skeleton";
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
        // No content pipeline: every shape is drawn with one runtime-made white pixel.
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _renderer = new GridRenderer(_pixel);
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        KeyboardState keys = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();

        if (keys.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (keys.IsKeyDown(Keys.F1) && _previousKeys.IsKeyUp(Keys.F1))
        {
            _overlayVisible = !_overlayVisible;
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
        _renderer.Draw(_spriteBatch, snapshot, GraphicsDevice.Viewport);

        if (_overlayVisible)
        {
            DrawOverlay(snapshot.Tick);
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    /// <summary>Text-free overlay: a bar that fills once per simulated second.</summary>
    private void DrawOverlay(int tick)
    {
        const int barWidth = 152;
        const int barHeight = 8;
        var panel = new Rectangle(8, 8, barWidth + 16, barHeight + 16);
        _spriteBatch.Draw(_pixel, panel, OverlayPanel);

        int ticksIntoSecond = tick % SimConstants.TicksPerSecond;
        int filled = barWidth * ticksIntoSecond / SimConstants.TicksPerSecond;
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 8, panel.Y + 8, filled, barHeight), OverlayBar);
    }
}
