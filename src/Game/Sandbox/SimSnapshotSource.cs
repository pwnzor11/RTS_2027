// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Saves;
using Rts.Scripting;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Sandbox;

/// <summary>
/// The sandbox's thin wrapper over <see cref="SimHost"/> (which steps the simulation): builds the sim, replay recording
/// and scenario, and adds the debug view's order validation. Every command the debug view issues (or a script issues)
/// goes through <see cref="Enqueue"/>. The renderer and the overlays read it between steps.
/// </summary>
public sealed class SimSnapshotSource : ISnapshotSource
{
    /// <summary>The player this debug view controls.</summary>
    public const byte LocalPlayer = 0;

    /// <summary>Number of players in the sandbox.</summary>
    public const int PlayerCount = 2;

    /// <summary>Tick at which player 1's units attack-move toward player 0's start (sandbox only).</summary>
    public const int AttackScriptTick = 40;

    /// <summary>Where the session replay is written, relative to the working directory.</summary>
    public const string ReplayPath = "replays/sandbox_last.rpl";

    /// <summary>The previous session's replay: <see cref="ReplayPath"/> is moved here when a new session starts.</summary>
    public const string PreviousReplayPath = "replays/sandbox_prev.rpl";

    /// <summary>The command that checks the session replay.</summary>
    public const string VerifyCommand =
        "dotnet run --project src/Tools -- replay verify replays/sandbox_last.rpl --map data/maps/test_32.map.toml";

    /// <summary>The seed the sandbox has always used.</summary>
    public const ulong DefaultSeed = 2027;

    /// <summary>Where a menu skirmish writes its replay.</summary>
    public const string SkirmishReplayPath = "replays/skirmish_last.rpl";

    /// <summary>The previous skirmish's replay.</summary>
    public const string SkirmishPreviousReplayPath = "replays/skirmish_prev.rpl";

    /// <summary>Where a campaign mission writes its replay.</summary>
    public const string MissionReplayPath = "replays/mission_last.rpl";

    /// <summary>The previous mission's replay.</summary>
    public const string MissionPreviousReplayPath = "replays/mission_prev.rpl";

    private readonly SimHost _host;
    private readonly string? _verifyCommand = VerifyCommand;
    private readonly MapData _map;
    private readonly SkirmishGame? _game;   // set for a menu skirmish: its map is saved beside the replay
    private readonly ReplayRecording? _recording;   // null only if it could not be opened
    private readonly Stopwatch _clock = Stopwatch.StartNew(); // only for the ISnapshotSource.Update() convenience
    private long _lastUs;

    /// <summary>Loads the rules and the test map, spawns the chosen setup and starts recording.</summary>
    /// <param name="rulesDir">The data/rules directory.</param>
    /// <param name="empty">True for the legacy 3-tank setup with no scripted attack.</param>
    /// <param name="seed">The simulation seed.</param>
    /// <exception cref="Rts.Data.RulesException">The rules data is missing or invalid.</exception>
    public SimSnapshotSource(string rulesDir, bool empty, ulong seed = DefaultSeed)
    {
        RuleSet rules = RulesLoader.Load(rulesDir);
        _map = MapFile.Load(Path.Combine(rulesDir, "..", "maps", SandboxScenario.MapFileName));
        var setup = new SimSetup(seed, _map, PlayerCount, rules.Sim, rules.RulesHash) { AllowDebug = true };
        var sim = new Simulation(setup);
        _recording = new ReplayRecording(sim, RulesBundle.ReadDirectory(rulesDir), ReplayPath, PreviousReplayPath);
        _host = new SimHost(sim, _recording);

        List<OwnedUnit> army = SandboxScenario.Populate(sim, _map, rules.Sim, _recording, empty);
        if (!empty)
        {
            CellCoord target = _map.StartPositions[LocalPlayer];
            foreach (OwnedUnit unit in army)
            {
                if (unit.Player != LocalPlayer)
                {
                    Enqueue(AttackScriptTick, unit.Player, CommandKind.AttackMove, unit.Key, target, default);
                }
            }
        }

        _host.RefreshSnapshots();
    }

