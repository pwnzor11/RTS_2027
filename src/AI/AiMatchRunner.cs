// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Replay;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.AI;

/// <summary>
/// Plays one headless AI-vs-AI game: two players, a fixed start force each (the `match` army), AIs driven by an
/// <see cref="AiDriver"/> before every Step. A side wins when the other has no vehicles, infantry or buildings left;
/// otherwise the game is drawn at the tick limit. Deterministic for equal inputs. No wall clock here.
/// </summary>
public static class AiMatchRunner
{
    private const int WinCheckInterval = 4;
    private static readonly string[] VehicleIds =
        ["test_tank", "test_tank", "test_tank", "test_tank", "test_jeep", "test_jeep", "test_artillery_truck"];
    private const string InfantryId = "test_rifleman";
    private const int InfantryPerPlayer = 6;
    private const string HqId = "test_hq";
    private const int MinHqRing = 3, MaxHqRing = 8;

    /// <summary>Plays a game. <paramref name="sideA"/> takes slot 0 when <paramref name="aInSlot0"/>, else slot 1
    /// (slot p starts at map start position p, or 1-p when <paramref name="swapStarts"/>). When <paramref name="replay"/> is given, the game is recorded to it.</summary>
    public static AiGameResult Play(MapData map, SimRules rules, ulong rulesHash, ulong seed, AiSide sideA, AiSide sideB,
        bool aInSlot0, int maxTicks, Stream? replay = null, string build = "",
        IReadOnlyList<(string Name, string Text)>? rulesFiles = null, bool swapStarts = false)
    {
        if (map.StartPositions.Count < 2) throw new ArgumentException("Map needs two start positions.", nameof(map));
        var setup = new SimSetup(seed, map, 2, rules, rulesHash);
        var sim = new Simulation(setup);
        ReplayWriter? recorder = replay is null ? null : new ReplayWriter(replay, sim, build, rulesFiles ?? Array.Empty<(string, string)>());
        for (byte p = 0; p < 2; p++) SpawnArmy(sim, rules, map, p, swapStarts ? 1 - p : p);

        byte slotA = aInSlot0 ? (byte)0 : (byte)1, slotB = (byte)(1 - slotA);
        var driver = new AiDriver(seed);
        driver.Add(slotA, Create(sideA), sideA.Profile);
        driver.Add(slotB, Create(sideB), sideB.Profile);

        var pending = new List<Command>();
        int fresh = 0;
        int winner = -1;
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
            bool aliveA = HasAnything(sim, slotA), aliveB = HasAnything(sim, slotB);
            if (aliveA && aliveB) continue;
            winner = aliveA ? 0 : aliveB ? 1 : -1;
            break;
        }

        if (recorder is not null) recorder.Finish();
        return new AiGameResult(winner, sim.Tick, ValueOf(sim, slotA), ValueOf(sim, slotB), sim.ComputeStateHash());
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
