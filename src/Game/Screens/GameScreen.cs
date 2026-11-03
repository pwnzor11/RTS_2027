// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Ui;
using Rts.Sim.Core;

namespace Rts.Game.Screens;

/// <summary>
/// The battle view (formerly the whole of RtsGame): camera, selection, orders, overlays, HUD and the F1 debug console
/// over the real simulation (or the --demo source when there is no sim). Controls: arrows or middle-drag to pan, wheel
/// to zoom, left-click to select or drag a box, right-click to move, attack or (after A) attack-move, S stop, G guard,
/// X scatter, F2 fog viewer, F3 occupancy, F4 paths, F5 ranges, Space pause, '.' step, Esc opens the pause menu.
/// F1 opens the debug console (sandbox only); while it is open, typing goes to it and Esc closes it instead.
/// The simulation is stepped only by its <see cref="SimHost"/>, driven here from the game time of each Update.
/// </summary>
public sealed class GameScreen : IScreen
{
    private const float PanScreenPixelsPerSecond = 320f;
    private const float DragThresholdPx = 4f;
    private static readonly Keys[] OrderKeys = { Keys.A, Keys.S, Keys.G, Keys.X };
    private static readonly string[] HelpLines =
    {
        "arrows / middle-drag pan   wheel zoom   LMB select   RMB move or attack",
        "A then RMB: attack-move   S stop   G guard here   X scatter   Space pause   . step",
        "F1 console   F2 fog viewer   F3 occupancy   F4 paths   F5 ranges   Esc pause menu",
    };

    private readonly GameServices _s;
    private readonly ISnapshotSource _source;
    private readonly SimSnapshotSource? _sim; // null with --demo
    private readonly Func<GameScreen> _restart;
    private readonly Selection _selection = new();
    private readonly OrderController? _orders; // null with --demo
    private readonly OverlayToggles _overlays = new();
    private readonly Camera _camera = new();
    private readonly DebugConsole? _console; // null with --demo
    private readonly List<RenderUnit> _renderUnits = new();
    private readonly RectRegion _hudRegion = new(Rectangle.Empty);
    private readonly RectRegion _helpRegion = new(Rectangle.Empty);
    private Point _mouse;
    private Point? _dragStart;
    private bool _dragging;
    private bool _gameOverShown;

    private GameScreen(GameServices services, SimSnapshotSource? sim, SkirmishSettings? skirmish, Func<GameScreen> restart)
    {
        _s = services;
        _sim = sim;
        Skirmish = skirmish;
        _restart = restart;
        if (sim is null)
        {
            _source = new DemoSnapshotSource();
        }
        else
        {
            _source = sim;
            _orders = new OrderController(sim, _selection);
            _console = new DebugConsole(sim, _selection, _camera);
        }
    }

    /// <summary>The skirmish choices this game was started with, or null for the plain sandbox and demo.</summary>
    public SkirmishSettings? Skirmish { get; }

    /// <summary>
    /// Asked once per Update: when it returns true the game-over screen is pushed (once). Placeholder until
    /// <c>Simulation.GameOver</c> exists; the win-rule package wires it as <c>() =&gt; screen.Sim.GameOver</c>.
    /// </summary>
    public Func<bool> IsGameOver { get; set; } = static () => false;

    /// <summary>The sandbox simulation source, or null for the demo.</summary>
    public SimSnapshotSource? Sim => _sim;

    /// <summary>Creates a sandbox game screen. <paramref name="skirmish"/> supplies the seed (null = the default sandbox seed).</summary>
    public static GameScreen CreateSandbox(GameServices services, bool empty, SkirmishSettings? skirmish)
    {
        ulong seed = skirmish?.MapSeed ?? SimSnapshotSource.DefaultSeed;
        var sim = new SimSnapshotSource(services.RulesDir, empty, seed);
        return new GameScreen(services, sim, skirmish, () => CreateSandbox(services, empty, skirmish));
    }

