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
        ["auth_tank", "auth_tank", "auth_tank", "auth_tank", "auth_tank", "auth_jeep", "auth_jeep"];
    private const string InfantryId = "auth_rifleman";
    private const int InfantryPerPlayer = 6;
    private const string HqId = "auth_conyard";
    private const int MinHqRing = 3, MaxHqRing = 8;

    /// <summary>Plays a game. <paramref name="sideA"/> takes slot 0 when <paramref name="aInSlot0"/>, else slot 1
    /// (slot p starts at map start position p). When <paramref name="replay"/> is given, the game is recorded to it.</summary>
    public static AiGameResult Play(MapData map, SimRules rules, ulong rulesHash, ulong seed, AiSide sideA, AiSide sideB,
        bool aInSlot0, int maxTicks, Stream? replay = null, string build = "",
        IReadOnlyList<(string Name, string Text)>? rulesFiles = null)
    {
        if (map.StartPositions.Count < 2) throw new ArgumentException("Map needs two start positions.", nameof(map));
        var setup = new SimSetup(seed, map, 2, rules, rulesHash);
        var sim = new Simulation(setup);
        ReplayWriter? recorder = replay is null ? null : new ReplayWriter(replay, sim, build, rulesFiles ?? Array.Empty<(string, string)>());
        for (byte p = 0; p < 2; p++) SpawnArmy(sim, rules, map, p);

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

    private static IAiPlayer Create(AiSide side)
    {
        if (!AiRegistry.TryCreate(side.AiId, side.Profile, out IAiPlayer? ai))
            throw new ArgumentException($"unknown AI '{side.AiId}'");
        return ai;
    }

    /// <summary>The `match` start force: vehicles in a row, infantry two cells behind, one HQ on a ring around the start.</summary>
    private static void SpawnArmy(Simulation sim, SimRules rules, MapData map, byte player)
    {
        CellCoord start = map.StartPositions[player];
        int dx = start.X < map.Width / 2 ? 1 : -1, dy = start.Y < map.Height / 2 ? 1 : -1;
        for (int k = 0; k < VehicleIds.Length; k++)
        {
            int type = Index(rules.Vehicles, VehicleIds[k], v => v.Id);
            var cell = new CellCoord(start.X + dx * k, start.Y);
            sim.SpawnVehicle(player, type, cell);
        }
        int rifle = Index(rules.Infantry, InfantryId, i => i.Id);
        for (int i = 0; i < InfantryPerPlayer; i++)
        {
            var cell = new CellCoord(start.X + dx * (i / InfantryStore.SubSlotCount), start.Y + 2 * dy);
            int sub = i % InfantryStore.SubSlotCount;
            sim.SpawnInfantry(player, rifle, cell, sub);
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