    /// <summary>
    /// Wraps a menu skirmish (<see cref="SkirmishGame"/>): the sim is already set up, the AI joins the host's tick loop, and the
    /// session is recorded to <see cref="SkirmishReplayPath"/>. The viewer is player 0 (fog applies).
    /// </summary>
    /// <param name="game">The skirmish to host.</param>
    /// <param name="rulesDir">The data/rules directory (its files go into the replay header).</param>
    /// <param name="replayPath">Where to record (tests); null = <see cref="SkirmishReplayPath"/>. The previous file is kept beside it as <c>.prev</c>.</param>
    public SimSnapshotSource(SkirmishGame game, string rulesDir, string? replayPath = null)
    {
        string last = replayPath ?? SkirmishReplayPath;
        string previous = replayPath is null ? SkirmishPreviousReplayPath : replayPath + ".prev";
        _map = game.Map;
        _game = game;
        KeepPreviousBesideMap(last, previous);
        _recording = new ReplayRecording(game.Sim, RulesBundle.ReadDirectory(rulesDir), last, previous);
        _host = new SimHost(game.Sim, _recording) { Ai = game.Ai };
        // A generated or user map is saved beside the replay when it is finished (SaveReplay), and the check names that copy.
        _verifyCommand = game.Curated
            ? $"dotnet run --project src/Tools -- replay verify {last} --map data/maps/{SkirmishGame.CuratedMapFile}"
            : $"dotnet run --project src/Tools -- replay verify {last} --map {SkirmishGame.MapBesideReplay(last)}";
        _host.RefreshSnapshots();
    }

