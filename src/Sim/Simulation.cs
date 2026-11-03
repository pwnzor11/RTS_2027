using System.IO.Hashing;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Serialization;
using Rts.Sim.World;

namespace Rts.Sim;

/// <summary>
/// Public face of the simulation (design/01_architecture.md §3–5). Deterministic: integer math only,
/// slot-index iteration, RNG only via the owned SimRandom.
/// </summary>
public sealed class Simulation
{
    private readonly UnitStore _units = new();
    private readonly MapState _map;
    // Canonical state-part order: [map, units]. Hash, Save and Load all iterate this array; append new parts
    // at the end only (changing the order changes every hash and the save format).
    private readonly ISimStatePart[] _parts;
    private SimRandom _rng;
    private readonly List<(int Tick, ulong Hash)> _recentHashes = new(SimConstants.HashRingSize);
    private Command[] _scratch = new Command[64];

    public Simulation(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        if (setup.Map == null) throw new ArgumentException("A map is required.", nameof(setup));
        if (setup.Rules == null || setup.Rules.Vehicles == null || setup.Rules.Vehicles.Count < 1)
            throw new ArgumentException("At least one vehicle type required.", nameof(setup));
        if (setup.PlayerCount < 1) throw new ArgumentException("At least one player required.", nameof(setup));
        Setup = setup;
        _rng = new SimRandom(setup.Seed);
        _map = new MapState(setup.Map);
        _parts = new ISimStatePart[] { _map, _units };
        MapHash = MapHasher.Compute(setup.Map);
    }

    public SimSetup Setup { get; }

    /// <summary>Identity hash of the starting map, computed once at construction.</summary>
    public ulong MapHash { get; }

    /// <summary>State parts in canonical order (map, units).</summary>
    public IReadOnlyList<ISimStatePart> StateParts => _parts;

    /// <summary>The only clock. Starts at 0; Step increments it by one.</summary>
    public int Tick { get; private set; }

    /// <summary>Hashes taken every 16 ticks, oldest first, at most 64 entries.</summary>
    public IReadOnlyList<(int Tick, ulong Hash)> RecentHashes => _recentHashes;

    /// <summary>Read access to unit state for tests and tools. Never mutate outside Step.</summary>
    public UnitStore Units => _units;

