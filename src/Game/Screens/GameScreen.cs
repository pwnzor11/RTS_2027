// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Game.Ui;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;

namespace Rts.Game.Screens;

/// <summary>
/// The battle view (formerly the whole of RtsGame): camera, selection, orders, overlays, HUD and the F1 debug console
/// over the real simulation (or the --demo source when there is no sim). Controls: arrows or middle-drag to pan, wheel
/// to zoom, left-click to select or drag a box, right-click to move, attack or (after A) attack-move, S stop, G guard,
/// X scatter, F2 fog viewer, F3 occupancy, F4 paths, F5 ranges, Space pause, '.' step, Esc opens the pause menu.
/// F1 opens the debug console (sandbox only); while it is open, typing goes to it and Esc closes it instead.
/// A skirmish (<see cref="CreateSkirmish"/>) adds the right-hand sidebar and minimap (Q W E R pick the tabs, Esc first leaves
/// placement, sell and repair mode), a factory selected with the left click takes a rally point with the right click, and the
/// game-over screen says Victory or Defeat. The skirmish has no debug console.
/// The simulation is stepped only by its <see cref="SimHost"/>, driven here from the game time of each Update.
/// </summary>
public sealed class GameScreen : IScreen
{
    private const float PanScreenPixelsPerSecond = 320f;
    private const float DragThresholdPx = 4f;
    private static readonly Keys[] OrderKeys = { Keys.A, Keys.S, Keys.G, Keys.X };
    private static readonly string[] SkirmishHelpLines =
    {
        "arrows / middle-drag pan   wheel zoom   LMB select   RMB move, attack or rally",
        "Q W E R sidebar tabs   A attack-move   S stop   G guard   X scatter   Space pause",
        "Esc leaves placement / sell / repair, then opens the pause menu",
    };

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
    private readonly bool _skirmishMode;
    private SidebarPanel? _sidebar;
    private CameoProvider? _cameos;
    private EntityId _selectedBuilding = EntityId.None;
    private bool _selectedBuildingProduces;
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

    private GameScreen(GameServices services, SimSnapshotSource? sim, SkirmishSettings? skirmish, Func<GameScreen> restart,
        bool skirmishMode = false)
    {
        _s = services;
        _sim = sim;
        Skirmish = skirmish;
        _restart = restart;
        _skirmishMode = skirmishMode && sim is not null;
        if (sim is null)
        {
            _source = new DemoSnapshotSource();
        }
        else
        {
            _source = sim;
            _orders = new OrderController(sim, _selection);
            if (!_skirmishMode) _console = new DebugConsole(sim, _selection, _camera); // AllowDebug is off in a skirmish
        }
    }

    /// <summary>The skirmish choices this game was started with, or null for the plain sandbox and demo.</summary>
    public SkirmishSettings? Skirmish { get; }

    /// <summary>
    /// Asked once per Update: when it returns true the game-over screen is pushed (once). A skirmish sets it to
    /// <c>() =&gt; sim.GameOver</c>; the sandbox and demo never end.
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