    /// <summary>Creates the --demo screen: fabricated orbiting units, no simulation.</summary>
    public static GameScreen CreateDemo(GameServices services) =>
        new(services, null, null, () => CreateDemo(services));

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter()
    {
    }

    /// <inheritdoc />
    public void Exit() => _sim?.SaveReplay();

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        UpdateHudRegions();
        if (_dragStart is null)
        {
            input.ConsumeMouseOver(_hudRegion);
            input.ConsumeMouseOver(_helpRegion);
        }

        if (_console is { } console)
        {
            if (input.ConsumePressed(Keys.F1)) console.Toggle();
            else if (console.IsOpen && console.Update(input.Keyboard, input.PreviousKeyboard, input.MousePosition))
            {
                input.Consume(Keys.Escape); // Esc closed the console; it must not also open the pause menu
            }

            if (console.IsOpen)
            {
                foreach (char ch in input.ConsumeText()) console.OnTextInput(ch);
            }
        }

        bool typing = _console is { IsOpen: true };
        if (typing) input.ConsumeKeyboard();

        if (input.ConsumePressed(Keys.Escape))
        {
            _s.Screens.Push(new PauseScreen(_s, _restart));
            return;
        }

        if (_sim is not null)
        {
            if (input.ConsumePressed(Keys.Space)) _sim.Paused = !_sim.Paused;
            if (input.ConsumePressed(Keys.OemPeriod)) _sim.StepOnce();
            if (input.ConsumePressed(Keys.F2)) _sim.CycleViewer();
        }

        if (input.ConsumePressed(Keys.F3)) _overlays.Occupancy = !_overlays.Occupancy;
        if (input.ConsumePressed(Keys.F4)) _overlays.Paths = !_overlays.Paths;
        if (input.ConsumePressed(Keys.F5)) _overlays.Ranges = !_overlays.Ranges;

        if (_orders is not null)
        {
            foreach (Keys key in OrderKeys)
            {
                if (input.ConsumePressed(key)) _orders.KeyPressed(key);
            }
        }

        Vector2 keyDirection = Vector2.Zero;
        if (input.IsDown(Keys.Left)) keyDirection.X -= 1f;
        if (input.IsDown(Keys.Right)) keyDirection.X += 1f;
        if (input.IsDown(Keys.Up)) keyDirection.Y -= 1f;
        if (input.IsDown(Keys.Down)) keyDirection.Y += 1f;
        if (keyDirection != Vector2.Zero)
        {
            keyDirection.Normalize();
            float seconds = (float)t.ElapsedGameTime.TotalSeconds;
            _camera.PanScreen(keyDirection * PanScreenPixelsPerSecond * seconds);
        }

        // Middle-drag: the map follows the cursor, so the view moves opposite to the mouse.
        if (input.MiddleDragging) _camera.PanScreen(-input.MouseDelta.ToVector2());

        int wheelDelta = input.WheelDelta;
        if (wheelDelta != 0) _camera.ZoomBy(wheelDelta > 0 ? 1 : -1);

        if (_sim is not null) _sim.Advance(t.ElapsedGameTime.Ticks / 10); // ticks are 100 ns; microseconds = ticks / 10
        else _source.Update();

        _camera.Clamp(_source.Current.MapWidth, _source.Current.MapHeight, _s.Viewport);
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

        HandleMouse(input);

        if (!_gameOverShown && IsGameOver())
        {
            _gameOverShown = true;
            _s.Screens.Push(new GameOverScreen(_s, _restart));
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        SimSnapshot snapshot = _source.Current;
        IReadOnlyList<LeptonCoord> projectiles = _sim is null ? Array.Empty<LeptonCoord>() : _sim.Projectiles;
        var frame = new SceneFrame(snapshot, _renderUnits, projectiles, _sim?.Sim, _sim?.Rules, _overlays, _selection, DragRectangle());

        _s.Scene.Draw(sb, _s.Pixel, frame, _camera, _s.Viewport);
        DrawHud(sb, snapshot);
        _console?.Draw(sb, _s.Pixel, _s.Viewport);
    }

