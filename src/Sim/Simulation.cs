// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.IO.Hashing;
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Pathing;
using Rts.Sim.Production;
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
    private readonly UnitStore _units;
    private readonly InfantryStore _infantry;
    private readonly BuildingStore _buildings;
    private readonly PlayerStore _players;
    private readonly EconomyState _economy;
    private readonly ProductionState _production;
    private readonly List<SetupEntry> _setupLog = new();
    private bool _inStep;
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
        if (setup.PlayerCount > byte.MaxValue) throw new ArgumentException("At most 255 players.", nameof(setup));
        if (setup.Teams != null && setup.Teams.Count != setup.PlayerCount)
            throw new ArgumentException("Teams must have one entry per player.", nameof(setup));
        ArgumentNullException.ThrowIfNull(setup.Limits);
        Setup = setup;
        _rng = new SimRandom(setup.Seed);
        _units = new UnitStore(setup.Limits.Vehicles);
        _infantry = new InfantryStore(setup.Limits.Infantry);
        _buildings = new BuildingStore(setup.Limits.Buildings);
        _players = new PlayerStore(setup);
        for (int p = 0; p < setup.PlayerCount; p++) if (!_players.IsNeutral((byte)p)) _players.Cash[p] = setup.Rules.Economy.StartingCash;   // M2-07
        _economy = new EconomyState(setup);
        _production = new ProductionState(setup);
        _map = new MapState(setup.Map);
        _paths = new PathService(_map, setup.Rules.Terrain);
        _vision = new VisionState(setup.Map.Width, setup.Map.Height, setup.PlayerCount,
            Math.Max(setup.Limits.Vehicles, setup.Limits.Infantry));
        // Parts after vision come from the partial files in canonical order: combat (M1-09) first.
        var extra = CombatParts();
        // M1.5: "players" is appended after the combat parts (append-only order).
        _parts = new ISimStatePart[9 + extra.Length];
        _parts[0] = _map; _parts[1] = _units; _parts[2] = _infantry; _parts[3] = _buildings; _parts[4] = _paths; _parts[5] = _vision;
        for (int e = 0; e < extra.Length; e++) _parts[6 + e] = extra[e];
        _parts[6 + extra.Length] = _players;
        _parts[7 + extra.Length] = _economy;
        _parts[8 + extra.Length] = _production;
        MapHash = MapHasher.Compute(setup.Map);
    }

    public SimSetup Setup { get; }

    /// <summary>Per-player state (team, cash, power, defeat) — the "players" part (M1.5, D-054).</summary>
    public PlayerStore Players => _players;

    /// <summary>Economy state — the "economy" part (M2).</summary>
    public EconomyState Economy => _economy;

    /// <summary>Production state — the "production" part (M2).</summary>
    public ProductionState Production => _production;

    /// <summary>Out-of-Step spawns and placements in the order they happened (M1.5). Replays read this.</summary>
    public IReadOnlyList<SetupEntry> SetupLog => _setupLog;

    /// <summary>True while Step is running.</summary>
    public bool InStep => _inStep;

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

    /// <summary>Commands whose ExecTick was already past when they reached Step (dropped). Diagnostic only; not hashed.</summary>
    public long LateCommandCount { get; private set; }

    /// <summary>Setup mutators work inside Step (scripts) or before the first Step; afterwards they must be commands.</summary>
    private void RequireSetupPhase()
    {
        if (!_inStep && Tick > 0)
            throw new InvalidOperationException("setup calls are only allowed before the first Step; use a command");
    }

    /// <summary>Setup/scripting entry: place a vehicle outside Step. Returns its id, or <see cref="EntityId.None"/> when the store is full.</summary>
    public EntityId SpawnVehicle(byte player, int vehicleTypeIndex, CellCoord cell)
    {
        RequireSetupPhase();
        var vehicles = Setup.Rules.Vehicles;
        if (vehicleTypeIndex < 0 || vehicleTypeIndex >= vehicles.Count) throw new ArgumentOutOfRangeException(nameof(vehicleTypeIndex));
        if (!InMap(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
        EntityId id = _units.Add(player, vehicleTypeIndex, LeptonCoord.CenterOf(cell), vehicles[vehicleTypeIndex].MaxHealth);
        if (id.IsNone) return id;
        _units.Facing[id.Index] = CentreFacing(cell);
        OnActorSpawned(ActorKind.Vehicle, id.Index);
        if (!_inStep) _setupLog.Add(new SetupEntry(ActorKind.Vehicle, player, vehicleTypeIndex, cell, 0));
        return id;
    }

    /// <summary>M2-01: setup-placed actors face the map centre, so a point-mirrored setup is mirrored in facing too
    /// (a fixed facing 0 left the north-west force turned away from its enemy and the south-east one facing it).</summary>
    private byte CentreFacing(CellCoord cell) =>
        Rts.Sim.Numerics.Facing.FromDelta(Setup.Map.Width * 128 - (cell.X * 256 + 128), Setup.Map.Height * 128 - (cell.Y * 256 + 128));

    /// <summary>Setup/scripting entry: place an infantry soldier at a sub-slot (0 = centre, 1..4 = corners) of a cell.</summary>
    public EntityId SpawnInfantry(byte player, int infantryTypeIndex, CellCoord cell, int subSlot = 0)
    {
        RequireSetupPhase();
        var infantry = Setup.Rules.Infantry;
        if (infantry == null || infantryTypeIndex < 0 || infantryTypeIndex >= infantry.Count) throw new ArgumentOutOfRangeException(nameof(infantryTypeIndex));
        if (!InMap(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
        if (subSlot < 0 || subSlot >= InfantryStore.SubSlotCount) throw new ArgumentOutOfRangeException(nameof(subSlot));
        EntityId id = _infantry.Add(player, infantryTypeIndex, cell, subSlot, infantry[infantryTypeIndex].MaxHealth);
        if (id.IsNone) return id;
        _infantry.Facing[id.Index] = CentreFacing(cell);
        OnActorSpawned(ActorKind.Infantry, id.Index);
        if (!_inStep) _setupLog.Add(new SetupEntry(ActorKind.Infantry, player, infantryTypeIndex, cell, subSlot));
        return id;
    }

    /// <summary>Advance exactly one tick, applying commands whose ExecTick == Tick (others are ignored).</summary>
    public void Step(IReadOnlyList<Command> commands)
    {
        _inStep = true;
        try { StepCore(commands); }
        finally { _inStep = false; }
    }

    private void StepCore(IReadOnlyList<Command> commands)
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
            if (commands[i].ExecTick < Tick) { LateCommandCount++; continue; }
            if (commands[i].ExecTick != Tick) continue;
            if (n == _scratch.Length) Array.Resize(ref _scratch, n * 2);
            // insertion sort; Compare is total, so the result does not depend on input order
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
        if (d != 0) return d;
        if ((d = a.Sequence.CompareTo(b.Sequence)) != 0) return d;
        if ((d = a.Kind.CompareTo(b.Kind)) != 0) return d;
        if ((d = a.Subject.Index.CompareTo(b.Subject.Index)) != 0) return d;
        if ((d = a.Subject.Generation.CompareTo(b.Subject.Generation)) != 0) return d;
        if ((d = a.Target.Index.CompareTo(b.Target.Index)) != 0) return d;
        if ((d = a.Target.Generation.CompareTo(b.Target.Generation)) != 0) return d;
        if ((d = a.Cell.Y.CompareTo(b.Cell.Y)) != 0) return d;
        if ((d = a.Cell.X.CompareTo(b.Cell.X)) != 0) return d;
        if ((d = a.Arg.CompareTo(b.Arg)) != 0) return d;
        return a.Arg2.CompareTo(b.Arg2);
    }

    private void Apply(Command c)
    {
        if (c.Player >= Setup.PlayerCount) return;
        switch (c.Kind)
        {
            case CommandKind.Move:
            case CommandKind.Stop:
            case CommandKind.Scatter:
                if (c.Kind == CommandKind.Move && !InMap(c.Cell)) break;
                ApplyMovementCommand(c);   // Simulation.Movement.cs (M1-08)
                break;
            case CommandKind.Attack:
            case CommandKind.AttackMove:
            case CommandKind.Guard:
                if (c.Kind != CommandKind.Attack && !InMap(c.Cell)) break;
                ApplyCombatCommand(c);     // Simulation.Combat.cs (M1-09)
                break;
            case CommandKind.Place:
                if (!_players.IsNeutral(c.Player) && c.Arg >= 0 && c.Arg < BuildingTypeCount)
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
            case CommandKind.Harvest:
            case CommandKind.Return:
                ApplyEconomyCommand(c);    // Simulation.Economy.cs (M2-07)
                break;
            case CommandKind.Debug:
                if (Setup.AllowDebug) ApplyDebug(c);
                break;
            default:
                break; // M1: remaining command kinds
        }
    }

    private void ApplyDebug(Command c)
    {
        switch ((DebugOp)c.Arg)
        {
            case DebugOp.BurnRng:
                _rng.Next(1000);
                break;
            case DebugOp.SpawnVehicle:
                // No occupancy check exists for spawns; spawns anyway (as the setup entry does).
                if (c.Arg2 >= 0 && c.Arg2 < Setup.Rules.Vehicles.Count && InMap(c.Cell))
                    SpawnVehicle(c.Player, c.Arg2, c.Cell);
                break;
            case DebugOp.SpawnInfantry:
                if (Setup.Rules.Infantry != null && c.Arg2 >= 0 && c.Arg2 < Setup.Rules.Infantry.Count && InMap(c.Cell))
                    SpawnInfantry(c.Player, c.Arg2, c.Cell, 0);
                break;
            case DebugOp.SetCash:
                _players.Cash[c.Player] = Math.Max(0, c.Arg2);
                break;
            case DebugOp.Kill:
                switch ((ActorKind)c.Arg2)
                {
                    case ActorKind.Vehicle when _units.Slots.IsAlive(c.Subject): _units.Health[c.Subject.Index] = 0; break;
                    case ActorKind.Infantry when _infantry.Slots.IsAlive(c.Subject): _infantry.Health[c.Subject.Index] = 0; break;
                    case ActorKind.Building when _buildings.Slots.IsAlive(c.Subject): _buildings.Health[c.Subject.Index] = 0; break;
                }
                break;
        }
    }

    /// <summary>Subject's store is chosen by Command.Arg (an ActorKind); true if the subject is alive and owned by the player.</summary>
    private bool OwnsSubject(Command c, out MobileStore? store)
    {
        store = c.Arg >= 0 && c.Arg <= byte.MaxValue ? StoreOf((ActorKind)c.Arg) : null;
        return store != null && store.Slots.IsAlive(c.Subject) && store.Player[c.Subject.Index] == c.Player;
    }

    /// <summary>The mission-running store of a kind (vehicle, infantry); null for buildings and other kinds. See Simulation.Actors.cs.</summary>
    internal MobileStore? StoreOf(ActorKind kind) => kind switch
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
        RequireSetupPhase();
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
        RequireSetupPhase();
        if (CanPlace(player, buildingType, topLeft) != PlacementResult.Ok) return EntityId.None;
        BuildingType bt = Setup.Rules.Buildings[buildingType];
        EntityId id = _buildings.Add(player, buildingType, topLeft, bt.MaxHealth);
        if (id.IsNone) return id;
        for (int y = 0; y < bt.FootprintH; y++)
            for (int x = 0; x < bt.FootprintW; x++)
                _map.SetBuilding(new CellCoord(topLeft.X + x, topLeft.Y + y), id);
        if (!_inStep) _setupLog.Add(new SetupEntry(ActorKind.Building, player, buildingType, topLeft, 0));
        return id;
    }

    private void RemoveBuilding(EntityId id)
    {
        int i = id.Index;
        OnCombatActorRemoving(ActorKind.Building, i); // clear attackers' targets (combat, M1-09)
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

    // RunEconomy: Simulation.Economy.cs (M2-07)
    private void RunProduction() { /* M1 */ }
    private void RunAiBookkeeping() { /* M1 */ }
    private void RunTriggers() { /* M1 */ }

    private void RunCleanup()
    {
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            int cap = slots.Capacity;
            for (int i = 1; i < cap; i++)
            {
                if (!slots.IsIndexAlive(i) || HealthAt(kind, i) > 0) continue;
                if (kind == ActorKind.Building) { RemoveBuilding(slots.IdOf(i)); continue; }
                OnActorRemoving(kind, i);
                StoreOf(kind)!.Remove(slots.IdOf(i));
            }
        }
        UpdateVision();
    }

    // ---- vision (M1-07, wired by the lead) ----------------------------------------------------

    /// <summary>Reveal/conceal for every actor slot in index order, then refresh non-allied building ghosts (team vision, D-054).
    /// VisionState.Reveal is incremental, so unchanged actors cost one comparison.</summary>
    private void UpdateVision()
    {
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            int cap = slots.Capacity;
            for (int i = 1; i < cap; i++)
                if (slots.IsIndexAlive(i)) _vision.Reveal(PlayerAt(kind, i), kind, i, VisionCellOf(kind, i), SightOf(kind, i));
                else _vision.Conceal(kind, i);
        }

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
                if (!_buildings.Slots.IsIndexAlive(i) || IsFriendly(viewer, _buildings.Player[i])) continue;
                BuildingType bt = BuildingTypeAt(i);
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
                if (_map.Data.Contains(c) && IsVisibleTo(viewer, c)) return true;
            }
        return false;
    }

    // ---- hash / snapshot / save --------------------------------------------------------------

    /// <summary>64-bit xxHash64 over all sim state, including RNG and Tick. One streaming pass, no allocation after
    /// warm-up (see Simulation.Hashing.cs).</summary>
    public ulong ComputeStateHash() => HashPass(null);

    /// <summary>Snapshot for the omniscient debug view. Snapshot(viewer) arrives with fog in M1-07.</summary>
    public SimSnapshot Snapshot() => Snapshot(-1);

    /// <summary>Snapshot as seen by <paramref name="viewer"/> (-1 = omniscient). With a viewer, enemy units outside
    /// current vision are omitted, and enemy buildings outside it appear as last-seen ghosts (if ever seen).</summary>
    public SimSnapshot Snapshot(int viewer)
    {
        if (viewer >= Setup.PlayerCount) throw new ArgumentOutOfRangeException(nameof(viewer));
        var list = new List<UnitView>(_units.Slots.Count + _infantry.Slots.Count);
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            MobileStore? store = StoreOf(kind);
            if (store == null) continue;   // buildings are listed below
            for (int i = 1; i < store.Capacity; i++)
                if (store.Slots.IsIndexAlive(i) && SeenBy(viewer, store.Player[i], store.Position[i]))
                    list.Add(new UnitView(store.Slots.IdOf(i), (UnitKind)k, store.Player[i], store.TypeIndex[i],
                        store.Position[i], store.Facing[i], store.Health[i]));
        }
        ulong hash = _recentHashes.Count > 0 ? _recentHashes[^1].Hash : 0UL;
        var blist = new List<BuildingView>(_buildings.Slots.Count);
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i))
            {
                BuildingType bt = BuildingTypeAt(i);
                if (viewer >= 0 && !IsFriendly((byte)viewer, _buildings.Player[i])
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
        ReadOnlySpan<byte> team = TeamMembers((byte)viewer);
        if (team.Length == 1) _vision.FillVision((byte)viewer, vision);
        else _vision.FillVisionAny(team, vision);   // allies share vision and exploration
        return new SimSnapshot(Tick, Setup.Map.Width, Setup.Map.Height, list, Setup.Map.Land, hash,
            blist, viewer, vision);
    }

    private bool SeenBy(int viewer, byte owner, LeptonCoord pos) =>
        viewer < 0 || owner == viewer || IsVisibleTo((byte)viewer, pos.ToCell());

    /// <summary>Versioned binary save of the whole sim state. The header carries the setup (build, seed, players, teams,
    /// limits, AllowDebug, rules and map identity) and the embedded rules files given here, so the save can be
    /// loaded without the caller supplying rules (see the Load overload taking a rules delegate).</summary>
    public void Save(Stream output, string build = "", IReadOnlyList<(string Name, string Text)>? rulesFiles = null) =>
        SimWriter.WriteSave(output, SetupHeader.From(this, build, rulesFiles ?? Array.Empty<(string, string)>()), Tick, _rng, _parts);

    /// <summary>Restore a game saved with Save. The setup must match the save header (rules hash, map hash, player
    /// count, teams, limits, AllowDebug).</summary>
    public static Simulation Load(Stream input, SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        using var r = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);
        SetupHeader header = ReadSaveHeader(r);
        var sim = new Simulation(setup);
        header.CheckMatches(setup, sim.MapHash, "Save", checkSeed: false);
        return sim.FinishLoad(r);
    }

    /// <summary>
    /// Restore a game from a save alone: the setup comes from the save header and the rules from its embedded rules
    /// files, turned into rules by <paramref name="rulesFromBlob"/> (supplied by the data layer, so the sim does not
    /// reference it). The map is supplied by the caller and checked against the saved map hash.
    /// </summary>
    public static Simulation Load(Stream input,
        Func<IReadOnlyList<(string Name, string Text)>, (SimRules Rules, ulong Hash)> rulesFromBlob, MapData map)
    {
        ArgumentNullException.ThrowIfNull(rulesFromBlob);
        ArgumentNullException.ThrowIfNull(map);
        using var r = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);
        SetupHeader header = ReadSaveHeader(r);
        (SimRules rules, ulong hash) = rulesFromBlob(header.RulesFiles);
        return new Simulation(header.ToSetup(map, rules, hash)).FinishLoad(r);
    }

    private static SetupHeader ReadSaveHeader(BinaryReader r)
    {
        try { return SimReader.ReadHeader(r); }
        catch (EndOfStreamException) { throw new InvalidDataException("Truncated save."); }
    }

    private Simulation FinishLoad(BinaryReader r)
    {
        SimReader.State state;
        try { state = SimReader.ReadState(r, _parts); }
        catch (EndOfStreamException) { throw new InvalidDataException("Truncated save."); }
        Tick = state.Tick;
        _rng = state.Rng;
        return this;
    }
}
