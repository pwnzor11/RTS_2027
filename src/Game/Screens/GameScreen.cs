// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Archive;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Saves;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Game.Ui;
using Rts.Game.Ui.Sidebar;
using Rts.Game.Ui.Spectator;
using Rts.Game.Ui.Superweapon;
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
    private static readonly Keys[] OrderKeys = { Keys.A, Keys.S, Keys.G, Keys.X, Keys.D };
    private static readonly string[] SkirmishHelpLines =
    {
        "arrows / middle-drag pan   wheel zoom   LMB select   RMB move, attack or rally",
        "Q W E R T sidebar tabs   A attack-move   S stop   G guard   X scatter   D deploy   Space pause",
        "RMB on a Hatch or transport: enter it   T tab: upgrades",
        "Esc leaves placement / sell / repair, then opens the pause menu",
    };

    private static readonly string[] HelpLines =
    {
        "arrows / middle-drag pan   wheel zoom   LMB select   RMB move or attack",
        "A then RMB: attack-move   S stop   G guard here   X scatter   Space pause   . step",
        "F1 console   F2 fog viewer   F3 occupancy   F4 paths   F5 ranges   F9 AI-vs-AI spectator   Esc pause menu",
    };

    private static readonly string[] SpectatorHelpLines =
    {
        "arrows / middle-drag pan   wheel zoom   click a unit or structure: info (read only)",
        "V next view   F2 full vision / fog   1 2 3 4 speed 1x 2x 4x 8x   Space pause   Tab detail   Esc pause menu",
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
    private int _selectedBuildingType = -1;
    private BuildingUpgradeOffer? _upgrade;   // M3-18: Hatch to Bore Head button for the selected building
    private readonly RectRegion _upgradeRegion = new(Rectangle.Empty);
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
    private MissionOverlay? _mission;   // campaign mission mode (D-061), null otherwise
    private readonly SuperweaponController _superweapon = new();   // M3-18: Sunrise Device button, targeting and flash
    private readonly RectRegion _swRegion = new(Rectangle.Empty);
    private ArchiveTracker? _archive;   // M3-18: unlock progress, skirmish and missions only
    private int _archiveTick = -1;
    private readonly bool _spectatorMode;   // M4-08: AI-vs-AI, no orders, stats strip
    private readonly SpectatorController? _spec;
    private readonly SpectatorPanel? _specPanel;
    private readonly RectRegion _specRegion = new(Rectangle.Empty);
    private UnitView? _inspectUnit;
    private BuildingView? _inspectBuilding;

    private GameScreen(GameServices services, SimSnapshotSource? sim, SkirmishSettings? skirmish, Func<GameScreen> restart,
        bool skirmishMode = false, bool spectator = false)
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
            GarrisonOverlay.Rules = sim.Rules;   // M4-09: pips show free room
            _spectatorMode = spectator;
            if (_spectatorMode)
            {
                _spec = new SpectatorController(sim.Host);   // marks the host Spectator: no order can reach the sim
                _specPanel = new SpectatorPanel(_spec);
            }
            else
            {
                _orders = new OrderController(sim, _selection);
                if (!_skirmishMode) _console = new DebugConsole(sim, _selection, _camera); // AllowDebug is off in a skirmish
            }
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
        SkirmishGame game = SkirmishGame.Create(services.RulesDir, settings.MapSeed, settings.AiTier, settings.Factions);
        var sim = new SimSnapshotSource(game, services.RulesDir);
        var screen = new GameScreen(services, sim, settings, () => CreateSkirmish(services, settings), skirmishMode: true);
        screen.IsGameOver = () => sim.Sim.GameOver;
        sim.SaveTemplate = game.SaveTemplate(settings);
        return screen;
    }

    /// <summary>
    /// Creates an AI-vs-AI spectator game (M4-08): two AIs on <see cref="SpectatorSetup.Create"/>'s skirmish, full map vision, the stats
    /// strip, speed and view keys, and no orders. The setup screen for it is M4-11; until then F9 in the sandbox starts it.
    /// </summary>
    /// <param name="services">Shared services.</param>
    /// <param name="seed">Map and sim seed (0 = the curated map).</param>
    /// <param name="aiTier">AI tier 1..5 for both sides.</param>
    public static GameScreen CreateSpectator(GameServices services, ulong seed, int aiTier = SpectatorSetup.DefaultAiTier)
    {
        SkirmishGame game = SpectatorSetup.Create(services.RulesDir, seed, aiTier);
        var sim = new SimSnapshotSource(game, services.RulesDir);
        var screen = new GameScreen(services, sim, null, () => CreateSpectator(services, seed, aiTier), skirmishMode: false, spectator: true);
        sim.Host.SetViewer(-1);
        screen.IsGameOver = () => sim.Sim.GameOver;
        return screen;
    }

    /// <summary>
    /// Creates a campaign mission (D-061): skirmish-style sidebar and fog over the mission's sim, objectives panel and message ticker,
    /// game over when the script (or a destroyed base) decides the match.
    /// </summary>
    public static GameScreen CreateMission(GameServices services, string missionId)
    {
        MissionGame game = MissionGame.Create(services.RulesDir, missionId);
        var sim = new SimSnapshotSource(game);
        var screen = new GameScreen(services, sim, null, () => CreateMission(services, missionId), skirmishMode: true);
        screen._mission = new MissionOverlay(game.Host);
        screen.IsGameOver = () => sim.Sim.GameOver;
        return screen;
    }

    /// <summary>
    /// Creates the game screen around a game restored from a save (M3-20): the same screen a new skirmish or mission gets, over the loaded
    /// simulation, with the AI re-attached and (mission) the script host restored. Restart starts the same skirmish or mission afresh.
    /// </summary>
    public static GameScreen CreateFromSave(GameServices services, LoadedGame loaded)
    {
        var sim = new SimSnapshotSource(loaded);
        if (loaded.Header.Mode == SaveMode.Mission)
        {
            MissionGame game = MissionGame.FromSave(loaded);
            string id = loaded.Header.MissionId;
            var mission = new GameScreen(services, sim, null, () => CreateMission(services, id), skirmishMode: true);
            mission._mission = new MissionOverlay(game.Host);
            mission.IsGameOver = () => sim.Sim.GameOver;
            return mission;
        }

        SkirmishSettings settings = loaded.Header.Skirmish ?? SkirmishSettings.Default;
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
        if (_spectatorMode && _sim is not null)
        {
            PlayerColours.Assign(new[] { 0, 1 });
            Vector2 mapCentre = new(_source.Current.MapWidth * Camera.CellPx / 2f, _source.Current.MapHeight * Camera.CellPx / 2f);
            Vector2 half = new Vector2(_s.Viewport.Width, _s.Viewport.Height) / _camera.Zoom / 2f;
            _camera.PanScreen((mapCentre - half - _camera.WorldFromScreen(Point.Zero)) * _camera.Zoom);
            return;
        }

        if (!_skirmishMode || _sim is null) return;
        string spritesRoot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_s.RulesDir)) ?? ".", "sprites");
        _cameos = new CameoProvider(_s.Graphics, spritesRoot);
        int colour = Skirmish?.PlayerColour ?? 0;
        // D-068: the local player draws in the chosen colour; the AI takes the next palette entry that differs.
        PlayerColours.Assign(new[] { colour, colour == 1 ? 0 : 1 });
        _sidebar = new SidebarPanel(_sim, SimSnapshotSource.LocalPlayer, _selection, _cameos, PlayerColours.Base(SimSnapshotSource.LocalPlayer));
        CentreCameraOnOwnBase();
        _archive = new ArchiveTracker(ArchiveProgress.Load());
    }

    /// <inheritdoc />
    public void Exit()
    {
        PlayerColours.ResetAssignment();
        _sidebar?.Dispose();
        _cameos?.Dispose();
        _sim?.SaveReplay();
    }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        UpdateHudRegions();
        PlayerHud? localHud = _skirmishMode && _sim is not null ? _sim.Sim.HudFor(SimSnapshotSource.LocalPlayer) : null;
        _superweapon.Update(localHud);
        UpdateSuperweaponRegion();
        UpdateUpgradeOffer(localHud);
        if (_upgrade is { Enabled: true } offer && input.LeftPressed && _upgradeRegion.HitTest(input.MousePosition))
        {
            _sim!.Host.Issue(offer.Command(SimSnapshotSource.LocalPlayer, _selectedBuilding));
            input.ConsumeLeft();
        }
        if (_superweapon.Buttons.Count > 0 && input.LeftPressed) ClickSuperweaponButton(input);
        if (_specPanel is not null && _spec is not null && input.LeftPressed)
        {
            int row = _specPanel.RowAt(input.MousePosition, _s.UiScale);
            if (row >= 0) _spec.Pick(row);
        }

        if (_dragStart is null)
        {
            input.ConsumeMouseOver(_specRegion);
            input.ConsumeMouseOver(_swRegion);
            input.ConsumeMouseOver(_upgradeRegion);
            input.ConsumeMouseOver(_hudRegion);
            input.ConsumeMouseOver(_helpRegion);
            if (_mission is not null) input.ConsumeMouseOver(_mission.ObjectivesRegion);
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
        if (_superweapon.IsArmed && input.ConsumePressed(Keys.Escape)) _superweapon.Cancel();   // Esc leaves targeting first

        if (input.ConsumePressed(Keys.Escape))
        {
            _s.Screens.Push(new PauseScreen(_s, _restart, SaveTarget()));
            return;
        }

        _sidebar?.Update(input, _camera, _s.Viewport.Width, _s.Viewport.Height);

        if (_sim is not null)
        {
            if (_spec is not null)
            {
                _spec.Handle(input);   // V, F2, 1-4, Space, Tab
            }
            else
            {
                if (input.ConsumePressed(Keys.Space)) _sim.Paused = !_sim.Paused;
                if (input.ConsumePressed(Keys.F2)) _sim.CycleViewer();
                if (!_skirmishMode && _console is not { IsOpen: true } && input.ConsumePressed(Keys.F9)) StartSpectatorFromSandbox();
            }

            if (input.ConsumePressed(Keys.OemPeriod)) _sim.StepOnce();
        }

        if (input.ConsumePressed(Keys.F3)) _overlays.Occupancy = !_overlays.Occupancy;
        if (input.ConsumePressed(Keys.F4)) _overlays.Paths = !_overlays.Paths;
        if (input.ConsumePressed(Keys.F5)) _overlays.Ranges = !_overlays.Ranges;

        if (_orders is not null) _orders.SelectedBuilding = _selectedBuilding;   // M4-09: D unloads a selected garrisoned building
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
        Vector2 view = _camera.WorldFromScreen(new Point(MapViewport().Width / 2, MapViewport().Height / 2));   // M3-INT: positional audio
        Audio.AudioListener.SetFromWorldPixels(view.X, view.Y, Camera.CellPx);
        UnitInterpolator.Build(_source.Current, _source.Previous, _source.Alpha, _renderUnits);
        _superweapon.Fx.Observe(_source.Current.Tick, _source.Current.FxEvents);
        ObserveArchive(localHud);

        _specPanel?.Update(_sim!.Sim);
        if (_spectatorMode)
        {
            PruneInspection();
        }
        else if (_sim is { } live)
        {
            _selection.Prune(key => live.IsOwnedAlive(key, SimSnapshotSource.LocalPlayer));
        }
        else
        {
            SimSnapshot shown = _source.Current;
            _selection.Prune(key => shown.Units.Any(unit => UnitKey.Of(unit) == key));
        }

        PruneBuildingSelection();
        HandleSuperweaponTargeting(input);
        HandleMouse(input);

        if (!_gameOverShown && IsGameOver())
        {
            _gameOverShown = true;
            IReadOnlyList<string>? table = _sim is null ? null : SpectatorTable.Final(SpectatorTable.Collect(_sim.Sim));
            _s.Screens.Push(new GameOverScreen(_s, _restart, GameOverHeadline(), table, _spectatorMode, () => _sim?.SaveReplay()));
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
        DrawSuperweapon(sb, snapshot.Tick);
        DrawUpgradeButton(sb);
        DrawSpectator(sb);
        _console?.Draw(sb, _s.Pixel, _s.Viewport);
    }

    // What Save game writes to: only a skirmish or mission (the sandbox has no save header).
    private GameSaveTarget? SaveTarget() =>
        _sim is { SaveTemplate: { } template } ? new GameSaveTarget(new SaveCatalog(SaveCatalog.DefaultRoot), template, _sim.Host, _s.RulesDir) : null;

    private void HandleMouse(InputFrame input)
    {
        if (_spectatorMode)
        {
            HandleInspectClick(input);
            return;
        }

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
                if (_selection.Count > 0) Audio.AudioBootstrap.Director?.Notify(Audio.AudioEventKind.UnitSelect);   // M3-INT
            }
            else
            {
                UnitKey? hit = Selection.Pick(_renderUnits, _camera.WorldFromScreen(mouse), SimSnapshotSource.LocalPlayer);
                if (hit is UnitKey key)
                {
                    _selection.Replace(new[] { key });
                    ClearBuildingSelection();
                    Audio.AudioBootstrap.Director?.Notify(Audio.AudioEventKind.UnitSelect);   // M3-INT
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

    // ---- Spectator (M4-08) ---------------------------------------------------------------------------

    // F9 in the sandbox: swap this screen for an AI-vs-AI spectator game on a generated map. Posted, so this screen has exited (and saved
    // its replay) before the new game opens its own.
    private void StartSpectatorFromSandbox() => _s.Screens.Post(() =>
    {
        _s.Screens.Pop();
        _s.Screens.Push(CreateSpectator(_s, SimSnapshotSource.DefaultSeed));
    });

    // A left click picks any unit or structure to show its info; it never orders anything.
    private void HandleInspectClick(InputFrame input)
    {
        if (!input.LeftPressed || !MapViewport().Bounds.Contains(input.MousePosition)) return;
        Vector2 world = _camera.WorldFromScreen(input.MousePosition);
        _inspectUnit = SpectatorInspect.PickUnit(_renderUnits, world);
        _inspectBuilding = _inspectUnit is null ? SpectatorInspect.PickBuilding(_source.Current.Buildings, Camera.CellAt(world)) : null;
    }

    // Follows the picked unit or structure through the latest snapshot; clears the pick when it is gone (dead or out of the viewer's sight).
    private void PruneInspection()
    {
        SimSnapshot snap = _source.Current;
        if (_inspectUnit is UnitView u)
        {
            _inspectUnit = null;
            foreach (UnitView v in snap.Units)
                if (v.Id == u.Id && v.Kind == u.Kind) { _inspectUnit = v; break; }
        }

        if (_inspectBuilding is BuildingView b)
        {
            _inspectBuilding = null;
            foreach (BuildingView v in snap.Buildings)
                if (v.Id == b.Id) { _inspectBuilding = v; break; }
        }
    }

    private void DrawSpectator(SpriteBatch sb)
    {
        if (_specPanel is null || _sim is null) return;
        _specPanel.Draw(sb, _s.Pixel, _s.UiScale);
        IReadOnlyList<string>? info = _inspectUnit is UnitView u ? SpectatorInspect.Describe(u, _sim.Sim.Setup.Rules)
            : _inspectBuilding is BuildingView b ? SpectatorInspect.Describe(b, _sim.Sim.Setup.Rules) : null;
        if (info is null) return;
        int scale = _s.UiScale;
        int w = info.Max(line => BitmapFont.Measure(line, scale)) + 16;
        Hud.Panel(sb, _s.Pixel, new Point(_s.Viewport.Width - w - 8, _s.Viewport.Height - Hud.HeightOf(info.Count, scale) - 8), info, scale);
    }

    // ---- Sunrise Device (M3-18) -------------------------------------------------------------------------

    private Rectangle SuperweaponButtonRect(int index)
    {
        int scale = _s.UiScale;
        int w = BitmapFont.Measure("SUNRISE  PAUSED", scale) + 16;
        int h = Hud.HeightOf(1, scale) + 6;
        return new Rectangle(MapViewport().Width - w - 8, 8 + index * (h + 4), w, h);
    }

    private void UpdateSuperweaponRegion()
    {
        int n = _superweapon.Buttons.Count;
        _swRegion.Bounds = n == 0 ? Rectangle.Empty : Rectangle.Union(SuperweaponButtonRect(0), SuperweaponButtonRect(n - 1));
    }

    private void ClickSuperweaponButton(InputFrame input)
    {
        for (int i = 0; i < _superweapon.Buttons.Count; i++)
        {
            if (!SuperweaponButtonRect(i).Contains(input.MousePosition)) continue;
            _superweapon.ClickButton(i);
            input.ConsumeLeft();
            return;
        }
    }

    // Armed: a left click on the map fires (the sim's own range check vetoes a bad cell), a right click cancels.
    private void HandleSuperweaponTargeting(InputFrame input)
    {
        if (!_superweapon.IsArmed || _sim is null) return;
        if (input.RightPressed)
        {
            _superweapon.Cancel();
            input.ConsumeRight();
        }
        else if (input.LeftPressed && MapViewport().Bounds.Contains(input.MousePosition))
        {
            byte local = SimSnapshotSource.LocalPlayer;
            CellCoord cell = Camera.CellAt(_camera.WorldFromScreen(input.MousePosition));
            Command? command = _superweapon.ClickMap(local, cell, (b, c) => _sim.Sim.CanFireSuperweapon(local, b, c));
            input.ConsumeLeft();   // never also select or box-drag
            if (command is Command fire) _sim.Host.Issue(fire);
        }
    }

    private void DrawSuperweapon(SpriteBatch sb, int tick)
    {
        int scale = _s.UiScale;
        for (int i = 0; i < _superweapon.Buttons.Count; i++)
        {
            SuperweaponButton b = _superweapon.Buttons[i];
            Rectangle r = SuperweaponButtonRect(i);
            bool armed = _superweapon.Targeting.Armed == b.Building;
            Shapes.Fill(sb, _s.Pixel, r, new Color(0, 0, 0, 190));
            Shapes.Fill(sb, _s.Pixel, new Rectangle(r.X, r.Bottom - 5, r.Width * b.ProgressPermille / 1000, 5),
                b.State == SuperweaponState.Ready ? new Color(255, 200, 60) : new Color(120, 120, 120));
            Shapes.Outline(sb, _s.Pixel, r, armed ? Color.Red : b.State == SuperweaponState.Ready ? Color.White : new Color(90, 90, 90), 2);
            Color text = b.State == SuperweaponState.Ready ? new Color(0xFF, 0xE0, 0x80) : new Color(0xB0, 0xB0, 0xB0);
            BitmapFont.Draw(sb, _s.Pixel, $"{b.Caption}  {b.Status}", new Vector2(r.X + 8, r.Y + 4), text, scale);
        }

        if (_superweapon.IsArmed && MapViewport().Bounds.Contains(_mouse))
        {
            CellCoord cell = Camera.CellAt(_camera.WorldFromScreen(_mouse));
            float left = cell.X * Camera.CellPx, top = cell.Y * Camera.CellPx;
            Shapes.Outline(sb, _s.Pixel, _camera.ScreenRect(left, top, left + Camera.CellPx, top + Camera.CellPx), Color.Red, 2);
        }

        int flash = _superweapon.Fx.FlashAlpha(tick);
        if (flash > 0) Shapes.Fill(sb, _s.Pixel, new Rectangle(0, 0, _s.Viewport.Width, _s.Viewport.Height), new Color(255, 255, 255, flash));
    }

    // ---- Hatch to Bore Head upgrade (M3-18) --------------------------------------------------------------

    private Rectangle UpgradeButtonRect()
    {
        int scale = _s.UiScale;
        int w = BitmapFont.Measure("UPGRADE BORE HEAD $9999", scale) + 16;
        int h = Hud.HeightOf(1, scale) + 6;
        Viewport map = MapViewport();
        return new Rectangle(map.Width - w - 8, map.Height - h - 8, w, h);
    }

    private void UpdateUpgradeOffer(PlayerHud? hud)
    {
        _upgrade = null;
        if (_sim is null || hud is null || _selectedBuilding.IsNone || _selectedBuildingType < 0)
        {
            _upgradeRegion.Bounds = Rectangle.Empty;
            return;
        }

        var owned = new List<string>();
        foreach (BuildingView b in _source.Current.Buildings)
            if (!b.Ghost && b.Player == SimSnapshotSource.LocalPlayer) owned.Add(_sim.Rules.Buildings[b.TypeIndex].Id);
        _upgrade = BuildingUpgradeOffer.For(_sim.Rules, _selectedBuildingType, hud.Cash, owned);
        _upgradeRegion.Bounds = _upgrade is null ? Rectangle.Empty : UpgradeButtonRect();
    }

    private void DrawUpgradeButton(SpriteBatch sb)
    {
        if (_upgrade is not BuildingUpgradeOffer offer) return;
        int scale = _s.UiScale;
        Rectangle r = UpgradeButtonRect();
        Shapes.Fill(sb, _s.Pixel, r, new Color(0, 0, 0, 190));
        Shapes.Outline(sb, _s.Pixel, r, offer.Enabled ? Color.White : new Color(90, 90, 90), 2);
        BitmapFont.Draw(sb, _s.Pixel, offer.Caption, new Vector2(r.X + 8, r.Y + 4),
            offer.Enabled ? new Color(0xFF, 0xE0, 0x80) : new Color(0x90, 0x90, 0x90), scale);
        if (!offer.Enabled && r.Contains(_mouse))
        {
            int w = BitmapFont.Measure(offer.Reason, scale) + 16;
            Hud.Panel(sb, _s.Pixel, new Point(Math.Max(0, r.Right - w), r.Y - Hud.HeightOf(1, scale) - 4), new[] { offer.Reason }, scale);
        }
    }

    // ---- Archive (M3-18) ---------------------------------------------------------------------------------

    // Once per new simulation tick: record what the local player can see, and the upgrades researched.
    private void ObserveArchive(PlayerHud? hud)
    {
        if (_archive is null || _sim is null || hud is null) return;
        SimSnapshot snapshot = _source.Current;
        if (snapshot.Tick == _archiveTick) return;
        _archiveTick = snapshot.Tick;
        _archive.Observe(snapshot, _sim.Rules);
        _archive.ObserveResearch(_sim.Rules, hud.Upgrades);
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
            _selectedBuildingType = b.TypeIndex;
            return;
        }
    }

    private void ClearBuildingSelection()
    {
        _selectedBuilding = EntityId.None;
        _selectedBuildingProduces = false;
        _selectedBuildingType = -1;
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
        if (_spectatorMode) return GameOverScreen.SpectatorHeadline(_sim.Sim.WinningTeam);
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

        if (_orders is { DeployArmed: true }) lines.Add("DEPLOY: right-click the cell to dig at (D cancels)");

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
        if (_specPanel is not null)
        {
            _specPanel.Origin = new Point(8, 8 + Hud.HeightOf(lines.Count, scale) + 4);
            _specRegion.Bounds = _specPanel.Bounds(scale);
        }
        _mission?.UpdateRegion(lines.Count, scale);
        if (_sim is not null)
        {
            string[] help = _spectatorMode ? SpectatorHelpLines : _skirmishMode ? SkirmishHelpLines : HelpLines;
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
        List<string> hud = HudLines(snapshot);
        Hud.Panel(sb, _s.Pixel, new Point(8, 8), hud, scale);

        if (_sim is not null)
        {
            string[] help = _spectatorMode ? SpectatorHelpLines : _skirmishMode ? SkirmishHelpLines : HelpLines;
            int top = _s.Viewport.Height - Hud.HeightOf(help.Length, scale) - 8;
            Hud.Panel(sb, _s.Pixel, new Point(8, top), help, scale);
            _mission?.Draw(sb, _s.Pixel, scale, hud.Count, top, snapshot.Tick);
        }
    }
}
