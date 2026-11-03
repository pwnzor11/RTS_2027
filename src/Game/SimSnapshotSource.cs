using Rts.Sim.Rules;
using System.Diagnostics;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Game;

/// <summary>
/// Runs the real <see cref="Simulation"/> on a fixed 20 Hz accumulator and hands its snapshots to the renderer.
/// Game setup: rules from data/rules, a flat 32x32 map, two players with three tanks each near the start
/// positions. Also queues the local player's move orders and handles pause and single-step.
/// </summary>
public sealed class SimSnapshotSource : ISnapshotSource
{
    /// <summary>The player this debug view controls.</summary>
    public const byte LocalPlayer = 0;

    private const int MapCells = 32;
    private const int PlayerCount = 2;
    private const int VehiclesPerPlayer = 3;
    private const string VehicleTypeId = "test_tank";
    private const string MapFileName = "test_32.map.toml";
    private const int SpawnSpacingCells = 2;
    private const ulong Seed = 2027;
    private const long TickUs = 1_000_000 / SimConstants.TicksPerSecond; // 50 ms
    private const long MaxAccumulatedUs = 250_000;                       // avoid a burst after a stall

    private readonly Simulation _sim;
    private readonly MapData _map;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<Command> _pending = new();
    private long _lastUs;
    private long _accumulatedUs;
    private ushort _sequence;
    private bool _paused;
    private SimSnapshot _previous;
    private SimSnapshot _current;

    /// <summary>Loads the rules from <paramref name="rulesDir"/> and builds the starting game.</summary>
    /// <exception cref="Rts.Data.RulesException">The rules data is missing or invalid.</exception>
    public SimSnapshotSource(string rulesDir)
    {
        RuleSet rules = RulesLoader.Load(rulesDir);
        string mapPath = Path.Combine(rulesDir, "..", "maps", MapFileName);
        _map = File.Exists(mapPath) ? MapFile.Load(mapPath) : MapData.CreateFlat(MapCells, MapCells);
        var setup = new SimSetup(Seed, _map, PlayerCount, rules.Sim, rules.RulesHash);
        _sim = new Simulation(setup);
        int vehicleType = Math.Max(0, SimRules.IndexOf(rules.Sim.Vehicles, VehicleTypeId, v => v.Id));

        for (int player = 0; player < PlayerCount; player++)
        {
            CellCoord start = _map.StartPositions[player];
            int direction = player == 0 ? 1 : -1;
            for (int i = 0; i < VehiclesPerPlayer; i++)
            {
                var cell = new CellCoord(start.X + direction * SpawnSpacingCells * i, start.Y);
                _sim.SpawnVehicle((byte)player, vehicleType, cell);
            }
        }

        _current = _sim.Snapshot();
        _previous = _current;
    }

    /// <inheritdoc />
    public SimSnapshot Current => _current;

    /// <inheritdoc />
    public SimSnapshot Previous => _previous;

    /// <summary>
    /// 0..1 from <see cref="Previous"/> to <see cref="Current"/>. While paused it is 1, so a single step shows
    /// its result at once.
    /// </summary>
    public float Alpha => _paused ? 1f : _accumulatedUs / (float)TickUs;

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

    /// <summary>
    /// Queues a move for each of the given units that belongs to the local player. Each command executes on the
    /// tick after the current one, as the sim requires (ExecTick = Tick + 1). Ignored if the cell is off the map.
    /// </summary>
    public void EnqueueMove(IReadOnlyList<EntityId> subjects, CellCoord cell)
    {
        if (cell.X < 0 || cell.Y < 0 || cell.X >= _map.Width || cell.Y >= _map.Height) return;

        int execTick = _sim.Tick + 1;
        foreach (EntityId id in subjects)
        {
            if (!OwnedByLocal(id)) continue;
            _pending.Add(new Command(execTick, LocalPlayer, _sequence++, CommandKind.Move, id, EntityId.None, cell, 0));
        }
    }

    private bool OwnedByLocal(EntityId id)
    {
        foreach (UnitView unit in _current.Units)
        {
            if (unit.Id == id) return unit.Player == LocalPlayer;
        }
        return false;
    }

    private void AdvanceOneTick()
    {
        int ran = _sim.Tick;
        _previous = _current;
        _sim.Step(_pending);
        // Commands for the tick that just ran have been applied (or were never due); drop them.
        _pending.RemoveAll(c => c.ExecTick <= ran);
        _current = _sim.Snapshot();
    }
}
