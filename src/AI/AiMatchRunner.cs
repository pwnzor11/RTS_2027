// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Replay;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.AI;

/// <summary>
/// Plays one headless AI-vs-AI game: two players (M4-12: <see cref="PlayN"/> plays 2-4 sides free-for-all), each starting as <see cref="AiStartMode"/> says (default: the shared
/// skirmish economy start), AIs driven by an <see cref="AiDriver"/> before every Step. Economy start: a side wins when the
/// other is Defeated (the sim's win rule); army start: when the other has no vehicles, infantry or buildings left;
/// otherwise the game is drawn at the tick limit. Deterministic for equal inputs. No wall clock here.
/// </summary>
public static class AiMatchRunner
{
    private const int WinCheckInterval = 4;
    private static readonly string[] VehicleIds =
        ["auth_tank", "auth_tank", "auth_tank", "auth_tank", "auth_tank", "auth_jeep", "auth_jeep"];
    private const string InfantryId = "auth_rifleman";
    private const int InfantryPerPlayer = 6;
    private const string HqId = "auth_conyard";
    private const int MinHqRing = 3, MaxHqRing = 8;

    /// <summary>Plays a game. <paramref name="sideA"/> takes slot 0 when <paramref name="aInSlot0"/>, else slot 1
    /// (slot p starts at map start position p, or 1-p when <paramref name="swapStarts"/>). When <paramref name="replay"/> is given, the game is recorded to it.</summary>
    public static AiGameResult Play(MapData map, SimRules rules, ulong rulesHash, ulong seed, AiSide sideA, AiSide sideB,
        bool aInSlot0, int maxTicks, Stream? replay = null, string build = "",
        IReadOnlyList<(string Name, string Text)>? rulesFiles = null, bool swapStarts = false, AiStartMode start = AiStartMode.Economy)
    {
        if (map.StartPositions.Count < 2) throw new ArgumentException("Map needs two start positions.", nameof(map));
        // Economy start places player p at map start p; swapped starts reverse the list so slots trade corners.
        MapData playMap = start == AiStartMode.Economy && swapStarts ? SwapStarts(map) : map;
        byte slotA = aInSlot0 ? (byte)0 : (byte)1, slotB = (byte)(1 - slotA);
        var factions = new Faction[2];
        factions[slotA] = sideA.Faction; factions[slotB] = sideB.Faction;
        // Authority on both sides keeps the M2 setup exactly (no Factions list), so Authority games are unchanged.
        var setup = new SimSetup(seed, playMap, 2, rules, rulesHash)
        {
            Factions = sideA.Faction == Faction.Authority && sideB.Faction == Faction.Authority ? null : factions,
        };
        var sim = new Simulation(setup);
        ReplayWriter? recorder = replay is null ? null : new ReplayWriter(replay, sim, build, rulesFiles ?? Array.Empty<(string, string)>());
        if (start == AiStartMode.Army) for (byte p = 0; p < 2; p++) SpawnArmy(sim, rules, map, p, swapStarts ? 1 - p : p);
        else SkirmishStart.Apply(sim);

        var driver = new AiDriver(seed);
        driver.Add(slotA, Create(sideA), sideA.Profile);
        driver.Add(slotB, Create(sideB), sideB.Profile);

        var pending = new List<Command>();
        int fresh = 0;
        int winner = -1;
        var tracker = new SideTracker();
        for (int t = 0; t < maxTicks; t++)
        {
            driver.Collect(sim, pending);
            if (recorder is not null)
                for (; fresh < pending.Count; fresh++) recorder.RecordCommand(pending[fresh]);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
            fresh = pending.Count;
            if (recorder is not null && sim.Tick % SimConstants.HashIntervalTicks == 0)
                recorder.RecordHash(sim);
            if (sim.Tick % WinCheckInterval != 0) continue;
            tracker.Sample(sim);
            bool aliveA, aliveB;
            if (start == AiStartMode.Economy)
            {
                aliveA = !sim.Players.Defeated[slotA]; aliveB = !sim.Players.Defeated[slotB];
            }
            else { aliveA = HasAnything(sim, slotA); aliveB = HasAnything(sim, slotB); }
            if (aliveA && aliveB) continue;
            winner = aliveA ? 0 : aliveB ? 1 : -1;
            break;
        }

        if (recorder is not null) recorder.Finish();
        return new AiGameResult(winner, sim.Tick, ValueOf(sim, slotA), ValueOf(sim, slotB), sim.ComputeStateHash(),
            tracker.StatsOf(sim, slotA), tracker.StatsOf(sim, slotB));
    }

