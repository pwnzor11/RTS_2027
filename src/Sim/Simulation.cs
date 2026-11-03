using System.IO.Hashing;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using Rts.Sim.Serialization;
using Rts.Sim.Vision;
using Rts.Sim.World;

namespace Rts.Sim;

/// <summary>
/// Public face of the simulation (design/01_architecture.md §3–5). Deterministic: integer math only,
/// slot-index iteration, RNG only via the owned SimRandom.
/// </summary>
public sealed partial class Simulation
{
    private readonly UnitStore _units = new();
    private readonly InfantryStore _infantry = new();
    private readonly BuildingStore _buildings = new();
    private readonly MapState _map;
    private readonly PathService _paths;
    private readonly VisionState _vision;
    // Canonical state-part order: [map, units, infantry, buildings, paths, vision]. Hash, Save and Load all iterate this array; append new parts
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
        _paths = new PathService(_map, setup.Rules.Terrain);
        _vision = new VisionState(setup.Map.Width, setup.Map.Height, setup.PlayerCount);
        // Parts after vision come from the partial files in canonical order: combat (M1-09) first.
        var extra = CombatParts();
        _parts = new ISimStatePart[6 + extra.Length];
        _parts[0] = _map; _parts[1] = _units; _parts[2] = _infantry; _parts[3] = _buildings; _parts[4] = _paths; _parts[5] = _vision;
        for (int e = 0; e < extra.Length; e++) _parts[6 + e] = extra[e];
        MapHash = MapHasher.Compute(setup.Map);
    }

    public SimSetup Setup { get; }

    /// <summary>Identity hash of the starting map, computed once at construction.</summary>
    public ulong MapHash { get; }

    /// <summary>Deterministic path service (M1-05). Movement requests and polls paths; Process runs once per tick.</summary>
    public PathService Paths => _paths;

    /// <summary>Per-player vision (M1-07), updated at the end of every tick. Read-only use outside Step.</summary>
    public VisionState Vision => _vision;

    /// <summary>State parts in canonical order (map, units, infantry, buildings, paths, vision).</summary>
    public IReadOnlyList<ISimStatePart> StateParts => _parts;

    /// <summary>The only clock. Starts at 0; Step increments it by one.</summary>
    public int Tick { get; private set; }

    /// <summary>Hashes taken every 16 ticks, oldest first, at most 64 entries.</summary>
    public IReadOnlyList<(int Tick, ulong Hash)> RecentHashes => _recentHashes;

    /// <summary>Read access to unit state for tests and tools. Never mutate outside Step.</summary>
    public UnitStore Units => _units;

    /// <summary>Read access to infantry state for tests and tools. Never mutate outside Step.</summary>
    public InfantryStore Infantry => _infantry;

    /// <summary>Read access to building state for tests and tools. Never mutate outside Step.</summary>
    public BuildingStore Buildings => _buildings;

    /// <summary>Read access to the runtime map (land, overlay, occupancy, Version) for tests and tools. Never mutate outside Step.</summary>
    public MapState Map => _map;

    /// <summary>Setup/scripting entry: place a vehicle outside Step. Returns its id.</summary>
    public EntityId SpawnVehicle(byte player, int vehicleTypeIndex, CellCoord cell)
    {
        var vehicles = Setup.Rules.Vehicles;
        if (vehicleTypeIndex < 0 || vehicleTypeIndex >= vehicles.Count) throw new ArgumentOutOfRangeException(nameof(vehicleTypeIndex));
        if (!InMap(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
        EntityId id = _units.Add(player, vehicleTypeIndex, LeptonCoord.CenterOf(cell), vehicles[vehicleTypeIndex].MaxHealth);
        OnActorSpawned(ActorKind.Vehicle, id.Index);
        return id;
    }

    /// <summary>Setup/scripting entry: place an infantry soldier at a sub-slot (0 = centre, 1..4 = corners) of a cell.</summary>
    public EntityId SpawnInfantry(byte player, int infantryTypeIndex, CellCoord cell, int subSlot = 0)
    {
        var infantry = Setup.Rules.Infantry;
        if (infantry == null || infantryTypeIndex < 0 || infantryTypeIndex >= infantry.Count) throw new ArgumentOutOfRangeException(nameof(infantryTypeIndex));
        if (!InMap(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
        if (subSlot < 0 || subSlot >= InfantryStore.SubSlotCount) throw new ArgumentOutOfRangeException(nameof(subSlot));
        EntityId id = _infantry.Add(player, infantryTypeIndex, cell, subSlot, infantry[infantryTypeIndex].MaxHealth);
        OnActorSpawned(ActorKind.Infantry, id.Index);
        return id;
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
            case CommandKind.Stop:
            case CommandKind.Scatter:
                ApplyMovementCommand(c);   // Simulation.Movement.cs (M1-08)
                break;
            case CommandKind.Attack:
            case CommandKind.AttackMove:
            case CommandKind.Guard:
                ApplyCombatCommand(c);     // Simulation.Combat.cs (M1-09)
                break;
            case CommandKind.Place:
                if (c.Player < Setup.PlayerCount && c.Arg >= 0 && c.Arg < BuildingTypeCount)
                    PlaceBuilding(c.Player, c.Arg, c.Cell);
                break;
            case CommandKind.Sell:
                if (c.Arg == (int)ActorKind.Building && _buildings.Slots.IsAlive(c.Subject)
                    && _buildings.Player[c.Subject.Index] == c.Player)
                {
                    // M2: refund
                    RemoveBuilding(c.Subject);
                }
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
    public bool TryResolve(ActorRef actor) =>
        actor.Kind == ActorKind.Building ? _buildings.Slots.IsAlive(actor.Id) : StoreOf(actor.Kind)?.Slots.IsAlive(actor.Id) ?? false;

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

    // ---- buildings ---------------------------------------------------------------------------

    private int BuildingTypeCount => Setup.Rules.Buildings?.Count ?? 0;

    // Scratch for the one-cell-gap flood fill. Not sim state: fully rewritten on every call.
    private bool[]? _open;
    private int[]? _fillStack;
    private int[]? _fillMark;
    private bool[]? _ring;
    private int _openVersion = -1;
    private int _fillStamp;

    /// <summary>
    /// Whether the player may put a building of this type with its top-left at the given cell. Checks the footprint
    /// is inside the map, buildable and free of units, then the one-cell gap rule (design 02 section 3): the footprint
    /// must not split any region of Tracked-passable, building-free cells into more pieces than before. No adjacency,
    /// prerequisite or cost checks in M1.
    /// </summary>
    public PlacementResult CanPlace(byte player, int buildingType, CellCoord topLeft)
    {
        if (buildingType < 0 || buildingType >= BuildingTypeCount) throw new ArgumentOutOfRangeException(nameof(buildingType));
        BuildingType bt = Setup.Rules.Buildings[buildingType];
        int w = bt.FootprintW, h = bt.FootprintH;
        int x0 = topLeft.X, y0 = topLeft.Y;
        if (x0 < 0 || y0 < 0 || x0 + w > Setup.Map.Width || y0 + h > Setup.Map.Height) return PlacementResult.OutOfMap;
        TerrainTable table = Setup.Rules.Terrain;
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                if (!_map.IsBuildable(new CellCoord(x, y), table)) return PlacementResult.Blocked;
        // Map occupancy is not maintained for units yet, so also check unit positions directly.
        for (int i = 1; i < _units.Capacity; i++)
            if (_units.Slots.IsIndexAlive(i) && InRect(_units.Position[i].ToCell(), x0, y0, w, h)) return PlacementResult.Blocked;
        for (int i = 1; i < _infantry.Capacity; i++)
            if (_infantry.Slots.IsIndexAlive(i) && InRect(_infantry.Position[i].ToCell(), x0, y0, w, h)) return PlacementResult.Blocked;
        return SealsRegion(x0, y0, w, h) ? PlacementResult.SealsRegion : PlacementResult.Ok;
    }

    private static bool InRect(CellCoord c, int x0, int y0, int w, int h) =>
        c.X >= x0 && c.X < x0 + w && c.Y >= y0 && c.Y < y0 + h;

    private bool SealsRegion(int x0, int y0, int w, int h)
    {
        int mw = Setup.Map.Width, mh = Setup.Map.Height, n = mw * mh;
        _open ??= new bool[n];
        _fillStack ??= new int[n];
        _fillMark ??= new int[n];
        _ring ??= new bool[n];
        if (_openVersion != _map.Version)
        {
            TerrainTable table = Setup.Rules.Terrain;
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++) _open[y * mw + x] = IsOpen(x, y, table);
            _openVersion = _map.Version;
        }

        // Fast path: open cells 4-adjacent to the footprint (the "ring"). If, with the footprint closed, a search from
        // one ring cell reaches every other ring cell, nothing was split and no full count is needed.
        int ringCount = 0, first = -1;
        for (int y = y0 - 1; y <= y0 + h; y++)
            for (int x = x0 - 1; x <= x0 + w; x++)
            {
                bool inX = x >= x0 && x < x0 + w, inY = y >= y0 && y < y0 + h;
                if (inX == inY) continue; // inside the footprint, or a diagonal corner (not 4-adjacent)
                if (x < 0 || y < 0 || x >= mw || y >= mh) continue;
                int idx = y * mw + x;
                if (!_open[idx]) continue;
                _ring[idx] = true; ringCount++;
                if (first < 0) first = idx;
            }
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                _open[y * mw + x] = false;
        bool connected = ringCount <= 1 || ReachesAllRing(first, ringCount, mw, n);
        for (int y = y0 - 1; y <= y0 + h; y++)
            for (int x = x0 - 1; x <= x0 + w; x++)
                if (x >= 0 && y >= 0 && x < mw && y < mh) _ring[y * mw + x] = false;
        if (connected) { RestoreFootprint(x0, y0, w, h); return false; }

        // Slow path: compare component counts before and after.
        int after = CountRegions(mw, mh);
        RestoreFootprint(x0, y0, w, h);
        int before = CountRegions(mw, mh);
        return after > before;
    }

    // _open caches "Tracked-passable and building-free" per cell; it stays valid while MapState.Version is unchanged.
    private bool IsOpen(int x, int y, TerrainTable table)
    {
        var c = new CellCoord(x, y);
        return _map.BuildingAt(c).IsNone && _map.IsPassableTerrain(c, Locomotion.Tracked, table);
    }

    private void RestoreFootprint(int x0, int y0, int w, int h)
    {
        TerrainTable table = Setup.Rules.Terrain;
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                _open![y * Setup.Map.Width + x] = IsOpen(x, y, table);
    }

    /// <summary>Search from start over open cells; true once every flagged ring cell has been reached.</summary>
    private bool ReachesAllRing(int start, int ringCount, int mw, int n)
    {
        bool[] open = _open!; bool[] ring = _ring!; int[] stack = _fillStack!; int[] mark = _fillMark!;
        int stamp = ++_fillStamp, sp = 0, found = 1;
        stack[sp++] = start; mark[start] = stamp;
        while (sp > 0)
        {
            int cur = stack[--sp];
            int cx = cur % mw;
            for (int d = 0; d < 4; d++)
            {
                int j = d switch { 0 => cx > 0 ? cur - 1 : -1, 1 => cx < mw - 1 ? cur + 1 : -1, 2 => cur >= mw ? cur - mw : -1, _ => cur < n - mw ? cur + mw : -1 };
                if (j < 0 || !open[j] || mark[j] == stamp) continue;
                mark[j] = stamp; stack[sp++] = j;
                if (ring[j] && ++found == ringCount) return true;
            }
        }
        return found == ringCount;
    }

    /// <summary>Number of 4-connected components of open cells in _open.</summary>
    private int CountRegions(int mw, int mh)
    {
        bool[] open = _open!; int[] stack = _fillStack!; int[] mark = _fillMark!;
        int stamp = ++_fillStamp, regions = 0, n = mw * mh;
        for (int start = 0; start < n; start++)
        {
            if (!open[start] || mark[start] == stamp) continue;
            regions++;
            int sp = 0;
            stack[sp++] = start; mark[start] = stamp;
            while (sp > 0)
            {
                int cur = stack[--sp];
                int cx = cur % mw;
                if (cx > 0 && open[cur - 1] && mark[cur - 1] != stamp) { mark[cur - 1] = stamp; stack[sp++] = cur - 1; }
                if (cx < mw - 1 && open[cur + 1] && mark[cur + 1] != stamp) { mark[cur + 1] = stamp; stack[sp++] = cur + 1; }
                if (cur >= mw && open[cur - mw] && mark[cur - mw] != stamp) { mark[cur - mw] = stamp; stack[sp++] = cur - mw; }
                if (cur < n - mw && open[cur + mw] && mark[cur + mw] != stamp) { mark[cur + mw] = stamp; stack[sp++] = cur + mw; }
            }
        }
        return regions;
    }

    /// <summary>Setup/scripting entry: place a building if <see cref="CanPlace"/> says Ok; otherwise returns <see cref="EntityId.None"/>.</summary>
    public EntityId PlaceBuilding(byte player, int buildingType, CellCoord topLeft)
    {
        if (CanPlace(player, buildingType, topLeft) != PlacementResult.Ok) return EntityId.None;
        BuildingType bt = Setup.Rules.Buildings[buildingType];
        EntityId id = _buildings.Add(player, buildingType, topLeft, bt.MaxHealth);
        for (int y = 0; y < bt.FootprintH; y++)
            for (int x = 0; x < bt.FootprintW; x++)
                _map.SetBuilding(new CellCoord(topLeft.X + x, topLeft.Y + y), id);
        return id;
    }

    private void RemoveBuilding(EntityId id)
    {
        int i = id.Index;
        BuildingType bt = Setup.Rules.Buildings[_buildings.TypeIndex[i]];
        CellCoord tl = _buildings.Cell[i];
        for (int y = 0; y < bt.FootprintH; y++)
            for (int x = 0; x < bt.FootprintW; x++)
                _map.SetBuilding(new CellCoord(tl.X + x, tl.Y + y), EntityId.None);
        _buildings.Remove(id);
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

    private void RunEconomy() { /* M1 */ }
    private void RunProduction() { /* M1 */ }
    private void RunAiBookkeeping() { /* M1 */ }
    private void RunTriggers() { /* M1 */ }

    private void RunCleanup()
    {
        for (int i = 1; i < _units.Capacity; i++)
            if (_units.Slots.IsIndexAlive(i) && _units.Health[i] <= 0)
            {
                OnActorRemoving(ActorKind.Vehicle, i);
                _units.Remove(_units.Slots.IdOf(i));
            }
        for (int i = 1; i < _infantry.Capacity; i++)
            if (_infantry.Slots.IsIndexAlive(i) && _infantry.Health[i] <= 0)
            {
                OnActorRemoving(ActorKind.Infantry, i);
                _infantry.Remove(_infantry.Slots.IdOf(i));
            }
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i) && _buildings.Health[i] <= 0)
                RemoveBuilding(_buildings.Slots.IdOf(i));
        UpdateVision();
    }

    // ---- vision (M1-07, wired by the lead) ----------------------------------------------------

    /// <summary>Reveal/conceal for every actor slot in index order, then refresh enemy-building ghosts.
    /// VisionState.Reveal is incremental, so unchanged actors cost one comparison.</summary>
    private void UpdateVision()
    {
        SimRules r = Setup.Rules;
        for (int i = 1; i < _units.Capacity; i++)
            if (_units.Slots.IsIndexAlive(i))
                _vision.Reveal(_units.Player[i], ActorKind.Vehicle, i, _units.Position[i].ToCell(), r.Vehicles[_units.TypeIndex[i]].Sight);
            else _vision.Conceal(ActorKind.Vehicle, i);
        for (int i = 1; i < _infantry.Capacity; i++)
            if (_infantry.Slots.IsIndexAlive(i))
                _vision.Reveal(_infantry.Player[i], ActorKind.Infantry, i, _infantry.Position[i].ToCell(), r.Infantry[_infantry.TypeIndex[i]].Sight);
            else _vision.Conceal(ActorKind.Infantry, i);
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i))
            {
                BuildingType bt = r.Buildings[_buildings.TypeIndex[i]];
                CellCoord c = _buildings.Cell[i];
                _vision.Reveal(_buildings.Player[i], ActorKind.Building, i,
                    new CellCoord(c.X + bt.FootprintW / 2, c.Y + bt.FootprintH / 2), bt.Sight);
            }
            else _vision.Conceal(ActorKind.Building, i);

        for (int p = 0; p < Setup.PlayerCount; p++)
        {
            byte viewer = (byte)p;
            // Forget ghosts whose footprint is visible again and no longer matches a live building.
            var ghosts = _vision.GhostsFor(viewer);
            for (int g = ghosts.Count - 1; g >= 0; g--)
            {
                (int index, BuildingGhost ghost) = ghosts[g];
                if (!FootprintVisible(viewer, ghost.Cell, ghost.FootprintW, ghost.FootprintH)) continue;
                bool same = _buildings.Slots.IsIndexAlive(index) && _buildings.Cell[index] == ghost.Cell
                    && _buildings.TypeIndex[index] == ghost.TypeIndex;
                if (!same) _vision.ForgetBuilding(viewer, index);
            }
            // Record what the viewer sees of enemy buildings now.
            for (int i = 1; i < _buildings.Capacity; i++)
            {
                if (!_buildings.Slots.IsIndexAlive(i) || _buildings.Player[i] == viewer) continue;
                BuildingType bt = r.Buildings[_buildings.TypeIndex[i]];
                if (FootprintVisible(viewer, _buildings.Cell[i], bt.FootprintW, bt.FootprintH))
                    _vision.RecordBuildingSeen(viewer, i, new BuildingGhost(_buildings.Player[i], _buildings.TypeIndex[i],
                        _buildings.Cell[i], bt.FootprintW, bt.FootprintH, _buildings.Health[i]));
            }
        }
    }

    private bool FootprintVisible(byte viewer, CellCoord topLeft, int w, int h)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = new CellCoord(topLeft.X + x, topLeft.Y + y);
                if (_map.Data.Contains(c) && _vision.IsVisible(viewer, c)) return true;
            }
        return false;
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
    public SimSnapshot Snapshot() => Snapshot(-1);

    /// <summary>Snapshot as seen by <paramref name="viewer"/> (-1 = omniscient). With a viewer, enemy units outside
    /// current vision are omitted, and enemy buildings outside it appear as last-seen ghosts (if ever seen).</summary>
    public SimSnapshot Snapshot(int viewer)
    {
        if (viewer >= Setup.PlayerCount) throw new ArgumentOutOfRangeException(nameof(viewer));
        var list = new List<UnitView>(_units.Slots.Count + _infantry.Slots.Count);
        for (int i = 1; i < _units.Capacity; i++)
            if (_units.Slots.IsIndexAlive(i) && SeenBy(viewer, _units.Player[i], _units.Position[i]))
                list.Add(new UnitView(_units.Slots.IdOf(i), UnitKind.Vehicle, _units.Player[i], _units.TypeIndex[i],
                    _units.Position[i], _units.Facing[i], _units.Health[i]));
        for (int i = 1; i < _infantry.Capacity; i++)
            if (_infantry.Slots.IsIndexAlive(i) && SeenBy(viewer, _infantry.Player[i], _infantry.Position[i]))
                list.Add(new UnitView(_infantry.Slots.IdOf(i), UnitKind.Infantry, _infantry.Player[i], _infantry.TypeIndex[i],
                    _infantry.Position[i], _infantry.Facing[i], _infantry.Health[i]));
        ulong hash = _recentHashes.Count > 0 ? _recentHashes[^1].Hash : 0UL;
        var blist = new List<BuildingView>(_buildings.Slots.Count);
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i))
            {
                BuildingType bt = Setup.Rules.Buildings[_buildings.TypeIndex[i]];
                if (viewer >= 0 && _buildings.Player[i] != viewer
                    && !FootprintVisible((byte)viewer, _buildings.Cell[i], bt.FootprintW, bt.FootprintH)) continue;
                blist.Add(new BuildingView(_buildings.Slots.IdOf(i), _buildings.Player[i], _buildings.TypeIndex[i],
                    _buildings.Cell[i], bt.FootprintW, bt.FootprintH, _buildings.Health[i], false));
            }
        if (viewer < 0)
            return new SimSnapshot(Tick, Setup.Map.Width, Setup.Map.Height, list, Setup.Map.Land, hash,
                blist, -1, Array.Empty<byte>());
        foreach ((int index, BuildingGhost g) in _vision.GhostsFor((byte)viewer))
        {
            if (FootprintVisible((byte)viewer, g.Cell, g.FootprintW, g.FootprintH)) continue; // live view already shown
            blist.Add(new BuildingView(new EntityId((ushort)index, 0), g.Player, g.TypeIndex, g.Cell, g.FootprintW,
                g.FootprintH, g.Health, true));
        }
        var vision = new byte[Setup.Map.Width * Setup.Map.Height];
        _vision.FillVision((byte)viewer, vision);
        return new SimSnapshot(Tick, Setup.Map.Width, Setup.Map.Height, list, Setup.Map.Land, hash,
            blist, viewer, vision);
    }

    private bool SeenBy(int viewer, byte owner, LeptonCoord pos) =>
        viewer < 0 || owner == viewer || _vision.IsVisible((byte)viewer, pos.ToCell());

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