    /// <summary>Setup/scripting entry: place a vehicle outside Step. Returns its id.</summary>
    public EntityId SpawnVehicle(byte player, int vehicleTypeIndex, CellCoord cell)
    {
        var vehicles = Setup.Rules.Vehicles;
        if (vehicleTypeIndex < 0 || vehicleTypeIndex >= vehicles.Count) throw new ArgumentOutOfRangeException(nameof(vehicleTypeIndex));
        if (!InMap(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
        return _units.Add(player, vehicleTypeIndex, LeptonCoord.CenterOf(cell), vehicles[vehicleTypeIndex].MaxHealth);
    }

    /// <summary>Advance exactly one tick, applying commands whose ExecTick == Tick (others are ignored).</summary>
    public void Step(IReadOnlyList<Command> commands)
    {
        ApplyInput(commands);
        RunMissions();
        RunMovement();
        RunCombat();
        RunEconomy();
        RunProduction();
        RunAiBookkeeping();
        RunTriggers();
        RunCleanup();
        Tick++;
        if (Tick % SimConstants.HashIntervalTicks == 0)
        {
            if (_recentHashes.Count == SimConstants.HashRingSize) _recentHashes.RemoveAt(0);
            _recentHashes.Add((Tick, ComputeStateHash()));
        }
    }

    // ---- input -------------------------------------------------------------------------------

    private bool InMap(CellCoord c) => Setup.Map.Contains(c);

    private void ApplyInput(IReadOnlyList<Command> commands)
    {
        int n = 0;
        for (int i = 0; i < commands.Count; i++)
        {
            if (commands[i].ExecTick != Tick) continue;
            if (n == _scratch.Length) Array.Resize(ref _scratch, n * 2);
            // stable insertion sort by (Player, Sequence)
            Command c = commands[i];
            int j = n;
            while (j > 0 && Compare(_scratch[j - 1], c) > 0) { _scratch[j] = _scratch[j - 1]; j--; }
            _scratch[j] = c;
            n++;
        }
        for (int i = 0; i < n; i++) Apply(_scratch[i]);
    }

    private static int Compare(Command a, Command b)
    {
        int d = a.Player.CompareTo(b.Player);
        return d != 0 ? d : a.Sequence.CompareTo(b.Sequence);
    }

    private void Apply(Command c)
    {
        switch (c.Kind)
        {
            case CommandKind.Move:
                if (Owns(c) && InMap(c.Cell))
                {
                    _units.Destination[c.Subject.Index] = LeptonCoord.CenterOf(c.Cell);
                    _units.Mission[c.Subject.Index] = UnitMission.Move;
                }
                break;
            case CommandKind.Stop:
                if (Owns(c)) _units.Mission[c.Subject.Index] = UnitMission.Idle;
                break;
            case CommandKind.Debug:
                if (c.Arg == 1) _rng.Next(1000);
                break;
            default:
                break; // M1: remaining command kinds
        }
    }

    private bool Owns(Command c) => _units.Slots.IsAlive(c.Subject) && _units.Player[c.Subject.Index] == c.Player;

    // ---- systems (fixed order) ---------------------------------------------------------------

    private void RunMissions() { /* M1 */ }

    private void RunMovement()
    {
        for (int i = 1; i < _units.Capacity; i++)
        {
            if (!_units.Slots.IsIndexAlive(i) || _units.Mission[i] != UnitMission.Move) continue;
            int speed = Setup.Rules.Vehicles[_units.TypeIndex[i]].Speed;
            LeptonCoord pos = _units.Position[i], dest = _units.Destination[i];
            int dx = dest.X - pos.X, dy = dest.Y - pos.Y;
            if (dx == 0 && dy == 0) { _units.Mission[i] = UnitMission.Idle; continue; }
            if (speed <= 0) continue;
            int ax = Math.Abs(dx), ay = Math.Abs(dy);
            int major = Math.Max(ax, ay);
            _units.Facing[i] = EightWayFacing(dx, dy);
            if (major <= speed)
            {
                _units.Position[i] = dest;
                _units.Mission[i] = UnitMission.Idle;
                continue;
            }
            int sx, sy;
            if (ax >= ay) { sx = speed; sy = (int)((long)ay * speed / ax); }
            else { sy = speed; sx = (int)((long)ax * speed / ay); }
            _units.Position[i] = new LeptonCoord(pos.X + (dx < 0 ? -sx : sx), pos.Y + (dy < 0 ? -sy : sy));
        }
    }

    // TODO(M1): use Rts.Sim.Numerics.Facing
    private static byte EightWayFacing(int dx, int dy)
    {
        int ax = Math.Abs(dx), ay = Math.Abs(dy);
        bool horizontalOnly = ay * 5 < ax * 2;   // |dy/dx| < 0.4  (~tan 22.5)
        bool verticalOnly = ax * 5 < ay * 2;
        if (verticalOnly) return dy < 0 ? (byte)0 : (byte)128;
        if (horizontalOnly) return dx > 0 ? (byte)64 : (byte)192;
        if (dx > 0) return dy < 0 ? (byte)32 : (byte)96;
        return dy < 0 ? (byte)224 : (byte)160;
    }

    private void RunCombat() { /* M1 */ }
    private void RunEconomy() { /* M1 */ }
    private void RunProduction() { /* M1 */ }
    private void RunAiBookkeeping() { /* M1 */ }
    private void RunTriggers() { /* M1 */ }

    private void RunCleanup()
    {
        for (int i = 1; i < _units.Capacity; i++)
            if (_units.Slots.IsIndexAlive(i) && _units.Health[i] <= 0)
                _units.Remove(_units.Slots.IdOf(i));
    }

    // ---- hash / snapshot / save --------------------------------------------------------------

    /// <summary>64-bit xxHash64 over all sim state, including RNG and Tick.</summary>
    public ulong ComputeStateHash()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            SimWriter.WriteState(w, Tick, _rng, _parts);
        return XxHash64.HashToUInt64(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    public SimSnapshot Snapshot()
    {
        var list = new List<UnitView>(_units.Slots.Count);
        for (int i = 1; i < _units.Capacity; i++)
            if (_units.Slots.IsIndexAlive(i))
                list.Add(new UnitView(_units.Slots.IdOf(i), UnitKind.Vehicle, _units.Player[i], _units.TypeIndex[i],
                    _units.Position[i], _units.Facing[i], _units.Health[i]));
        ulong hash = _recentHashes.Count > 0 ? _recentHashes[^1].Hash : 0UL;
        return new SimSnapshot(Tick, Setup.Map.Width, Setup.Map.Height, list, Setup.Map.Land, hash);
    }

    /// <summary>Versioned binary save of the whole sim state.</summary>
    public void Save(Stream output) =>
        SimWriter.WriteSave(output, Setup.RulesHash, MapHash, Tick, _rng, _parts);

    /// <summary>Restore a game saved with Save. The setup must match (checked via RulesHash and MapHash).</summary>
    public static Simulation Load(Stream input, SimSetup setup)
    {
        var sim = new Simulation(setup);
        var state = SimReader.ReadSave(input, setup.RulesHash, sim.MapHash, sim._parts);
        sim.Tick = state.Tick;
        sim._rng = state.Rng;
        return sim;
    }
}