    /// <summary>
    /// M4-12: plays one free-for-all game of 2-4 AI sides (every player on its own team). Side i takes slot <c>(i + rotate) % N</c>, and
    /// slot p starts at map start p, so rotating 0..N-1 over a seed gives every side every start once. The map needs at least N starts.
    /// A side is out when the sim marks it Defeated; the game ends when at most one side is left, or at <paramref name="maxTicks"/>
    /// (a draw). Deterministic for equal inputs.
    /// </summary>
    public static AiMultiResult PlayN(MapData map, SimRules rules, ulong rulesHash, ulong seed, IReadOnlyList<AiSide> sides, int rotate,
        int maxTicks, Stream? replay = null, string build = "", IReadOnlyList<(string Name, string Text)>? rulesFiles = null)
    {
        int n = sides.Count;
        if (n < 2 || n > 4) throw new ArgumentException("2-4 sides.", nameof(sides));
        if (map.StartPositions.Count < n) throw new ArgumentException($"Map needs {n} start positions.", nameof(map));
        var slotOf = new int[n];
        var factions = new Faction[n];
        bool allAuthority = true;
        for (int i = 0; i < n; i++)
        {
            slotOf[i] = ((i + rotate) % n + n) % n;
            factions[slotOf[i]] = sides[i].Faction;
            allAuthority &= sides[i].Faction == Faction.Authority;
        }
        var sim = new Simulation(new SimSetup(seed, map, n, rules, rulesHash) { Factions = allAuthority ? null : factions });
        ReplayWriter? recorder = replay is null ? null : new ReplayWriter(replay, sim, build, rulesFiles ?? Array.Empty<(string, string)>());
        SkirmishStart.Apply(sim);
        var driver = new AiDriver(seed);
        for (int i = 0; i < n; i++) driver.Add((byte)slotOf[i], Create(sides[i]), sides[i].Profile);

        var pending = new List<Command>();
        int fresh = 0;
        var defeat = new int[n];
        Array.Fill(defeat, -1);
        var tracker = new SideTracker(n);
        for (int t = 0; t < maxTicks; t++)
        {
            driver.Collect(sim, pending);
            if (recorder is not null)
                for (; fresh < pending.Count; fresh++) recorder.RecordCommand(pending[fresh]);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
            fresh = pending.Count;
            if (recorder is not null && sim.Tick % SimConstants.HashIntervalTicks == 0) recorder.RecordHash(sim);
            if (sim.Tick % WinCheckInterval != 0) continue;
            tracker.Sample(sim);
            int alive = 0;
            for (int i = 0; i < n; i++)
            {
                if (defeat[i] < 0 && sim.Players.Defeated[slotOf[i]]) defeat[i] = sim.Tick;
                if (defeat[i] < 0) alive++;
            }
            if (alive <= 1) break;
        }
        if (recorder is not null) recorder.Finish();
        int winner = -1, left = 0;
        for (int i = 0; i < n; i++) if (defeat[i] < 0) { left++; winner = i; }
        if (left != 1) winner = -1;
        var value = new int[n];
        var stats = new AiSideStats[n];
        for (int i = 0; i < n; i++) { value[i] = ValueOf(sim, (byte)slotOf[i]); stats[i] = tracker.StatsOf(sim, slotOf[i]); }
        return new AiMultiResult(winner, sim.Tick, defeat, value, slotOf, stats, sim.ComputeStateHash());
    }

