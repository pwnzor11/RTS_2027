using System.Diagnostics;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Sandbox;

/// <summary>
/// Runs the real <see cref="Simulation"/> on a fixed 20 Hz accumulator and hands its snapshots to the renderer. Owns
/// the one order path: every command the debug view issues (or a script issues) goes through <see cref="Enqueue"/>,
/// which queues it for the sim and records it in the replay. Also owns the fog viewer and pause/step.
/// The sim is only stepped here; the renderer and the overlays read it between steps.
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

    private const ulong Seed = 2027;
    private const long TickUs = 1_000_000 / SimConstants.TicksPerSecond; // 50 ms
    private const long MaxAccumulatedUs = 250_000;                       // avoid a burst after a stall

    private readonly Simulation _sim;
    private readonly MapData _map;
    private readonly ReplayRecording _recording;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<Command> _pending = new();
    private long _lastUs;
    private long _accumulatedUs;
    private ushort _sequence;
    private bool _paused;
    private int _viewer;
    private SimSnapshot _previous;
    private SimSnapshot _current;
    private IReadOnlyList<LeptonCoord> _projectiles = Array.Empty<LeptonCoord>();

    /// <summary>Loads the rules and the test map, spawns the chosen setup and starts recording.</summary>
    /// <param name="rulesDir">The data/rules directory.</param>
    /// <param name="empty">True for the legacy 3-tank setup with no scripted attack.</param>
    /// <exception cref="Rts.Data.RulesException">The rules data is missing or invalid.</exception>
    public SimSnapshotSource(string rulesDir, bool empty)
    {
        RuleSet rules = RulesLoader.Load(rulesDir);
        _map = MapFile.Load(Path.Combine(rulesDir, "..", "maps", SandboxScenario.MapFileName));
        var setup = new SimSetup(Seed, _map, PlayerCount, rules.Sim, rules.RulesHash);
        _sim = new Simulation(setup);
        _recording = new ReplayRecording(_sim, RulesBundle.ReadDirectory(rulesDir), ReplayPath, PreviousReplayPath);

        List<OwnedUnit> army = SandboxScenario.Populate(_sim, _map, rules.Sim, _recording, empty);
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

        _current = _sim.Snapshot(_viewer);
        _previous = _current;
        _projectiles = CaptureProjectiles();
    }

    /// <summary>The simulation. Read it between steps only; never mutate it outside <see cref="Enqueue"/>.</summary>
    public Simulation Sim => _sim;

    /// <summary>The map the sim runs on.</summary>
    public MapData Map => _map;

    /// <summary>The rules the sim runs on.</summary>
    public SimRules Rules => _sim.Setup.Rules;

    /// <inheritdoc />
    public SimSnapshot Current => _current;

    /// <inheritdoc />
    public SimSnapshot Previous => _previous;

    /// <summary>
    /// 0..1 from <see cref="Previous"/> to <see cref="Current"/>. While paused it is 1, so a single step shows
    /// its result at once.
    /// </summary>
    public float Alpha => _paused ? 1f : _accumulatedUs / (float)TickUs;

    /// <summary>Projectile positions in leptons at the current tick (not interpolated).</summary>
    public IReadOnlyList<LeptonCoord> Projectiles => _projectiles;

    /// <summary>Which player's view the snapshot shows: 0, 1, or -1 for the omniscient view.</summary>
    public int Viewer => _viewer;

    /// <summary>True while the simulation is paused. Pausing drops any partly accumulated time.</summary>
    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            if (value) _accumulatedUs = 0;
        }
    }

    /// <summary>Current simulation tick.</summary>
    public int Tick => _sim.Tick;

    /// <inheritdoc />
    public void Update()
    {
        long nowUs = _clock.Elapsed.Ticks / 10;
        long elapsedUs = nowUs - _lastUs;
        _lastUs = nowUs;

        if (_paused)
        {
            _accumulatedUs = 0;
            return;
        }

        _accumulatedUs = Math.Min(_accumulatedUs + elapsedUs, MaxAccumulatedUs);
        while (_accumulatedUs >= TickUs)
        {
            _accumulatedUs -= TickUs;
            AdvanceOneTick();
        }
    }

    /// <summary>While paused, advances the simulation by exactly one tick.</summary>
    public void StepOnce()
    {
        if (_paused) AdvanceOneTick();
    }

    /// <summary>Advances the simulation by exactly <paramref name="count"/> ticks, ignoring the clock (headless runs).</summary>
    public void StepTicks(int count)
    {
        for (int i = 0; i < count; i++) AdvanceOneTick();
    }

    /// <summary>Moves to the next fog view: player 0, player 1, omniscient, then player 0 again.</summary>
    public void CycleViewer()
    {
        _viewer = _viewer switch { 0 => 1, 1 => -1, _ => 0 };
        _current = _sim.Snapshot(_viewer);
        _previous = _current;
    }

    /// <summary>Issues one order for one of the local player's units, to run on the next tick.</summary>
    public void Order(CommandKind kind, UnitKey subject, CellCoord cell = default, ActorRef target = default) =>
        Enqueue(_sim.Tick + 1, LocalPlayer, kind, subject, cell, target);

    /// <summary>
    /// The one way orders reach the sim. Queues a command for <paramref name="execTick"/> and records it. Ignored when
    /// the subject is not a live unit of <paramref name="player"/>, when the tick has passed, or when a move-type
    /// order targets a cell off the map.
    /// </summary>
    public void Enqueue(int execTick, byte player, CommandKind kind, UnitKey subject, CellCoord cell, ActorRef target)
    {
        if (execTick <= _sim.Tick || !IsOwnedAlive(subject, player)) return;
        bool needsCell = kind is CommandKind.Move or CommandKind.AttackMove or CommandKind.Guard;
        if (needsCell && !_map.Contains(cell)) return;

        Command command = OrderBuilder.Build(execTick, player, _sequence++, kind, subject, cell, target);
        _pending.Add(command);
        _recording.RecordCommand(command);
    }

    /// <summary>True when the key names a live unit owned by <paramref name="player"/>.</summary>
    public bool IsOwnedAlive(UnitKey key, byte player)
    {
        if (key.Kind == ActorKind.Vehicle)
            return _sim.Units.Slots.IsAlive(key.Id) && _sim.Units.Player[key.Id.Index] == player;
        if (key.Kind == ActorKind.Infantry)
            return _sim.Infantry.Slots.IsAlive(key.Id) && _sim.Infantry.Player[key.Id.Index] == player;
        return false;
    }

    /// <summary>Position of a live unit in leptons, from the sim (not from the snapshot, so fog does not hide it).</summary>
    public bool TryGetPosition(UnitKey key, out LeptonCoord position)
    {
        position = default;
        if (key.Kind == ActorKind.Vehicle && _sim.Units.Slots.IsAlive(key.Id))
        {
            position = _sim.Units.Position[key.Id.Index];
            return true;
        }
        if (key.Kind == ActorKind.Infantry && _sim.Infantry.Slots.IsAlive(key.Id))
        {
            position = _sim.Infantry.Position[key.Id.Index];
            return true;
        }
        return false;
    }

    /// <summary>Every live vehicle and infantry unit of a player, in slot order.</summary>
    public List<UnitKey> AliveUnits(byte player)
    {
        var list = new List<UnitKey>();
        for (int i = 1; i < _sim.Units.Capacity; i++)
            if (_sim.Units.Slots.IsIndexAlive(i) && _sim.Units.Player[i] == player)
                list.Add(new UnitKey(ActorKind.Vehicle, _sim.Units.Slots.IdOf(i)));
        for (int i = 1; i < _sim.Infantry.Capacity; i++)
            if (_sim.Infantry.Slots.IsIndexAlive(i) && _sim.Infantry.Player[i] == player)
                list.Add(new UnitKey(ActorKind.Infantry, _sim.Infantry.Slots.IdOf(i)));
        return list;
    }

    /// <summary>Ends the session replay (<see cref="ReplayPath"/> is streamed from the start of the session, so a crash
    /// keeps it) and prints the command that checks it.</summary>
    /// <returns>False if the file could not be written.</returns>
    public bool SaveReplay()
    {
        bool ok = _recording.Finish();
        if (ok) Console.WriteLine($"verify with: {VerifyCommand}");
        return ok;
    }

    private void AdvanceOneTick()
    {
        _previous = _current;
        int ran = _sim.Tick;
        _sim.Step(_pending);
        // Commands for the tick that just ran have been applied (or were never due); drop them.
        _pending.RemoveAll(c => c.ExecTick <= ran);
        if (_sim.Tick % SimConstants.HashIntervalTicks == 0) _recording.RecordHash(_sim);
        _current = _sim.Snapshot(_viewer);
        _projectiles = CaptureProjectiles();
    }

    private List<LeptonCoord> CaptureProjectiles()
    {
        var list = new List<LeptonCoord>();
        var store = _sim.Projectiles;
        for (int i = 1; i < store.Capacity; i++)
            if (store.Slots.IsIndexAlive(i)) list.Add(store.Position[i]);
        return list;
    }
}