    // The last skirmish replay becomes the previous one, so its beside-map moves with it.
    private static void KeepPreviousBesideMap(string lastReplay, string previousReplay)
    {
        try
        {
            string last = Path.GetFullPath(SkirmishGame.MapBesideReplay(lastReplay));
            if (File.Exists(last)) File.Move(last, Path.GetFullPath(SkirmishGame.MapBesideReplay(previousReplay)), overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A stale map beside a replay is ignored when its hash does not match.
        }
    }

    /// <summary>
    /// Wraps a campaign mission (<see cref="MissionGame"/>, M2-15): the AI joins the host's tick loop, the session is recorded to
    /// <see cref="MissionReplayPath"/> with the mission id and script hash (M3-19), and the viewer is player 0.
    /// </summary>
    /// <param name="game">The mission to host.</param>
    public SimSnapshotSource(MissionGame game)
    {
        _map = game.Map;
        _recording = new ReplayRecording(game.Sim, RulesBundle.ReadDirectory(game.RulesDir), MissionReplayPath, MissionPreviousReplayPath,
            game.ReplayRef);
        _verifyCommand = $"dotnet run --project src/Tools -- replay play {MissionReplayPath}";
        _host = new SimHost(game.Sim, _recording) { Ai = game.Ai };
        SaveTemplate = game.SaveTemplate();
        _host.RefreshSnapshots();
    }

    /// <summary>
    /// Wraps a game restored from a save (M3-20). The AI joins the host's tick loop and the command queue is put back. Nothing is
    /// recorded: a replay has to start at tick 0, which a loaded game is past.
    /// </summary>
    /// <param name="loaded">The restored game (skirmish or mission).</param>
    /// <param name="appSettings">M5-10: app settings to bring back the save's LLM commanders; null = none.</param>
    public SimSnapshotSource(LoadedGame loaded, Settings? appSettings = null)
    {
        if (appSettings is not null && loaded.Ai is not null && loaded.Header.Skirmish is { } skirmish)
            _loadedCommanders = CommanderSet.Attach(loaded.Ai, skirmish.AiPlayers, appSettings);
        _map = loaded.Map;
        _verifyCommand = null;
        _host = new SimHost(loaded.Sim) { Ai = loaded.Ai };
        _host.RestoreQueue(loaded.Header.Pending, loaded.Header.Sequences);
        SaveTemplate = loaded.Header;
        _host.RefreshSnapshots();
    }

    /// <summary>The menu skirmish this source wraps, or null for the sandbox, a mission or a loaded game.</summary>
    public SkirmishGame? Game => _game;

    /// <summary>M5: the LLM commanders and chat of this game (a menu skirmish's, or those rebuilt for a loaded save), or null.</summary>
    public CommanderSet? Commanders => _game?.CommanderSet ?? _loadedCommanders;

    private readonly CommanderSet? _loadedCommanders;

    /// <summary>The fixed part of this game's save header, or null when the game cannot be saved (the sandbox).</summary>
    public GameSaveHeader? SaveTemplate { get; set; }

    /// <summary>The host that steps the simulation.</summary>
    public SimHost Host => _host;

    /// <summary>The simulation. Read it between steps only; never mutate it outside <see cref="Enqueue"/>.</summary>
    public Simulation Sim => _host.Sim;

    /// <summary>The map the sim runs on.</summary>
    public MapData Map => _map;

    /// <summary>The rules the sim runs on.</summary>
    public SimRules Rules => _host.Sim.Setup.Rules;

    /// <inheritdoc />
    public SimSnapshot Current => _host.Current;

    /// <inheritdoc />
    public SimSnapshot Previous => _host.Previous;

    /// <summary>0..1 from <see cref="Previous"/> to <see cref="Current"/>; 1 while paused.</summary>
    public float Alpha => _host.Alpha;

    /// <summary>Projectile positions in leptons at the current tick (not interpolated).</summary>
    public IReadOnlyList<LeptonCoord> Projectiles => _host.Projectiles;

    /// <summary>Which player's view the snapshot shows: 0, 1, or -1 for the omniscient view.</summary>
    public int Viewer => _host.Viewer;

    /// <summary>True while the simulation is paused. Pausing drops any partly accumulated time.</summary>
    public bool Paused
    {
        get => _host.Paused;
        set => _host.Paused = value;
    }

    /// <summary>Single-player game speed in percent of normal (50..200). Front end only; see <see cref="SimHost.SpeedPercent"/>.</summary>
    public int SpeedPercent
    {
        get => _host.SpeedPercent;
        set => _host.SpeedPercent = value;
    }

    /// <summary>Ticks run per tick interval while running (1 = normal speed). Front end only; set by the debug console.</summary>
    public int SpeedMultiplier
    {
        get => _host.SpeedMultiplier;
        set => _host.SpeedMultiplier = value;
    }

    /// <summary>Current simulation tick.</summary>
    public int Tick => _host.Tick;

    /// <summary>Advances by this source's own wall clock (legacy convenience; the game screen calls <see cref="Advance"/>).</summary>
    public void Update()
    {
        long nowUs = _clock.Elapsed.Ticks / 10;
        long elapsedUs = nowUs - _lastUs;
        _lastUs = nowUs;
        _host.Advance(elapsedUs);
    }

    /// <summary>Advances by caller-supplied elapsed time (see <see cref="SimHost.Advance"/>).</summary>
    public int Advance(long elapsedMicroseconds) => _host.Advance(elapsedMicroseconds);

    /// <summary>While paused, advances the simulation by exactly one tick.</summary>
    public void StepOnce()
    {
        if (_host.Paused) _host.StepOnce();
    }

    /// <summary>Advances the simulation by exactly <paramref name="count"/> ticks, ignoring the clock (headless runs).</summary>
    public void StepTicks(int count) => _host.StepTicks(count);

    /// <summary>Moves to the next fog view: player 0, player 1, omniscient, then player 0 again.</summary>
    public void CycleViewer() => _host.CycleViewer();

    /// <summary>Issues one order for one of the local player's units, to run on the next tick.</summary>
    public void Order(CommandKind kind, UnitKey subject, CellCoord cell = default, ActorRef target = default) =>
        Enqueue(Tick + SimHost.CommandDelayTicks, LocalPlayer, kind, subject, cell, target);

    /// <summary>
    /// The one way orders reach the sim. Queues a command for <paramref name="execTick"/> and records it. Ignored when
    /// the subject is not a live unit of <paramref name="player"/>, when the tick has passed, or when a move-type
    /// order targets a cell off the map.
    /// </summary>
    public void Enqueue(int execTick, byte player, CommandKind kind, UnitKey subject, CellCoord cell, ActorRef target)
    {
        if (execTick <= Tick || !IsOwnedAlive(subject, player)) return;
        bool needsCell = kind is CommandKind.Move or CommandKind.AttackMove or CommandKind.Guard;
        if (needsCell && !_map.Contains(cell)) return;

        _host.IssueOrder(player, kind, subject, cell, target, execTick);
    }

    /// <summary>
    /// Issues one debug operation (M1.5) to run on the next tick, and records it in the replay. The sim applies it only
    /// because the sandbox sets <see cref="SimSetup.AllowDebug"/>. Spawn operations need a cell on the map (ignored
    /// otherwise). <paramref name="arg2"/> and <paramref name="subject"/> carry the rest as <see cref="DebugOp"/> documents.
    /// </summary>
    /// <param name="op">The debug operation (Command.Arg).</param>
    /// <param name="arg2">The operation's second int (Command.Arg2): type index, amount, or ActorKind.</param>
    /// <param name="cell">The cell for spawn operations.</param>
    /// <param name="subject">The unit for Kill.</param>
    /// <param name="player">The player the operation acts for (Command.Player).</param>
    public void EnqueueDebug(DebugOp op, int arg2, CellCoord cell, EntityId subject = default, byte player = LocalPlayer)
    {
        bool spawns = op is DebugOp.SpawnVehicle or DebugOp.SpawnInfantry;
        if (spawns && !_map.Contains(cell)) return;

        _host.Issue(new Command(0, player, 0, CommandKind.Debug, subject, EntityId.None, cell, (int)op, arg2));
    }

    /// <summary>True when the key names a live unit owned by <paramref name="player"/>.</summary>
    public bool IsOwnedAlive(UnitKey key, byte player)
    {
        if (key.Kind == ActorKind.Vehicle)
            return Sim.Units.Slots.IsAlive(key.Id) && Sim.Units.Player[key.Id.Index] == player;
        if (key.Kind == ActorKind.Infantry)
            return Sim.Infantry.Slots.IsAlive(key.Id) && Sim.Infantry.Player[key.Id.Index] == player;
        return false;
    }

    /// <summary>Position of a live unit in leptons, from the sim (not from the snapshot, so fog does not hide it).</summary>
    public bool TryGetPosition(UnitKey key, out LeptonCoord position)
    {
        position = default;
        if (key.Kind == ActorKind.Vehicle && Sim.Units.Slots.IsAlive(key.Id))
        {
            position = Sim.Units.Position[key.Id.Index];
            return true;
        }
        if (key.Kind == ActorKind.Infantry && Sim.Infantry.Slots.IsAlive(key.Id))
        {
            position = Sim.Infantry.Position[key.Id.Index];
            return true;
        }
        return false;
    }

    /// <summary>Rules type index of a live vehicle or infantry unit (read from the sim, like <see cref="TryGetPosition"/>).</summary>
    public bool TryGetTypeIndex(UnitKey key, out int typeIndex)
    {
        typeIndex = -1;
        if (key.Kind == ActorKind.Vehicle && Sim.Units.Slots.IsAlive(key.Id))
        {
            typeIndex = Sim.Units.TypeIndex[key.Id.Index];
            return true;
        }
        if (key.Kind == ActorKind.Infantry && Sim.Infantry.Slots.IsAlive(key.Id))
        {
            typeIndex = Sim.Infantry.TypeIndex[key.Id.Index];
            return true;
        }
        return false;
    }

    /// <summary>Every live vehicle and infantry unit of a player, in slot order.</summary>
    public List<UnitKey> AliveUnits(byte player)
    {
        var list = new List<UnitKey>();
        for (int i = 1; i < Sim.Units.Capacity; i++)
            if (Sim.Units.Slots.IsIndexAlive(i) && Sim.Units.Player[i] == player)
                list.Add(new UnitKey(ActorKind.Vehicle, Sim.Units.Slots.IdOf(i)));
        for (int i = 1; i < Sim.Infantry.Capacity; i++)
            if (Sim.Infantry.Slots.IsIndexAlive(i) && Sim.Infantry.Player[i] == player)
                list.Add(new UnitKey(ActorKind.Infantry, Sim.Infantry.Slots.IdOf(i)));
        return list;
    }

    /// <summary>Ends the session replay (<see cref="ReplayPath"/> is streamed from the start of the session, so a crash
    /// keeps it) and prints the command that checks it.</summary>
    /// <returns>False if the file could not be written.</returns>
    public bool SaveReplay()
    {
        if (_recording is null) return false;
        bool ok = _recording.Finish();
        if (ok && _game is not null)
        {
            // M4-INT2: a replay of a generated or user map needs that map to be checked or watched.
            try { _game.SaveMapBeside(_recording.FilePath); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"error: map not saved beside the replay: {e.Message}");
            }
        }
        if (ok && _verifyCommand is not null) Console.WriteLine($"verify with: {_verifyCommand}");
        return ok;
    }
}