    private static MapData SwapStarts(MapData m)
    {
        var starts = new List<CellCoord>(m.StartPositions);
        starts.Reverse();
        return new MapData(m.Name, m.Theater, m.Width, m.Height, m.Template.ToArray(), m.Land.ToArray(), m.HeightLevel.ToArray(),
            m.Overlay.ToArray(), m.Density.ToArray(), starts);
    }

    /// <summary>Counts, per slot, the distinct buildings / mobile actors ever seen alive and the cash range (sampled). M4-12: any number
    /// of slots (actors of a slot outside the range, e.g. a neutral owner, are not counted).</summary>
    private sealed class SideTracker
    {
        private readonly HashSet<ulong>[] _structures, _units;
        private readonly int[] _min, _max;
        private readonly int _n;

        public SideTracker(int players = 2)
        {
            _n = players;
            _structures = new HashSet<ulong>[players]; _units = new HashSet<ulong>[players];
            for (int p = 0; p < players; p++) { _structures[p] = new(); _units[p] = new(); }
            _min = new int[players]; Array.Fill(_min, int.MaxValue);
            _max = new int[players];
        }

        public void Sample(Simulation sim)
        {
            for (int i = 1; i < sim.Buildings.Capacity; i++)
                if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] < _n) _structures[sim.Buildings.Player[i]].Add(Key(i, sim.Buildings.Slots.GenerationOf(i)));
            for (int i = 1; i < sim.Units.Capacity; i++)
                if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] < _n) _units[sim.Units.Player[i]].Add(Key(i, sim.Units.Slots.GenerationOf(i)));
            for (int i = 1; i < sim.Infantry.Capacity; i++)
                if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] < _n) _units[sim.Infantry.Player[i]].Add(Key(i, sim.Infantry.Slots.GenerationOf(i)) | (1UL << 40));
            for (int p = 0; p < _n; p++)
            {
                int cash = sim.Players.Cash[p] + sim.Players.Stored[p];
                if (cash < _min[p]) _min[p] = cash;
                if (cash > _max[p]) _max[p] = cash;
            }
        }

        private static ulong Key(int index, int generation) => ((ulong)(uint)index << 16) | (uint)generation;

        public AiSideStats StatsOf(Simulation sim, int p) =>
            new(_structures[p].Count, _units[p].Count, _min[p] == int.MaxValue ? 0 : _min[p], _max[p], sim.Players.Cash[p] + sim.Players.Stored[p]);
    }

    /// <summary>Scripted symmetry probe (no AI): both start forces are placed as exact point mirrors and attack-move
    /// to the enemy start at tick 0. Returns the remaining health at each START CORNER (index 0 = first start position)
    /// after <paramref name="ticks"/>. With <paramref name="swapStarts"/> the players trade corners (and so spawn order).
    /// On a point-symmetric map a fair simulation gives the same numbers whichever corner a side holds.</summary>
    public static (int HealthStart0, int HealthStart1, ulong Hash) PlayScripted(MapData map, SimRules rules, ulong rulesHash,
        ulong seed, int ticks, bool swapStarts, Action<Simulation, IReadOnlyList<(EntityId Id, ActorKind Kind)>[]>? afterStep = null)
    {
        var sim = new Simulation(new SimSetup(seed, map, 2, rules, rulesHash));
        var armies = new List<(EntityId Id, ActorKind Kind)>[2] { new(), new() };
        for (byte p = 0; p < 2; p++) SpawnArmy(sim, rules, map, p, swapStarts ? 1 - p : p, armies[p]);
        var cmds = new List<Command>();
        ushort seq = 0;
        for (byte p = 0; p < 2; p++)
        {
            CellCoord target = map.StartPositions[swapStarts ? p : 1 - p];
            foreach (var (id, kind) in armies[p])
                cmds.Add(new Command(0, p, seq++, CommandKind.AttackMove, id, EntityId.None, target, (int)kind));
        }
        for (int t = 0; t < ticks; t++) { sim.Step(cmds); afterStep?.Invoke(sim, armies); }
        int pOfStart0 = swapStarts ? 1 : 0;
        return (ValueOf(sim, (byte)pOfStart0), ValueOf(sim, (byte)(1 - pOfStart0)), sim.ComputeStateHash());
    }

    private static IAiPlayer Create(AiSide side)
    {
        if (!AiRegistry.TryCreate(side.AiId, side.Profile, out IAiPlayer? ai))
            throw new ArgumentException($"unknown AI '{side.AiId}'");
        return ai;
    }

    /// <summary>The `match` start force: vehicles in a row, infantry two cells behind, one HQ on a ring around the start.</summary>
    private static void SpawnArmy(Simulation sim, SimRules rules, MapData map, byte player, int startIndex,
        List<(EntityId Id, ActorKind Kind)>? spawned = null)
    {
        CellCoord start = map.StartPositions[startIndex];
        int dx = start.X < map.Width / 2 ? 1 : -1, dy = start.Y < map.Height / 2 ? 1 : -1;
        for (int k = 0; k < VehicleIds.Length; k++)
        {
            int type = Index(rules.Vehicles, VehicleIds[k], v => v.Id);
            var cell = new CellCoord(start.X + dx * k, start.Y);
            EntityId v = sim.SpawnVehicle(player, type, cell);
            spawned?.Add((v, ActorKind.Vehicle));
        }
        int rifle = Index(rules.Infantry, InfantryId, i => i.Id);
        for (int i = 0; i < InfantryPerPlayer; i++)
        {
            var cell = new CellCoord(start.X + dx * (i / InfantryStore.SubSlotCount), start.Y + 2 * dy);
            int sub = MirrorSub(i % InfantryStore.SubSlotCount, dx < 0, dy < 0);
            EntityId f = sim.SpawnInfantry(player, rifle, cell, sub);
            spawned?.Add((f, ActorKind.Infantry));
        }
        int hq = Index(rules.Buildings, HqId, b => b.Id);
        BuildingType hqType = rules.Buildings[hq];
        for (int ring = MinHqRing; ring <= MaxHqRing; ring++)
        {
            int x0 = dx > 0 ? start.X + ring : start.X - ring - (hqType.FootprintW - 1);
            int y0 = dy > 0 ? start.Y + ring : start.Y - ring - (hqType.FootprintH - 1);
            var topLeft = new CellCoord(x0, y0);
            if (sim.PlaceBuilding(player, hq, topLeft) != EntityId.None) break;
        }
    }

    /// <summary>Sub-slot (0 centre, 1 NW, 2 NE, 3 SW, 4 SE) after flipping the cell layout in x and/or y, so a start
    /// placed with a negative direction is the exact point mirror of the positive one.</summary>
    private static int MirrorSub(int sub, bool flipX, bool flipY)
    {
        if (sub == 0) return 0;
        int bit = sub - 1;                       // bit 0 = east, bit 1 = south
        if (flipX) bit ^= 1;
        if (flipY) bit ^= 2;
        return bit + 1;
    }

    private static int Index<T>(IReadOnlyList<T> list, string id, Func<T, string> idOf)
    {
        int i = SimRules.IndexOf(list, id, idOf);
        if (i < 0) throw new InvalidOperationException($"rules have no type '{id}' needed by the AI match");
        return i;
    }

    private static bool HasAnything(Simulation sim, byte player)
    {
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player) return true;
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player) return true;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player) return true;
        return false;
    }

    private static int ValueOf(Simulation sim, byte player)
    {
        long v = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player) v += sim.Units.Health[i];
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player) v += sim.Infantry.Health[i];
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player) v += sim.Buildings.Health[i];
        return (int)Math.Min(v, int.MaxValue);
    }
}
