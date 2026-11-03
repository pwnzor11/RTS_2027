using System.IO.Hashing;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
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
    private readonly InfantryStore _infantry = new();
    private readonly MapState _map;
    // Canonical state-part order: [map, units, infantry]. Hash, Save and Load all iterate this array; append new parts
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
        _parts = new ISimStatePart[] { _map, _units, _infantry };
        MapHash = MapHasher.Compute(setup.Map);
    }

    public SimSetup Setup { get; }

    /// <summary>Identity hash of the starting map, computed once at construction.</summary>
    public ulong MapHash { get; }

    /// <summary>State parts in canonical order (map, units, infantry).</summary>
    public IReadOnlyList<ISimStatePart> StateParts => _parts;

    /// <summary>The only clock. Starts at 0; Step increments it by one.</summary>
    public int Tick { get; private set; }

    /// <summary>Hashes taken every 16 ticks, oldest first, at most 64 entries.</summary>
    public IReadOnlyList<(int Tick, ulong Hash)> RecentHashes => _recentHashes;

    /// <summary>Read access to unit state for tests and tools. Never mutate outside Step.</summary>
    public UnitStore Units => _units;

    /// <summary>Read access to infantry state for tests and tools. Never mutate outside Step.</summary>
    public InfantryStore Infantry => _infantry;

    /// <summary>Setup/scripting entry: place a vehicle outside Step. Returns its id.</summary>
    public EntityId SpawnVehicle(byte player, int vehicleTypeIndex, CellCoord cell)
    {
        var vehicles = Setup.Rules.Vehicles;
        if (vehicleTypeIndex < 0 || vehicleTypeIndex >= vehicles.Count) throw new ArgumentOutOfRangeException(nameof(vehicleTypeIndex));
        if (!InMap(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
        return _units.Add(player, vehicleTypeIndex, LeptonCoord.CenterOf(cell), vehicles[vehicleTypeIndex].MaxHealth);
    }

    /// <summary>Setup/scripting entry: place an infantry soldier at a sub-slot (0 = centre, 1..4 = corners) of a cell.</summary>
    public EntityId SpawnInfantry(byte player, int infantryTypeIndex, CellCoord cell, int subSlot = 0)
    {
        var infantry = Setup.Rules.Infantry;
        if (infantry == null || infantryTypeIndex < 0 || infantryTypeIndex >= infantry.Count) throw new ArgumentOutOfRangeException(nameof(infantryTypeIndex));
        if (!InMap(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
        if (subSlot < 0 || subSlot >= InfantryStore.SubSlotCount) throw new ArgumentOutOfRangeException(nameof(subSlot));
        return _infantry.Add(player, infantryTypeIndex, cell, subSlot, infantry[infantryTypeIndex].MaxHealth);
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
                if (OwnsSubject(c, out var store) && InMap(c.Cell))
                    Queue(store!, c.Subject.Index, MissionKind.Move, c.Cell, default);
                break;
            case CommandKind.Stop:
                if (OwnsSubject(c, out store)) Queue(store!, c.Subject.Index, MissionKind.Stop, default, default);
                break;
            case CommandKind.Debug:
                if (c.Arg == 1) _rng.Next(1000);
                break;
            default:
                break; // M1: remaining command kinds
        }
    }

    /// <summary>Subject's store is chosen by Command.Arg (an ActorKind); true if the subject is alive and owned by the player.</summary>
    private bool OwnsSubject(Command c, out MobileStore? store)
    {
        store = c.Arg >= 0 && c.Arg <= byte.MaxValue ? StoreOf((ActorKind)c.Arg) : null;
        return store != null && store.Slots.IsAlive(c.Subject) && store.Player[c.Subject.Index] == c.Player;
    }

    private MobileStore? StoreOf(ActorKind kind) => kind switch
    {
        ActorKind.Vehicle => _units,
        ActorKind.Infantry => _infantry,
        _ => null,
    };

    /// <summary>True if the actor exists in its store (generation checked).</summary>
    public bool TryResolve(ActorRef actor) => StoreOf(actor.Kind)?.Slots.IsAlive(actor.Id) ?? false;

    /// <summary>Queue a mission for the actor; it commences at the start of the next RunMissions. False if the actor is gone or not a mission-running kind.</summary>
    public bool AssignMission(ActorRef actor, MissionKind kind, CellCoord cell = default, EntityId target = default)
    {
        var store = StoreOf(actor.Kind);
        if (store == null || !store.Slots.IsAlive(actor.Id)) return false;
        Queue(store, actor.Id.Index, kind, cell, target);
        return true;
    }

    private static void Queue(MobileStore store, int i, MissionKind kind, CellCoord cell, EntityId target)
    {
        store.NextMission[i] = kind; store.NextCell[i] = cell; store.NextTarget[i] = target;
    }

    // ---- systems (fixed order) ---------------------------------------------------------------

    private void RunMissions()
    {
        // Safe point: promote queued missions, then run handlers. Handlers only queue new missions, so a
        // mission assigned during a handler takes effect on the next tick.
        RunMissions(_units, ActorKind.Vehicle);
        RunMissions(_infantry, ActorKind.Infantry);
    }

    private void RunMissions(MobileStore store, ActorKind kind)
    {
        for (int i = 1; i < store.Capacity; i++)
        {
            if (!store.Slots.IsIndexAlive(i)) continue;
            if (store.NextMission[i] != MissionKind.None) Commence(store, kind, i);
        }
        for (int i = 1; i < store.Capacity; i++)
        {
            if (!store.Slots.IsIndexAlive(i) || store.Mission[i] == MissionKind.None || store.WakeTick[i] > Tick) continue;
            store.WakeTick[i] = Tick + MissionHandlers.Handle(this, store, kind, i);
        }
    }

    private void Commence(MobileStore store, ActorKind kind, int i)
    {
        MissionKind m = store.NextMission[i];
        store.Mission[i] = m;
        store.Status[i] = MissionStatus.Init;
        store.WakeTick[i] = 0;
        store.Target[i] = store.NextTarget[i];
        if (m == MissionKind.Move)
            store.Destination[i] = kind == ActorKind.Infantry
                ? InfantryStore.SlotPosition(store.NextCell[i], _infantry.SubSlot[i])
                : LeptonCoord.CenterOf(store.NextCell[i]);
        store.NextMission[i] = MissionKind.None;
        store.NextCell[i] = default;
        store.NextTarget[i] = default;
    }

    // Movement is performed by the Move mission handler (D-042); this system is reserved for M1-05+ (blocking, paths).
    private void RunMovement() { }

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
        for (int i = 1; i < _infantry.Capacity; i++)
            if (_infantry.Slots.IsIndexAlive(i) && _infantry.Health[i] <= 0)
                _infantry.Remove(_infantry.Slots.IdOf(i));
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

    /// <summary>Snapshot for the omniscient debug view. Snapshot(viewer) arrives with fog in M1-07.</summary>
    public SimSnapshot Snapshot()
    {
        var list = new List<UnitView>(_units.Slots.Count + _infantry.Slots.Count);
        for (int i = 1; i < _units.Capacity; i++)
            if (_units.Slots.IsIndexAlive(i))
                list.Add(new UnitView(_units.Slots.IdOf(i), UnitKind.Vehicle, _units.Player[i], _units.TypeIndex[i],
                    _units.Position[i], _units.Facing[i], _units.Health[i]));
        for (int i = 1; i < _infantry.Capacity; i++)
            if (_infantry.Slots.IsIndexAlive(i))
                list.Add(new UnitView(_infantry.Slots.IdOf(i), UnitKind.Infantry, _infantry.Player[i], _infantry.TypeIndex[i],
                    _infantry.Position[i], _infantry.Facing[i], _infantry.Health[i]));
        ulong hash = _recentHashes.Count > 0 ? _recentHashes[^1].Hash : 0UL;
        return new SimSnapshot(Tick, Setup.Map.Width, Setup.Map.Height, list, Setup.Map.Land, hash,
            Array.Empty<BuildingView>(), -1, Array.Empty<byte>());
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