    private void HandleMouse(InputFrame input)
    {
        Point mouse = input.MousePosition;
        if (input.LeftPressed)
        {
            _dragStart = mouse;
            _dragging = false;
        }
        else if (input.LeftDown && _dragStart is Point start && !_dragging)
        {
            _dragging = Vector2.Distance(start.ToVector2(), mouse.ToVector2()) > DragThresholdPx;
        }
        else if (input.LeftReleased && _dragStart is Point begin)
        {
            if (_dragging)
            {
                Vector2 a = _camera.WorldFromScreen(begin);
                Vector2 b = _camera.WorldFromScreen(mouse);
                _selection.Replace(Selection.Inside(_renderUnits, Vector2.Min(a, b), Vector2.Max(a, b), SimSnapshotSource.LocalPlayer));
            }
            else
            {
                UnitKey? hit = Selection.Pick(_renderUnits, _camera.WorldFromScreen(mouse), SimSnapshotSource.LocalPlayer);
                if (hit is UnitKey key) _selection.Replace(new[] { key });
                else _selection.Clear();
            }

            _dragStart = null;
            _dragging = false;
        }
        else if (_dragStart is not null && input.Mouse.LeftButton == ButtonState.Released)
        {
            // The release was consumed or happened while another screen had the input: forget the half-made drag.
            _dragStart = null;
            _dragging = false;
        }

        if (input.RightPressed && _orders is not null && _console is not { IsOpen: true })
        {
            _orders.RightClick(mouse, _camera, _renderUnits);
        }
    }

    private Rectangle? DragRectangle()
    {
        if (!_dragging || _dragStart is not Point start) return null;
        int left = Math.Min(start.X, _mouse.X);
        int top = Math.Min(start.Y, _mouse.Y);
        int right = Math.Max(start.X, _mouse.X);
        int bottom = Math.Max(start.Y, _mouse.Y);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    private List<string> HudLines(SimSnapshot snapshot)
    {
        var lines = new List<string>
        {
            $"tick {snapshot.Tick}  hash {snapshot.StateHash:x16}  fps {_s.Fps}  zoom {_camera.ZoomLabel}"
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

        return lines;
    }

    // The HUD panels are UI regions: clicks on them never reach the map.
    private void UpdateHudRegions()
    {
        int scale = _s.UiScale;
        List<string> lines = HudLines(_source.Current);
        _hudRegion.Bounds = PanelRect(new Point(8, 8), lines, scale);
        if (_sim is not null)
        {
            int top = _s.Viewport.Height - Hud.HeightOf(HelpLines.Length, scale) - 8;
            _helpRegion.Bounds = PanelRect(new Point(8, top), HelpLines, scale);
        }
        else
        {
            _helpRegion.Bounds = Rectangle.Empty;
        }
    }

    private static Rectangle PanelRect(Point origin, IReadOnlyList<string> lines, int scale) =>
        new(origin.X, origin.Y, lines.Max(line => BitmapFont.Measure(line, scale)) + 16, Hud.HeightOf(lines.Count, scale));

    /// <summary>
    /// Top-left: tick, state hash, frames per second, zoom (and PAUSED). Second line: alive units per player, selection,
    /// overlays, fog viewer. Bottom-left: the key help.
    /// </summary>
    private void DrawHud(SpriteBatch sb, SimSnapshot snapshot)
    {
        int scale = _s.UiScale;
        Hud.Panel(sb, _s.Pixel, new Point(8, 8), HudLines(snapshot), scale);

        if (_sim is not null)
        {
            int top = _s.Viewport.Height - Hud.HeightOf(HelpLines.Length, scale) - 8;
            Hud.Panel(sb, _s.Pixel, new Point(8, top), HelpLines, scale);
        }
    }
}
