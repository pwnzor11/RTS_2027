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
using Rts.Game.Ui.Llm;
using Rts.Game.Ui.Sidebar;
using Rts.Game.Ui.Spectator;
using Rts.Game.Ui.Superweapon;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Screens;

/// <summary>
/// The battle view (formerly the whole of RtsGame): camera, selection, orders, overlays, HUD and the F1 debug console
/// over the real simulation (or the --demo source when there is no sim). Controls: arrows or middle-drag to pan, wheel
/// to zoom, left-click to select or drag a box, right-click to move, attack or (after A) attack-move, S stop, G guard,
/// X scatter, F2 fog viewer, F3 occupancy, F4 paths, F10 ranges, Space pause, '.' step, Esc opens the pause menu.
/// F1 opens the debug console (sandbox only); while it is open, typing goes to it and Esc closes it instead.
/// A skirmish (<see cref="CreateSkirmish"/>) adds the right-hand sidebar and minimap (Q W E R pick the tabs, Esc first leaves
/// placement, sell and repair mode), a factory selected with the left click takes a rally point with the right click, and the
/// game-over screen says Victory or Defeat. The skirmish has no debug console.
/// The simulation is stepped only by its <see cref="SimHost"/>, driven here from the game time of each Update.
/// </summary>
public sealed class GameScreen : IScreen
{
    private const float DragThresholdPx = 4f;
    private readonly GameServices _s;
    private readonly ISnapshotSource _source;
    private readonly SimSnapshotSource? _sim; // null with --demo
    private readonly Func<GameScreen> _restart;
    private readonly bool _skirmishMode;
    private SidebarPanel? _sidebar;
    private CameoProvider? _cameos;
    private Rts.Game.Ui.Card.SelectionCard? _card;   // M11-04: the bottom-centre selection card
    private EntityId _selectedBuilding = EntityId.None;
    private bool _selectedBuildingProduces;
    private int _selectedBuildingType = -1;
    private BuildingUpgradeOffer? _upgrade;   // M3-18: Hatch to Bore Head button for the selected building
    private readonly RectRegion _upgradeRegion = new(Rectangle.Empty);
    private PackOffer? _pack;                 // M6-09: Pack / Unpack button for the selected rig
    private EntityId _packSubject = EntityId.None;
    private readonly RectRegion _packRegion = new(Rectangle.Empty);
    private bool _diveShown;                  // M7-14: Dive/Surface button for a selected own submarine
    private bool _diveSubmerged;
    private readonly RectRegion _diveRegion = new(Rectangle.Empty);
    private UnpackPlacement? _unpack;         // M6-09: the unpack footprint follows the cursor
    private readonly Selection _selection = new();
    private readonly OrderController? _orders; // null with --demo
    private readonly OrderFeedback _feedback = new();   // O1: order rings
    private readonly OverlayToggles _overlays = new();
    private readonly Camera _camera = new();
    private readonly DebugConsole? _console; // null with --demo
    private readonly CommanderChatPanel _chat = new(); // M5-07: commander chat (Enter to talk)
    private readonly List<RenderUnit> _renderUnits = new();
    private readonly RectRegion _hudRegion = new(Rectangle.Empty);
    private Point _mouse;
    private Point? _dragStart;
    private bool _dragging;
    private bool _gameOverShown;
    private Func<IScreen?>? _gameOverOverride;   // M8-08: campaign missions show a debrief on a win
    private MissionOverlay? _mission;   // campaign mission mode (D-061), null otherwise
    private MissionGame? _missionGame;  // S18 MIS-01/05: a mission ends on the script outcome or the human's defeat
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
                if (skirmish is not null) _specPanel.Commanders = SpectatorSetup.CommandersOf(skirmish);   // M5-09: "LLM" tag
            }
            else
            {
                _orders = new OrderController(sim, _selection) { Feedback = _feedback };
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
        // M4-INT2: the full v2 settings (2 to 4 slots, teams, per-slot AI and faction); no Human slot starts a spectator game.
        if (settings.IsSpectator) return CreateSpectator(services, settings);
        SimSnapshotSource sim = BuildSkirmishSource(services.RulesDir, settings, appSettings: services.Settings);
        var screen = new GameScreen(services, sim, settings, () => CreateSkirmish(services, settings), skirmishMode: true);
        screen.IsGameOver = () => sim.Sim.GameOver;
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
    /// The simulation source a skirmish screen runs on, built from the full v2 settings (M4-INT2): a Human slot gives the normal skirmish,
    /// none gives the all-AI spectator game. <see cref="CreateSkirmish"/> and <see cref="CreateSpectator(GameServices, SkirmishSettings)"/> both
    /// use it, and the headless tests call it, so they start the same game the screen does.
    /// </summary>
    /// <param name="rulesDir">The data/rules directory.</param>
    /// <param name="settings">The setup choices.</param>
    /// <param name="replayPath">Where the replay is recorded (tests); null = the default skirmish replay path.</param>
    /// <param name="appSettings">M5: the app settings, for the LLM commanders of slots with Commander set; null = no commanders (tests).</param>
    public static SimSnapshotSource BuildSkirmishSource(string rulesDir, SkirmishSettings settings, string? replayPath = null,
        Settings? appSettings = null)
    {
        SkirmishGame game = settings.IsSpectator ? SpectatorSetup.Create(rulesDir, settings, appSettings)
            : SkirmishGame.Create(rulesDir, settings, appSettings);
        var sim = new SimSnapshotSource(game, rulesDir, replayPath);
        if (!settings.IsSpectator) sim.SaveTemplate = game.SaveTemplate(settings);
        return sim;
    }

    /// <summary>
    /// Creates the AI-only game the setup screen's Spectate starts (M4-INT2): every open slot of <paramref name="settings"/> is an AI
    /// (2 to 4 of them, with their teams, factions and tiers), full map vision, the spectator panel and no orders.
    /// </summary>
    /// <param name="services">Shared services.</param>
    /// <param name="settings">Settings with no Human slot.</param>
    public static GameScreen CreateSpectator(GameServices services, SkirmishSettings settings)
    {
        SimSnapshotSource sim = BuildSkirmishSource(services.RulesDir, settings, appSettings: services.Settings);
        var screen = new GameScreen(services, sim, settings, () => CreateSpectator(services, settings), skirmishMode: false, spectator: true);
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
        screen._mission = new MissionOverlay(game.Host, Rts.Game.Ui.Objectives.ObjectiveGuidance.BriefingOf(game));
        screen.EndOnMission(game);
        return screen;
    }

    /// <summary>
    /// Creates a campaign mission screen (M8-08) around a game that <see cref="Rts.Game.Campaign.CampaignService.Start"/> set up. When the game ends,
    /// <paramref name="gameOver"/> may return the screen to show instead of the normal game-over menu (null = the normal menu).
    /// </summary>
    public static GameScreen CreateCampaignMission(GameServices services, MissionGame game, Func<GameScreen> restart, Func<IScreen?> gameOver)
    {
        var sim = new SimSnapshotSource(game);
        var screen = new GameScreen(services, sim, null, restart, skirmishMode: true);
        screen._mission = new MissionOverlay(game.Host, Rts.Game.Ui.Objectives.ObjectiveGuidance.BriefingOf(game));
        screen.EndOnMission(game);
        screen._gameOverOverride = gameOver;
        return screen;
    }

    /// <summary>
    /// Creates the game screen around a game restored from a save (M3-20): the same screen a new skirmish or mission gets, over the loaded
    /// simulation, with the AI re-attached and (mission) the script host restored. Restart starts the same skirmish or mission afresh.
    /// </summary>
    public static GameScreen CreateFromSave(GameServices services, LoadedGame loaded)
    {
        var sim = new SimSnapshotSource(loaded, services.Settings);   // M5-10: brings back the save's LLM commanders
        if (loaded.Header.Mode == SaveMode.Mission)
        {
            MissionGame game = MissionGame.FromSave(loaded);
            string id = loaded.Header.MissionId;
            var campaign = CampaignFlow.Resume(services, game);   // S18 MIS-02: a campaign mission keeps its campaign wiring
            var mission = new GameScreen(services, sim, null, campaign?.Restart ?? (() => CreateMission(services, id)), skirmishMode: true);
            mission._mission = new MissionOverlay(game.Host, Rts.Game.Ui.Objectives.ObjectiveGuidance.BriefingOf(game));
            mission.EndOnMission(game);
            mission._gameOverOverride = campaign?.GameOver;
            return mission;
        }

        SkirmishSettings settings = loaded.Header.Skirmish ?? SkirmishSettings.Default;
        var screen = new GameScreen(services, sim, settings, () => CreateSkirmish(services, settings), skirmishMode: true);
        screen.IsGameOver = () => sim.Sim.GameOver;
        return screen;
    }

    // S18 MIS-01 / MIS-05: a scripted mission ends when the script decides or the human is defeated (MissionGame.IsOver), not on the sim's
    // elimination rule, which can end it early (enemy out of buildings) or never (a third team survives Mission.Lose).
    private void EndOnMission(MissionGame game)
    {
        _missionGame = game;
        IsGameOver = () => game.IsOver;
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
            PlayerColours.Assign(Skirmish is { IsSpectator: true } spec ? ColoursOf(spec) : new[] { 0, 1 });
            Vector2 mapCentre = new(_source.Current.MapWidth * Camera.CellPx / 2f, _source.Current.MapHeight * Camera.CellPx / 2f);
            Vector2 half = new Vector2(_s.Viewport.Width, _s.Viewport.Height) / _camera.Zoom / 2f;
            _camera.PanScreen((mapCentre - half - _camera.WorldFromScreen(Point.Zero)) * _camera.Zoom);
            return;
        }

        if (_sim is not null && _orders is not null && !_skirmishMode) _card = new Rts.Game.Ui.Card.SelectionCard(_sim, _selection, null, PlayerColours.Base(SimSnapshotSource.LocalPlayer));
        if (!_skirmishMode || _sim is null) return;
        string spritesRoot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_s.RulesDir)) ?? ".", "sprites");
        _cameos = new CameoProvider(_s.Graphics, spritesRoot);
        int colour = Skirmish?.PlayerColour ?? 0;
        // D-068: the local player draws in the chosen colour; the AI takes the next palette entry that differs.
        PlayerColours.Assign(Skirmish is { } sk && sk.PlayerCount >= 2 ? ColoursOf(sk) : new[] { colour, colour == 1 ? 0 : 1 });
        _sidebar = new SidebarPanel(_sim, SimSnapshotSource.LocalPlayer, _selection, _cameos, PlayerColours.Base(SimSnapshotSource.LocalPlayer));
        _sidebar.HiddenCursor = SoftwareCursor.MakeHidden(_s.Graphics);   // M11-10e
        _sidebar.Blurbs = LoadCameoBlurbs();                               // M11-10g
        _card = new Rts.Game.Ui.Card.SelectionCard(_sim, _selection, _cameos, PlayerColours.Base(SimSnapshotSource.LocalPlayer));
        CentreCameraOnOwnBase();
        _archive = new ArchiveTracker(ArchiveProgress.Load());
    }

    // The colour each sim player draws in: the slots that are not Closed, in player order (Human first).
    private static int[] ColoursOf(SkirmishSettings settings) => settings.PlayerSlots.Select(i => settings.Slots[i].Colour).ToArray();

    /// <inheritdoc />
    public void Exit()
    {
        PlayerColours.ResetAssignment();
        _sidebar?.Dispose();
        _cameos?.Dispose();
        _sim?.SaveReplay();
        _sim?.Commanders?.Dispose();   // M5-05: cancels any LLM call still in flight
    }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _nowMs = (long)t.TotalGameTime.TotalMilliseconds;   // M11-10c: clock for the double-tap rules
        _mouse = input.MousePosition;
        _updatedSinceDraw = true;
        UpdateHudRegions();
        UpdateCard(input);   // M11-04: before anything else reads the click
        PlayerHud? localHud = _skirmishMode && _sim is not null ? _sim.Sim.HudFor(SimSnapshotSource.LocalPlayer) : null;
        _superweapon.Update(localHud, _sim?.Sim.Tick ?? 0);
        UpdateSuperweaponRegion();
        UpdateUpgradeOffer(localHud);
        if (_upgrade is { Enabled: true } offer && input.LeftPressed && _upgradeRegion.HitTest(input.MousePosition))
        {
            _sim!.Host.Issue(offer.Command(SimSnapshotSource.LocalPlayer, _selectedBuilding));
            input.ConsumeLeft();
        }
        UpdatePackOffer();
        if (_pack is { } packOffer && input.LeftPressed && _packRegion.HitTest(input.MousePosition))
        {
            ActivatePack(packOffer);
            input.ConsumeLeft();
        }
        UpdateDiveButton();
        if (_diveShown && input.LeftPressed && _diveRegion.HitTest(input.MousePosition))
        {
            IssueDive();
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
            input.ConsumeMouseOver(_packRegion);
            input.ConsumeMouseOver(_diveRegion);
            input.ConsumeMouseOver(_hudRegion);
            if (_card is not null) input.ConsumeMouseOver(_card);   // M11-04: clicks on the card never reach the map
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

        bool chatTyping = _chat.Update(input, _sim?.Commanders, _sim?.Sim.Tick ?? 0, _s.Settings.LlmChatOn, _console is { IsOpen: true });
        if (chatTyping) input.ConsumeKeyboard();
        bool typing = _console is { IsOpen: true };
        if (typing) input.ConsumeKeyboard();

        if (!typing && !chatTyping && (_console is null && input.ConsumePressed(Keys.F1) || input.Text.Contains('?')))
        {
            input.ConsumeText();
            _s.Screens.Push(new ControlsScreen(_s));   // M11-03: F1 (or ?) lists the controls; F1 is the console in the sandbox, ? works everywhere
            return;
        }

        if (_sidebar is { Mode: not SidebarMode.None } && input.ConsumePressed(Keys.Escape)) _sidebar.CancelMode();
        if (_superweapon.IsArmed && input.ConsumePressed(Keys.Escape)) _superweapon.Cancel();   // Esc leaves targeting first
        if (_unpack is not null && input.ConsumePressed(Keys.Escape)) _unpack = null;            // M6-09: ... and unpack placement

        if (_orders is not null && _orders.AnyArmed && input.ConsumePressed(Keys.Escape)) _orders.CancelArmed();   // F6: Esc leaves an armed order first

        if (input.ConsumePressed(Keys.Escape))
        {
            _s.Screens.Push(new PauseScreen(_s, _restart, SaveTarget()));
            return;
        }

        _sidebar?.Update(input, _camera, _s.Viewport.Width, _s.Viewport.Height, _s.UiFactor);
        HandlePackKeyAndPlacement(input);   // M6-09: K, and the unpack footprint on the map

        bool debugKeys = GameKeys.DebugKeysAllowed(_skirmishMode);
        if (_sim is not null)
        {
            if (_spec is not null)
            {
                _spec.Handle(input);   // V, F2, 1-4, Space, Tab
            }
            else
            {
                if (input.ConsumePressed(GameAction.Pause)) _sim.Paused = !_sim.Paused;
                if (debugKeys && input.ConsumePressed(GameAction.DebugFog)) _sim.CycleViewer();
                if (debugKeys && _console is not { IsOpen: true } && input.ConsumePressed(GameAction.DebugSpectator)) StartSpectatorFromSandbox();
            }

            if (debugKeys && input.ConsumePressed(GameAction.DebugStep)) _sim.StepOnce();
        }

        if (debugKeys)
        {
            if (input.ConsumePressed(GameAction.DebugOccupancy)) _overlays.Occupancy = !_overlays.Occupancy;
            if (input.ConsumePressed(GameAction.DebugPaths)) _overlays.Paths = !_overlays.Paths;
            if (input.ConsumePressed(GameAction.DebugRanges)) _overlays.Ranges = !_overlays.Ranges;
        }

        if (input.ConsumePressed(GameAction.DecorToggle)) Rts.Game.Render.DecorSettings.Enabled = !Rts.Game.Render.DecorSettings.Enabled;   // M12-03
        HandleCameraKeys(input);
        HandleSelectionKeys(input);   // M11-10c: select-all-army and the control groups

        if (_orders is not null) _orders.SelectedBuilding = _selectedBuilding;   // M4-09: D unloads a selected garrisoned building
        if (_orders is not null)
        {
            foreach (GameAction action in GameKeys.OrderActions)
            {
                if (input.ConsumePressed(action)) _orders.KeyPressed(action);
            }
        }

        _feedback.Advance((float)t.ElapsedGameTime.TotalSeconds);
        Vector2 keyDirection = Vector2.Zero;
        if (input.IsDown(GameAction.ScrollLeft)) keyDirection.X -= 1f;
        if (input.IsDown(GameAction.ScrollRight)) keyDirection.X += 1f;
        if (input.IsDown(GameAction.ScrollUp)) keyDirection.Y -= 1f;
        if (input.IsDown(GameAction.ScrollDown)) keyDirection.Y += 1f;
        bool busy = _dragStart is not null || input.MiddleDragging || _sidebar is { MinimapDragging: true } || _console is { IsOpen: true } || chatTyping;
        Point client = _s.ClientSize();   // M11-10b: the band is in client pixels, as the mouse is
        Vector2 edge = CameraScroll.EdgeDirection(input.MousePosition, CameraScroll.ClientRect(client.X, client.Y, _s.Viewport.Width, _s.Viewport.Height),
            _s.Settings.EdgeScroll, _s.WindowActive(), busy);
        bool shift = input.IsDown(Keys.LeftShift) || input.IsDown(Keys.RightShift);
        _camera.PanScreen(CameraScroll.FrameDelta(keyDirection + edge, _s.Settings.ScrollSpeed, shift, (float)t.ElapsedGameTime.TotalSeconds));

        // Middle-drag: the map follows the cursor, so the view moves opposite to the mouse.
        if (input.MiddleDragging) _camera.PanScreen(-input.MouseDelta.ToVector2());

        int wheelDelta = input.WheelDelta;
        if (wheelDelta != 0) _camera.ZoomBy(wheelDelta > 0 ? 1 : -1);

        if (_sim is not null) _sim.Advance(t.ElapsedGameTime.Ticks / 10); // ticks are 100 ns; microseconds = ticks / 10
        else _source.Update();
        bool scriptCue = false;
        if (_mission is not null && _sim is not null && _mission.Advance(_sim.Sim.Tick, _s.Audio) is { } hint) { CentreCameraOn(Camera.CentreOf(hint)); scriptCue = true; }   // M8-05: camera hint, once
        // ---- M11-05 objective guidance keys: J jump, Tab next objective, O list. (Actions ObjJump / ObjCycle / ObjList in the key table.) ----
        if (_mission is not null && _sim is not null && _console is not { IsOpen: true })
        {
            Vector2 viewCentre = _camera.WorldFromScreen(new Point(MapViewport().Width / 2, MapViewport().Height / 2));
            if (_mission.Guidance.Update(input, (float)t.ElapsedGameTime.TotalSeconds, _sim.Sim.Tick, viewCentre, _s.Settings.CameraCueOnNewObjective, scriptCue) is { } focus)
                CentreCameraOn(focus);
        }
        // ---- end M11-05 ----

        if (_sim is not null && !_spectatorMode) _alerts.Observe(_source.Previous, _source.Current, _sim.Sim.HudFor(SimSnapshotSource.LocalPlayer), SimSnapshotSource.LocalPlayer);   // M11-10d
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
            _groups.Prune(key => live.IsOwnedAlive(key, SimSnapshotSource.LocalPlayer));
        }
        else
        {
            SimSnapshot shown = _source.Current;
            _selection.Prune(key => shown.Units.Any(unit => UnitKey.Of(unit) == key));
            _groups.Prune(key => shown.Units.Any(unit => UnitKey.Of(unit) == key));
        }

        PruneBuildingSelection();
        HandleSuperweaponTargeting(input);
        HandleMouse(input);

        if (!_gameOverShown && IsGameOver())
        {
            _gameOverShown = true;
            if (_gameOverOverride?.Invoke() is { } replacement) { _s.Screens.Push(replacement); return; }
            IReadOnlyList<string>? table = _sim is null ? null : SpectatorTable.Final(SpectatorTable.Collect(_sim.Sim));
            _s.Screens.Push(new GameOverScreen(_s, _restart, GameOverHeadline(), table, _spectatorMode, () => _sim?.SaveReplay()));
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        SimSnapshot snapshot = _source.Current;
        IReadOnlyList<LeptonCoord> projectiles = _sim is null ? Array.Empty<LeptonCoord>() : _sim.Projectiles;
        var frame = new SceneFrame(snapshot, _renderUnits, projectiles, _sim?.Sim, _sim?.Rules, _overlays, _selection, DragRectangle(), _groups);

        _s.Scene.HealthBarsAlways = _s.Settings.HealthBarsAlways;
        _s.Scene.LargerInfantry = _s.Settings.LargerInfantry;   // D-184
        _s.Scene.Draw(sb, _s.Pixel, frame, _camera, _s.Viewport);
        DrawSelectedBuilding(sb, snapshot);
        _feedback.Draw(sb, _s.Pixel, _camera);
        DrawPackProgress(sb, snapshot);
        DrawUnpackGhost(sb);
        _sidebar?.DrawMapOverlay(sb, _s.Pixel, _camera, snapshot, _s.Scene);
        _mission?.Guidance.DrawWorld(sb, _s.Pixel, _camera, MapViewport().Bounds, _s.UiScale);   // M11-05
        DrawHud(sb, snapshot);
        _card?.Draw(sb, _s.Pixel, _mouse, _orders is { AttackMoveArmed: true });   // M11-04
        _chat.Draw(sb, _s.Pixel, _s.UiScale, new Point(8, _s.Viewport.Height - Hud.HeightOf(1, _s.UiScale) - 12), _s.Viewport.Width - 16);
        _sidebar?.Draw(sb, _s.Pixel, snapshot);
        if (_mission is not null && _sidebar is not null) _mission.Guidance.DrawMinimap(sb, _s.Pixel, _sidebar.Layout.Minimap, snapshot.MapWidth, snapshot.MapHeight);   // M11-05
        if (_sidebar is not null) _alerts.DrawMinimapPings(sb, _s.Pixel, _sidebar.Layout.Minimap, snapshot.MapWidth, snapshot.MapHeight, snapshot.Tick);   // M11-10d
        DrawSuperweapon(sb, snapshot.Tick);
        DrawUpgradeButton(sb);
        DrawPackButton(sb);
        DrawDiveButton(sb);
        DrawSpectator(sb);
        _console?.Draw(sb, _s.Pixel, _s.Viewport);
        DrawMapCursor(sb);   // last, so it sits above everything
    }

    // M11-04: rebuild the card for this frame and act on a left click on it (the click is then used up).
    private void UpdateCard(InputFrame input)
    {
        if (_card is null) return;
        Rectangle mapView = _sidebar?.Layout.MapView ?? new Rectangle(0, 0, _s.Viewport.Width, _s.Viewport.Height);
        _card.Groups = _groups;
        _card.Refresh(mapView, _s.UiFactor, _selectedBuilding);
        if (!_card.Visible || !input.LeftPressed || _dragStart is not null) return;
        Rts.Game.Ui.Card.CardAction action = _card.Click(input.MousePosition, input.IsDown(Keys.LeftShift) || input.IsDown(Keys.RightShift));
        if (!action.Consumed) return;
        input.ConsumeLeft();
        switch (action.Kind)
        {
            case Rts.Game.Ui.Card.CardActionKind.Order:
                _orders?.KeyPressed(action.Action);
                break;
            case Rts.Game.Ui.Card.CardActionKind.SelectOnly:
                _selection.Replace(new[] { action.Unit });
                Audio.AudioBootstrap.Director?.Notify(Audio.AudioEventKind.UnitSelect);
                break;
            case Rts.Game.Ui.Card.CardActionKind.Remove:
                _selection.Remove(action.Unit);
                break;
            case Rts.Game.Ui.Card.CardActionKind.Sell when _sim is not null && !_selectedBuilding.IsNone:
                _sim.Host.Issue(SidebarCommands.Sell(SimSnapshotSource.LocalPlayer, _selectedBuilding));
                break;
            case Rts.Game.Ui.Card.CardActionKind.Repair when _sim is not null && !_selectedBuilding.IsNone:
                _sim.Host.Issue(SidebarCommands.Repair(SimSnapshotSource.LocalPlayer, _selectedBuilding));
                break;
        }
    }

    private readonly CursorPreview _cursorPreview = new();
    private bool _updatedSinceDraw;

    // M11-10g: the archive's one-line subtitle for each rules id, for the cameo tooltips (none if the archive is missing).
    private Func<string, string?>? LoadCameoBlurbs()
    {
        try
        {
            ArchiveCatalog catalog = ArchiveCatalog.Load(ArchiveCatalog.DirectoryFor(_s.RulesDir));
            return id => catalog.Find(id)?.Subtitle;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // M11-10e (audit F2, O7): over the bare map the OS cursor is hidden and a software cursor shows what a right-click would do.
    // The first armed-attack-move cursor (O2) is kept: its crosshair and label are drawn for AttackMove. When this screen is not
    // being updated (a menu is on top) the OS cursor comes back.
    private void DrawMapCursor(SpriteBatch sb)
    {
        bool updated = _updatedSinceDraw;
        _updatedSinceDraw = false;
        if (!updated) { _sidebar?.ShowOsCursor(); return; }
        if (_sidebar is not { WantsSoftwareCursor: true })
        {
            if (_orders is { AttackMoveArmed: true } && MapViewport().Bounds.Contains(_mouse)) ArmedCursor.Draw(sb, _s.Pixel, _mouse, "ATTACK-MOVE", _s.UiScale.Whole);   // O2
            return;
        }

        CursorKind kind = CursorKind.Select;
        bool overUi = _console is { IsOpen: true } || _card is { Visible: true } && _card.HitTest(_mouse) || _hudRegion.HitTest(_mouse)
            || _swRegion.HitTest(_mouse) || _upgradeRegion.HitTest(_mouse) || _packRegion.HitTest(_mouse) || _diveRegion.HitTest(_mouse)
            || _specRegion.HitTest(_mouse);
        if (!overUi && !_spectatorMode && _orders is not null && _sim is not null)
        {
            kind = _cursorPreview.Probe(_sim, _selection, _mouse, _camera, _renderUnits, _orders.AttackMoveArmed, _orders.DeployArmed);
        }

        if (kind == CursorKind.AttackMove) ArmedCursor.Draw(sb, _s.Pixel, _mouse, "ATTACK-MOVE", _s.UiScale.Whole);
        else SoftwareCursor.Draw(sb, _s.Pixel, kind, _mouse, _s.UiScale.Whole);
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
        if (input.LeftPressed && _orders is { AttackMoveArmed: true } && _console is not { IsOpen: true } && MapViewport().Bounds.Contains(mouse))
        {
            _orders.RightClick(mouse, _camera, _renderUnits);   // O2: a left click commits an armed attack-move (right click is the alias below)
            input.ConsumeLeft();
        }
        else if (input.LeftPressed)
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
                bool addBox = ShiftHeld(input);   // M11-10c: Shift+box adds to the selection
                _selection.Apply(Selection.Inside(_renderUnits, Vector2.Min(a, b), Vector2.Max(a, b), SimSnapshotSource.LocalPlayer), addBox);
                if (!addBox || _selection.Count > 0) ClearBuildingSelection();
                _clickTap.Reset();
                if (_selection.Count > 0) Audio.AudioBootstrap.Director?.Notify(Audio.AudioEventKind.UnitSelect);   // M3-INT
            }
            else
            {
                UnitKey? hit = Selection.Pick(_renderUnits, _camera.WorldFromScreen(mouse), SimSnapshotSource.LocalPlayer);
                bool addClick = ShiftHeld(input);
                if (hit is UnitKey key)
                {
                    // M11-10c: Ctrl+click or a double-click takes every unit of that type on screen; Shift+click toggles one unit.
                    if (CtrlHeld(input) || _clickTap.Tap(key, _nowMs))
                    {
                        Viewport map = MapViewport();
                        Vector2 topLeft = _camera.WorldFromScreen(new Point(0, 0));
                        Vector2 bottomRight = _camera.WorldFromScreen(new Point(map.Width, map.Height));
                        _selection.Apply(Selection.SameTypeInside(_renderUnits, topLeft, bottomRight, SimSnapshotSource.LocalPlayer, key), addClick);
                    }
                    else if (addClick) _selection.Toggle(key);
                    else _selection.Replace(new[] { key });
                    ClearBuildingSelection();
                    Audio.AudioBootstrap.Director?.Notify(Audio.AudioEventKind.UnitSelect);   // M3-INT
                }
                else if (addClick)
                {
                    _clickTap.Reset();   // Shift+click on the ground keeps the selection
                }
                else
                {
                    _selection.Clear();
                    _clickTap.Reset();
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
        var scale = _s.UiScale;
        int w = info.Max(line => BitmapFont.Measure(line, scale)) + 16;
        Hud.Panel(sb, _s.Pixel, new Point(_s.Viewport.Width - w - 8, _s.Viewport.Height - Hud.HeightOf(info.Count, scale) - 8), info, scale);
    }

    // ---- Sunrise Device (M3-18) -------------------------------------------------------------------------

    private Rectangle SuperweaponButtonRect(int index)
    {
        var scale = _s.UiScale;
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
        var scale = _s.UiScale;
        for (int i = 0; i < _superweapon.Buttons.Count; i++)
        {
            SuperweaponButton b = _superweapon.Buttons[i];
            Rectangle r = SuperweaponButtonRect(i);
            bool armed = _superweapon.Targeting.Armed == b.Building;
            Shapes.Fill(sb, _s.Pixel, r, new Color(0, 0, 0, 190));
            Shapes.Fill(sb, _s.Pixel, new Rectangle(r.X, r.Bottom - 5, r.Width * b.ProgressPermille / 1000, 5),
                b.State == SuperweaponState.Ready ? new Color(255, 200, 60) : new Color(120, 120, 120));
            if (b.State == SuperweaponState.Ready && !armed)   // M3-21: a READY button pulses gold
                Shapes.Fill(sb, _s.Pixel, r, new Color(255, 200, 60, (int)SuperweaponButtons.ReadyPulse(tick)));
            Shapes.Outline(sb, _s.Pixel, r, armed ? Color.Red : b.State == SuperweaponState.Ready ? Color.White : new Color(90, 90, 90), 2);
            Color text = b.State == SuperweaponState.Ready ? new Color(0xFF, 0xE0, 0x80) : new Color(0xB0, 0xB0, 0xB0);
            BitmapFont.Draw(sb, _s.Pixel, $"{b.Caption}  {b.Status}", new Vector2(r.X + 8, r.Y + 4), text, scale);
        }

        if (_superweapon.IsArmed && MapViewport().Bounds.Contains(_mouse))
        {
            CellCoord cell = Camera.CellAt(_camera.WorldFromScreen(_mouse));
            float left = cell.X * Camera.CellPx, top = cell.Y * Camera.CellPx;
            Shapes.Outline(sb, _s.Pixel, _camera.ScreenRect(left, top, left + Camera.CellPx, top + Camera.CellPx), Color.Red, 2);
            TelegraphRenderer.DrawBlastRadius(sb, _s.Pixel, cell, _superweapon.ArmedBlastRadius, _camera, tick);   // M3-21
        }

        if (_superweapon.MessageText(tick) is { } ready)   // M3-21: short "ready" HUD message (placeholder text key, see SuperweaponText)
        {
            int w = BitmapFont.Measure(ready, scale) + 16;
            Hud.Panel(sb, _s.Pixel, new Point(Math.Max(0, (_s.Viewport.Width - w) / 2), 8), new[] { ready }, scale);
        }

        int flash = _superweapon.Fx.FlashAlpha(tick);
        if (flash > 0) Shapes.Fill(sb, _s.Pixel, new Rectangle(0, 0, _s.Viewport.Width, _s.Viewport.Height), new Color(255, 255, 255, flash));
    }

    // ---- Hatch to Bore Head upgrade (M3-18) --------------------------------------------------------------

    private Rectangle UpgradeButtonRect()
    {
        var scale = _s.UiScale;
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
        var scale = _s.UiScale;
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

    // ---- Dive / Surface a submarine (M7-14, D-143: Deploy toggles) ----------------------------------------------

    private Rectangle DiveButtonRect()
    {
        var scale = _s.UiScale;
        int w = Math.Max(BitmapFont.Measure(DiveOffer.Caption(false), scale), BitmapFont.Measure(DiveOffer.Caption(true), scale)) + 16;
        int h = Hud.HeightOf(1, scale) + 6;
        Viewport map = MapViewport();
        return new Rectangle(map.Width - w - 8, map.Height - 3 * h - 16, w, h);   // stacked above the pack button
    }

    // The button follows the selection: shown while an own ship with submerges is selected; the caption shows the toggle's next state.
    private void UpdateDiveButton()
    {
        _diveShown = false;
        if (_sim is null || !_skirmishMode)
        {
            _diveRegion.Bounds = Rectangle.Empty;
            return;
        }

        List<ViewActor> own = _diveScratch;
        _sim.Sim.ViewFor(SimSnapshotSource.LocalPlayer).CopyOwn(own, includeBuildings: false);
        foreach (UnitKey key in _selection.Units)
        {
            if (!_sim.TryGetTypeIndex(key, out int type) || !M3Orders.IsDiver(_sim.Rules, key.Kind, type)) continue;
            _diveShown = true;
            _diveSubmerged = false;
            foreach (ViewActor a in own) if (a.Kind == ActorKind.Vehicle && a.Id == key.Id) { _diveSubmerged = a.Submerged; break; }
            break;
        }

        _diveRegion.Bounds = _diveShown ? DiveButtonRect() : Rectangle.Empty;
    }

    private readonly List<ViewActor> _diveScratch = new();

    // One Deploy per selected diver: the sim toggles each ship on its own.
    private void IssueDive()
    {
        foreach (UnitKey key in _selection.Units.ToList())
            if (_sim!.TryGetTypeIndex(key, out int type) && M3Orders.IsDiver(_sim.Rules, key.Kind, type) && _sim.TryGetPosition(key, out LeptonCoord at))
                _sim.Order(CommandKind.Deploy, key, at.ToCell());
    }

    private void DrawDiveButton(SpriteBatch sb)
    {
        if (!_diveShown) return;
        Rectangle r = DiveButtonRect();
        Shapes.Fill(sb, _s.Pixel, r, new Color(0, 0, 0, 190));
        Shapes.Outline(sb, _s.Pixel, r, Color.White, 2);
        BitmapFont.Draw(sb, _s.Pixel, DiveOffer.Caption(_diveSubmerged), new Vector2(r.X + 8, r.Y + 4), new Color(0xFF, 0xE0, 0x80), _s.UiScale);
    }

    // ---- Pack / unpack a Mother Rig (M6-09) ------------------------------------------------------------

    private Rectangle PackButtonRect()
    {
        var scale = _s.UiScale;
        int w = BitmapFont.Measure(LeftBehindText.Placeholder(LeftBehindText.UnpackKey), scale) + 16;
        int h = Hud.HeightOf(1, scale) + 6;
        Viewport map = MapViewport();
        return new Rectangle(map.Width - w - 8, map.Height - 2 * h - 12, w, h);   // stacked above the upgrade button
    }

    // The button follows the selection: a selected own rig building packs, a selected own packed rig unpacks.
    private void UpdatePackOffer()
    {
        _pack = null;
        _packSubject = EntityId.None;
        if (_sim is null || _orders is null || !_skirmishMode)
        {
            _packRegion.Bounds = Rectangle.Empty;
            return;
        }

        PlayerView view = _sim.Sim.ViewFor(SimSnapshotSource.LocalPlayer);
        if (!_selectedBuilding.IsNone && _selectedBuildingType >= 0)
        {
            int progress = view.PackProgressPermille(new ActorRef(ActorKind.Building, _selectedBuilding));
            _pack = PackOffer.ForBuilding(_sim.Rules, _selectedBuildingType, progress);
            if (_pack is not null) _packSubject = _selectedBuilding;
        }
        else
        {
            foreach (UnitKey key in _selection.Units)
            {
                if (key.Kind != ActorKind.Vehicle) continue;
                int into = view.UnpackTypeOf(key.Id);
                if (into < 0) continue;
                _pack = PackOffer.ForPackedRig(_sim.Rules, into, view.PackProgressPermille(new ActorRef(ActorKind.Vehicle, key.Id)));
                if (_pack is not null) _packSubject = key.Id;
                break;
            }
        }

        _packRegion.Bounds = _pack is null ? Rectangle.Empty : PackButtonRect();
    }

    private void ActivatePack(PackOffer offer)
    {
        if (_sim is null || !offer.Enabled || _packSubject.IsNone) return;
        byte local = SimSnapshotSource.LocalPlayer;
        if (offer.Action == PackAction.Pack)
        {
            _sim.Host.Issue(PackOffer.PackCommand(local, _packSubject));
            return;
        }

        EntityId rig = _packSubject;
        BuildingType into = _sim.Rules.Buildings[offer.UnpackType];
        _sidebar?.CancelMode();
        _unpack = new UnpackPlacement(rig, offer.UnpackType, into.FootprintW, into.FootprintH,
            topLeft => _sim.Sim.ViewFor(local).CanUnpackAt(rig, topLeft));
    }

    // K packs or unpacks (or leaves unpack placement); in placement the footprint follows the cursor, a left click on a valid site
    // sends the rig, a right click leaves.
    private void HandlePackKeyAndPlacement(InputFrame input)
    {
        if (_sim is null || _orders is null) return;
        if (input.ConsumePressed(GameAction.Pack))
        {
            if (_unpack is not null) _unpack = null;
            else if (_pack is { } offer) ActivatePack(offer);
        }

        if (_unpack is not { } placing) return;
        if (_sim.Sim.ViewFor(SimSnapshotSource.LocalPlayer).UnpackTypeOf(placing.Rig) < 0)
        {
            _unpack = null;   // the rig died, or is no longer packed
            return;
        }

        if (!MapViewport().Bounds.Contains(input.MousePosition)) return;
        placing.Hover(Camera.CellAt(_camera.WorldFromScreen(input.MousePosition)));
        if (input.RightPressed)
        {
            _unpack = null;
            input.ConsumeRight();
        }
        else if (input.LeftPressed)
        {
            if (placing.Click(SimSnapshotSource.LocalPlayer) is Command unpack)
            {
                _sim.Host.Issue(unpack);
                _unpack = null;
            }

            input.ConsumeLeft();   // never also select or box-drag
        }
    }

    private void DrawPackButton(SpriteBatch sb)
    {
        if (_pack is not PackOffer offer) return;
        var scale = _s.UiScale;
        Rectangle r = PackButtonRect();
        Shapes.Fill(sb, _s.Pixel, r, new Color(0, 0, 0, 190));
        Shapes.Outline(sb, _s.Pixel, r, _unpack is not null ? Color.Red : offer.Enabled ? Color.White : new Color(90, 90, 90), 2);
        BitmapFont.Draw(sb, _s.Pixel, offer.Caption, new Vector2(r.X + 8, r.Y + 4),
            offer.Enabled ? new Color(0xFF, 0xE0, 0x80) : new Color(0x90, 0x90, 0x90), scale);
        if (!offer.Enabled && r.Contains(_mouse))
        {
            int w = BitmapFont.Measure(offer.Reason, scale) + 16;
            Hud.Panel(sb, _s.Pixel, new Point(Math.Max(0, r.Right - w), r.Y - Hud.HeightOf(1, scale) - 4), new[] { offer.Reason }, scale);
        }
    }

    // The unpack footprint: green where the sim would accept it, red elsewhere.
    private void DrawUnpackGhost(SpriteBatch sb)
    {
        if (_unpack is not { HasGhost: true } p) return;
        float left = p.TopLeft.X * Camera.CellPx, top = p.TopLeft.Y * Camera.CellPx;
        Rectangle r = _camera.ScreenRect(left, top, left + p.FootprintW * Camera.CellPx, top + p.FootprintH * Camera.CellPx);
        Color c = p.Valid ? new Color(0x5E, 0xD0, 0x5A) : new Color(0xE0, 0x4A, 0x32);
        Shapes.Fill(sb, _s.Pixel, r, Shapes.Alpha(c, 90));
        Shapes.Outline(sb, _s.Pixel, r, c, 2);
    }

    // Pack / unpack progress of the local player's rigs, as a gold bar like the build progress bar.
    private void DrawPackProgress(SpriteBatch sb, SimSnapshot snapshot)
    {
        if (_sim is null || !_skirmishMode) return;
        byte local = SimSnapshotSource.LocalPlayer;
        PlayerView view = _sim.Sim.ViewFor(local);
        foreach (BuildingView b in snapshot.Buildings)
        {
            if (b.Ghost || b.Player != local) continue;
            int permille = view.PackProgressPermille(new ActorRef(ActorKind.Building, b.Id));
            if (permille <= 0) continue;
            float left = b.Cell.X * Camera.CellPx, top = b.Cell.Y * Camera.CellPx;
            DrawProgressBar(sb, _camera.ScreenRect(left + 4, top + 4, left + b.FootprintW * Camera.CellPx - 4, top + 12), permille);
        }

        foreach (RenderUnit u in _renderUnits)
        {
            if (u.View.Player != local || u.View.Kind != UnitKind.Vehicle) continue;
            int permille = view.PackProgressPermille(new ActorRef(ActorKind.Vehicle, u.View.Id));
            if (permille > 0) DrawProgressBar(sb, _camera.ScreenRect(u.World.X - 24, u.World.Y - 38, u.World.X + 24, u.World.Y - 30), permille);
        }
    }

    private void DrawProgressBar(SpriteBatch sb, Rectangle r, int permille)
    {
        Shapes.Fill(sb, _s.Pixel, r, new Color(0, 0, 0, 190));
        Shapes.Fill(sb, _s.Pixel, new Rectangle(r.X, r.Y, PackOffer.BarFill(permille, r.Width), r.Height), new Color(255, 200, 60));
        Shapes.Outline(sb, _s.Pixel, r, new Color(90, 90, 90));
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
            // O4: a selected production building shows its rally flag and a line to it
            if (_sim is not null && _sim.Rules.Buildings[b.TypeIndex].Produces is { } lane
                && _sim.Sim.Production.RallyCell(SimSnapshotSource.LocalPlayer, lane) is { } rally)
            {
                OrderFeedback.DrawRally(sb, _s.Pixel, _camera, new Vector2(left + b.FootprintW * Camera.CellPx / 2f, top + b.FootprintH * Camera.CellPx / 2f), rally);
            }
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

    // ---- M11-10c selection toolkit: control groups (UI state, not saved), select-all-army, double-tap timing ----
    private readonly ControlGroups _groups = new();
    private readonly TapTracker<int> _groupTap = new();
    private readonly TapTracker<UnitKey> _clickTap = new();
    private long _nowMs;
    private static readonly GameAction[] GroupActions =
    {
        GameAction.Group1, GameAction.Group2, GameAction.Group3, GameAction.Group4, GameAction.Group5,
        GameAction.Group6, GameAction.Group7, GameAction.Group8, GameAction.Group9,
    };

    private static bool ShiftHeld(InputFrame input) => input.IsDown(Keys.LeftShift) || input.IsDown(Keys.RightShift);

    private static bool CtrlHeld(InputFrame input) => input.IsDown(Keys.LeftControl) || input.IsDown(Keys.RightControl);

    // The select-all-army key, then 1..9 (recall; twice quickly centres the view), Ctrl+n (set), Shift+n (add the selection).
    private void HandleSelectionKeys(InputFrame input)
    {
        if (_spectatorMode || _sim is null) return;
        if (input.ConsumePressed(GameAction.SelectAll))
        {
            SimRules rules = _sim.Rules;
            _selection.Replace(Selection.Army(_renderUnits, SimSnapshotSource.LocalPlayer, unit => Selection.IsCombatUnit(rules, unit)));
            ClearBuildingSelection();
            if (_selection.Count > 0) Audio.AudioBootstrap.Director?.Notify(Audio.AudioEventKind.UnitSelect);
        }

        for (int i = 0; i < GroupActions.Length; i++)
        {
            if (!input.ConsumePressed(GroupActions[i])) continue;
            int number = i + 1;
            if (CtrlHeld(input)) { _groups.Set(number, _selection.Units); _groupTap.Reset(); continue; }
            if (ShiftHeld(input)) { _groups.Add(number, _selection.Units); continue; }
            IReadOnlyList<UnitKey> members = _groups.Members(number);
            if (members.Count == 0) continue;
            bool twice = _groupTap.Tap(number, _nowMs);
            _selection.Replace(members);
            ClearBuildingSelection();
            Audio.AudioBootstrap.Director?.Notify(Audio.AudioEventKind.UnitSelect);
            if (twice && GroupCentre(members) is { } centre) CentreCameraOn(centre);
        }
    }

    private Vector2? GroupCentre(IReadOnlyList<UnitKey> members)
    {
        Vector2 sum = Vector2.Zero;
        int n = 0;
        foreach (RenderUnit unit in _renderUnits)
        {
            if (!members.Contains(UnitKey.Of(unit.View))) continue;
            sum += unit.World;
            n++;
        }

        return n == 0 ? null : sum / n;
    }

    private Rts.Game.Ui.Alerts.AlertController? _alertsBacking;   // M11-10d
    private Rts.Game.Ui.Alerts.AlertController _alerts => _alertsBacking ??= new Rts.Game.Ui.Alerts.AlertController(u => u.Kind == UnitKind.Vehicle && _sim is not null && _sim.Rules.Vehicles[u.TypeIndex].HarvestCapacity > 0);
    private readonly Vector2?[] _bookmarks = new Vector2?[4];   // M11-02: camera bookmarks F5..F8 (Ctrl+key stores, key recalls)

    // Home centres on the player's base; Ctrl+F5..F8 store the view centre, F5..F8 recall it.
    private void HandleCameraKeys(InputFrame input)
    {
        if (_sim is null) return;
        if (input.ConsumePressed(GameAction.CentreBase)) CentreCameraOnOwnBase();
        if (input.ConsumePressed(GameAction.LastAlert) && _alerts.JumpTarget(_source.Current.Tick) is { } alertAt) CentreCameraOn(alertAt);   // M11-10d
        bool ctrl = input.IsDown(Keys.LeftControl) || input.IsDown(Keys.RightControl);
        GameAction[] keys = { GameAction.Bookmark1, GameAction.Bookmark2, GameAction.Bookmark3, GameAction.Bookmark4 };
        for (int i = 0; i < keys.Length; i++)
        {
            if (!input.ConsumePressed(keys[i])) continue;
            Viewport map = MapViewport();
            if (ctrl) _bookmarks[i] = _camera.WorldFromScreen(new Point(map.Width / 2, map.Height / 2));
            else if (_bookmarks[i] is { } at) CentreCameraOn(at);
        }
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
        CentreCameraOn(centre.Value);
    }

    /// <summary>Centres the map view on a world point, clamped to the map (the same move as the start view).</summary>
    private void CentreCameraOn(Vector2 centre)
    {
        int mapWidth = _s.Viewport.Width - SidebarLayout.WidthFor(_s.Viewport.Width, _s.UiFactor);
        Vector2 size = new Vector2(Math.Max(1, mapWidth), _s.Viewport.Height) / _camera.Zoom;
        Vector2 target = centre - size / 2f;
        _camera.PanScreen((target - _camera.WorldFromScreen(Point.Zero)) * _camera.Zoom);
        _camera.Clamp(_source.Current.MapWidth, _source.Current.MapHeight, new Viewport(0, 0, Math.Max(1, mapWidth), _s.Viewport.Height));
    }

    private string GameOverHeadline()
    {
        if (_sim is null) return GameOverScreen.Headline(false, -1, 0);
        if (_spectatorMode) return GameOverScreen.SpectatorHeadline(_sim.Sim.WinningTeam);
        if (_missionGame is { } mg) return GameOverScreen.Headline(!mg.LocalWon, mg.LocalWon ? 0 : -1, 0);   // S18 MIS-01
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
        var lines = new List<string>();
        bool debugMode = GameKeys.DebugKeysAllowed(_skirmishMode);
        if (_s.Settings.ShowDiagnostics)
            lines.Add($"tick {snapshot.Tick}  hash {snapshot.StateHash:x16}  fps {_s.Fps}  zoom {_camera.ZoomLabel}");
        if (_sim is { Paused: true }) lines.Add(debugMode ? "PAUSED (. = step)" : "PAUSED");

        if (_orders is { DeployArmed: true }) lines.Add($"DEPLOY: right-click the cell to dig at ({KeyNames.Of(GameKeys.Active.Get(GameAction.Deploy))} cancels)");
        if (_unpack is not null) lines.Add(LeftBehindText.Placeholder(LeftBehindText.UnpackPlacementKey));

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
        var scale = _s.UiScale;
        List<string> lines = HudLines(_source.Current);
        _hudRegion.Bounds = lines.Count == 0 ? Rectangle.Empty : PanelRect(new Point(8, 8), lines, scale);
        if (_specPanel is not null)
        {
            _specPanel.Origin = new Point(8, 8 + Hud.HeightOf(lines.Count, scale) + 4);
            _specRegion.Bounds = _specPanel.Bounds(scale);
        }
        if (_mission is not null) _mission.MapViewWidth = MapViewport().Width;   // M11-05
        _mission?.UpdateRegion(lines.Count, scale);
    }

    private static Rectangle PanelRect(Point origin, IReadOnlyList<string> lines, UiUnit scale) =>
        new(origin.X, origin.Y, lines.Max(line => BitmapFont.Measure(line, scale)) + 16, Hud.HeightOf(lines.Count, scale));

    /// <summary>
    /// Top-left: PAUSED, and with Settings > Show diagnostics tick, state hash, frames per second and zoom. In the sandbox a second
    /// line: alive units per player, selection, overlays, fog viewer. There are no key hints (F1 opens the controls list).
    /// </summary>
    private void DrawHud(SpriteBatch sb, SimSnapshot snapshot)
    {
        var scale = _s.UiScale;
        List<string> hud = HudLines(snapshot);
        Hud.Panel(sb, _s.Pixel, new Point(8, 8), hud, scale);

        if (_sim is not null) _mission?.Draw(sb, _s.Pixel, scale, hud.Count, _s.Viewport.Height - 8, snapshot.Tick);
        if (_sim is not null && !_spectatorMode)   // M11-10d: alert stack, clear of the objective bar and the buttons
        {
            Rectangle mapView = _sidebar?.Layout.MapView ?? new Rectangle(0, 0, _s.Viewport.Width, _s.Viewport.Height);
            Rectangle banner = _mission is null ? Rectangle.Empty : new Rectangle(_mission.ObjectivesRegion.Bounds.X, _mission.ObjectivesRegion.Bounds.Y, _mission.ObjectivesRegion.Bounds.Width, _mission.ObjectivesRegion.Bounds.Height + 48);
            _alerts.DrawStack(sb, _s.Pixel, mapView, scale, snapshot.Tick, new[] { banner, _hudRegion.Bounds, _swRegion.Bounds, _upgradeRegion.Bounds, _packRegion.Bounds, _diveRegion.Bounds, _specRegion.Bounds });
        }
    }
}