    /// <summary>
    /// Creates the real skirmish (D-062): the map and AI from <see cref="SkirmishGame"/>, local player 0 with the chosen colour,
    /// the sidebar and minimap, and the game-over check wired to <c>Simulation.GameOver</c>.
    /// </summary>
    public static GameScreen CreateSkirmish(GameServices services, SkirmishSettings settings)
    {
        SkirmishGame game = SkirmishGame.Create(services.RulesDir, settings.MapSeed, settings.AiTier);
        var sim = new SimSnapshotSource(game, services.RulesDir);
        var screen = new GameScreen(services, sim, settings, () => CreateSkirmish(services, settings), skirmishMode: true);
        screen.IsGameOver = () => sim.Sim.GameOver;
        return screen;
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
        if (!_skirmishMode || _sim is null) return;
        string spritesRoot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_s.RulesDir)) ?? ".", "sprites");
        _cameos = new CameoProvider(_s.Graphics, spritesRoot);
        int colour = Skirmish?.PlayerColour ?? 0;
        _sidebar = new SidebarPanel(_sim, SimSnapshotSource.LocalPlayer, _selection, _cameos, PlayerColours.Base(colour));
        CentreCameraOnOwnBase();
    }

    /// <inheritdoc />
    public void Exit()
    {
        _sidebar?.Dispose();
        _cameos?.Dispose();
        _sim?.SaveReplay();
    }

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

        if (_sidebar is { Mode: not SidebarMode.None } && input.ConsumePressed(Keys.Escape)) _sidebar.CancelMode();

        if (input.ConsumePressed(Keys.Escape))
        {
            _s.Screens.Push(new PauseScreen(_s, _restart));
            return;
        }

        _sidebar?.Update(input, _camera, _s.Viewport.Width, _s.Viewport.Height);

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

        _camera.Clamp(_source.Current.MapWidth, _source.Current.MapHeight, MapViewport());
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

        PruneBuildingSelection();
        HandleMouse(input);

        if (!_gameOverShown && IsGameOver())
        {
            _gameOverShown = true;
            _s.Screens.Push(new GameOverScreen(_s, _restart, GameOverHeadline()));
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        SimSnapshot snapshot = _source.Current;
        IReadOnlyList<LeptonCoord> projectiles = _sim is null ? Array.Empty<LeptonCoord>() : _sim.Projectiles;
        var frame = new SceneFrame(snapshot, _renderUnits, projectiles, _sim?.Sim, _sim?.Rules, _overlays, _selection, DragRectangle());

        _s.Scene.Draw(sb, _s.Pixel, frame, _camera, _s.Viewport);
        DrawSelectedBuilding(sb, snapshot);
        _sidebar?.DrawMapOverlay(sb, _s.Pixel, _camera, snapshot);
        DrawHud(sb, snapshot);
        _sidebar?.Draw(sb, _s.Pixel, snapshot);
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
                ClearBuildingSelection();
            }
            else
            {
                UnitKey? hit = Selection.Pick(_renderUnits, _camera.WorldFromScreen(mouse), SimSnapshotSource.LocalPlayer);
                if (hit is UnitKey key)
                {
                    _selection.Replace(new[] { key });
                    ClearBuildingSelection();
                }
                else
                {
                    _selection.Clear();
                    SelectBuildingAt(Camera.CellAt(_camera.WorldFromScreen(mouse)));
                }
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
            CellCoord cell = Camera.CellAt(_camera.WorldFromScreen(mouse));
            if (_sim is not null && !_selectedBuilding.IsNone && _selectedBuildingProduces && _sim.Map.Contains(cell))
            {
                _sim.Host.Issue(SidebarCommands.Rally(SimSnapshotSource.LocalPlayer, _selectedBuilding, cell));
            }
            else
            {
                _orders.RightClick(mouse, _camera, _renderUnits);
            }
        }
    }

    // ---- buildings: selection for rally points ------------------------------------------------------

    private void SelectBuildingAt(CellCoord cell)
    {
        ClearBuildingSelection();
        if (_sim is null || !_skirmishMode) return;
        foreach (BuildingView b in _source.Current.Buildings)
        {
            if (b.Ghost || b.Player != SimSnapshotSource.LocalPlayer) continue;
            if (cell.X < b.Cell.X || cell.X >= b.Cell.X + b.FootprintW || cell.Y < b.Cell.Y || cell.Y >= b.Cell.Y + b.FootprintH) continue;
            _selectedBuilding = b.Id;
            _selectedBuildingProduces = _sim.Rules.Buildings[b.TypeIndex].Produces is not null;
            return;
        }
    }

    private void ClearBuildingSelection()
    {
        _selectedBuilding = EntityId.None;
        _selectedBuildingProduces = false;
    }

    private void PruneBuildingSelection()
    {
        if (_selectedBuilding.IsNone) return;
        foreach (BuildingView b in _source.Current.Buildings)
            if (!b.Ghost && b.Id == _selectedBuilding) return;
        ClearBuildingSelection();
    }

    private void DrawSelectedBuilding(SpriteBatch sb, SimSnapshot snapshot)
    {
        if (_selectedBuilding.IsNone) return;
        foreach (BuildingView b in snapshot.Buildings)
        {
            if (b.Ghost || b.Id != _selectedBuilding) continue;
            float left = b.Cell.X * Camera.CellPx, top = b.Cell.Y * Camera.CellPx;
            Rectangle r = _camera.ScreenRect(left, top, left + b.FootprintW * Camera.CellPx, top + b.FootprintH * Camera.CellPx);
            Shapes.Outline(sb, _s.Pixel, r, Color.White, 2);
        }
    }

    // ---- skirmish layout and result -------------------------------------------------------------------

    // The part of the window the map is shown in: all of it, or what the sidebar leaves.
    private Viewport MapViewport()
    {
        Viewport v = _s.Viewport;
        if (_sidebar is null) return v;
        return new Viewport(0, 0, Math.Max(1, _sidebar.Layout.MapView.Width), v.Height);
    }

    private void CentreCameraOnOwnBase()
    {
        if (_sim is null) return;
        Vector2? centre = null;
        foreach (BuildingView b in _source.Current.Buildings)
        {
            if (b.Player != SimSnapshotSource.LocalPlayer) continue;
            centre = new Vector2((b.Cell.X + b.FootprintW / 2f) * Camera.CellPx, (b.Cell.Y + b.FootprintH / 2f) * Camera.CellPx);
            break;
        }

        centre ??= Camera.CentreOf(_sim.Map.StartPositions[SimSnapshotSource.LocalPlayer]);
        int mapWidth = _s.Viewport.Width - SidebarLayout.WidthFor(_s.Viewport.Width, Rts.Game.Ui.UiScale.FromBackbuffer(_s.Viewport.Height));
        Vector2 size = new Vector2(Math.Max(1, mapWidth), _s.Viewport.Height) / _camera.Zoom;
        Vector2 target = centre.Value - size / 2f;
        _camera.PanScreen((target - _camera.WorldFromScreen(Point.Zero)) * _camera.Zoom);
        _camera.Clamp(_source.Current.MapWidth, _source.Current.MapHeight, new Viewport(0, 0, Math.Max(1, mapWidth), _s.Viewport.Height));
    }

    private string GameOverHeadline()
    {
        if (_sim is null) return GameOverScreen.Headline(false, -1, 0);
        byte local = SimSnapshotSource.LocalPlayer;
        return GameOverScreen.Headline(_sim.Sim.HudFor(local).Defeated, _sim.Sim.WinningTeam, _sim.Sim.Setup.TeamOf(local));
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

        if (_sim is not null && _orders is not null && !_skirmishMode) // unit counts of both sides would show the fogged enemy
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
            string[] help = _skirmishMode ? SkirmishHelpLines : HelpLines;
            int top = _s.Viewport.Height - Hud.HeightOf(help.Length, scale) - 8;
            _helpRegion.Bounds = PanelRect(new Point(8, top), help, scale);
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
            string[] help = _skirmishMode ? SkirmishHelpLines : HelpLines;
            int top = _s.Viewport.Height - Hud.HeightOf(help.Length, scale) - 8;
            Hud.Panel(sb, _s.Pixel, new Point(8, top), help, scale);
        }
    }
}
