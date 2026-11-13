// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Tools;

/// <summary>
/// S23 (M13-23): one-line economy snapshot of a side for <c>ai match --trace N --game G --econ 1</c>: structures that matter for income
/// (yard, refineries, factories per lane), harvesters (count, with cargo / mission summary), the queue of every production lane
/// (item, stalled, credits spent), power, and the ore left (whole map and near the side's own yard). Read-only; tools only.
/// </summary>
internal static class EconDiag
{
    /// <summary>Radius in cells around the side's first yard that counts as "near the base".</summary>
    internal const int NearRadius = 24;

    /// <summary>Ore cells and the summed density of cells (ashfall with density above 0, or pitchblende) in the whole map and within <see cref="NearRadius"/> of a centre.</summary>
    internal static (int Cells, int Density, int NearCells, int NearDensity) OreLeft(Simulation sim, CellCoord centre)
    {
        int cells = 0, dens = 0, nc = 0, nd = 0;
        var map = sim.Map;
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var c = new CellCoord(x, y);
                OverlayKind o = map.OverlayAt(c);
                if (o != OverlayKind.Ashfall && o != OverlayKind.Pitchblende) continue;
                int d = map.DensityAt(c);
                if (d <= 0) continue;
                cells++; dens += d;
                if (Math.Abs(x - centre.X) <= NearRadius && Math.Abs(y - centre.Y) <= NearRadius) { nc++; nd += d; }
            }
        return (cells, dens, nc, nd);
    }

    /// <summary>The snapshot line for <paramref name="p"/>; <paramref name="home"/> is the side's start cell (where its yard stood at tick 1).</summary>
    internal static string Line(Simulation sim, SimRules rules, byte p, CellCoord home)
    {
        int yards = 0, refineries = 0, power = 0;
        var factories = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (int i = 1; i < sim.Buildings.Capacity; i++)
        {
            if (!sim.Buildings.Slots.IsIndexAlive(i) || sim.Buildings.Player[i] != p || sim.Buildings.Health[i] <= 0) continue;
            BuildingType bt = rules.Buildings[sim.Buildings.TypeIndex[i]];
            if (bt.Refinery) refineries++;
            if (bt.Power > 0) power++;
            if (bt.Produces is { } cat) factories[cat.ToString()] = factories.GetValueOrDefault(cat.ToString()) + 1;
            else if (bt.Prerequisites.Count == 0 && bt.Cost == 0) yards++;
        }
        int harv = 0, loaded = 0, idle = 0, noRef = 0, ret = 0, hv = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
        {
            if (!sim.Units.Slots.IsIndexAlive(i) || sim.Units.Player[i] != p || rules.Vehicles[sim.Units.TypeIndex[i]].HarvestCapacity <= 0) continue;
            harv++;
            if (sim.Economy.Cargo[i] > 0) loaded++;
            if (sim.Economy.NoRefinery[i] != 0) noRef++;
            if (sim.Units.Mission[i] == Rts.Sim.Missions.MissionKind.Return) ret++;
            else if (sim.Units.Mission[i] != Rts.Sim.Missions.MissionKind.Harvest) idle++;
            hv += sim.Units.Health[i];
        }
        var q = new StringBuilder();
        foreach (ProductionCategory cat in Enum.GetValues<ProductionCategory>())
        {
            if (cat == ProductionCategory.Upgrade) continue;
            int n = sim.Production.QueueLength(p, cat), ready = sim.Production.ReadyType(p, cat);
            if (n == 0 && ready < 0) continue;
            string head = n > 0 ? Name(rules, cat, sim.Production.QueuedType(p, cat, 0)) : "-";
            q.Append($" {cat}:{head}x{n}{(sim.Production.IsStalled(p, cat) ? "!stall" : "")}{(ready >= 0 ? " ready" : "")}");
        }
        var ore = OreLeft(sim, home);
        return $"yard {yards} ref {refineries} pwr {power}/{sim.Players.PowerOut[p]}-{sim.Players.PowerUse[p]} fact [{string.Join(",", factories.Select(f => f.Key + "=" + f.Value))}] "
            + $"harv {harv} (loaded {loaded} return {ret} other {idle} noRef {noRef}) cash {sim.Funds(p)} queue[{q.ToString().Trim()}] ore map {ore.Cells}c/{ore.Density}d near {ore.NearCells}c/{ore.NearDensity}d";
    }

    /// <summary>Where the side stands: its non-harvester units (count, bounding box) and its structure cells.</summary>
    internal static string Where(Simulation sim, SimRules rules, byte p)
    {
        int n = 0, x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
        var types = new SortedDictionary<string, int>(StringComparer.Ordinal);
        void Add(CellCoord c) { n++; x0 = Math.Min(x0, c.X); y0 = Math.Min(y0, c.Y); x1 = Math.Max(x1, c.X); y1 = Math.Max(y1, c.Y); }
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == p && rules.Vehicles[sim.Units.TypeIndex[i]].HarvestCapacity <= 0) { Add(sim.Units.Position[i].ToCell()); string id = rules.Vehicles[sim.Units.TypeIndex[i]].Id + "/" + sim.Units.Mission[i]; types[id] = types.GetValueOrDefault(id) + 1; }
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == p) { Add(sim.Infantry.Position[i].ToCell()); string id = rules.Infantry[sim.Infantry.TypeIndex[i]].Id + "/" + sim.Infantry.Mission[i]; types[id] = types.GetValueOrDefault(id) + 1; }
        var b = new StringBuilder();
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == p && sim.Buildings.Health[i] > 0)
                b.Append($" {rules.Buildings[sim.Buildings.TypeIndex[i]].Id}@{sim.Buildings.Cell[i].X},{sim.Buildings.Cell[i].Y}:{sim.Buildings.Health[i]}hp");
        return $"units {n}{(n > 0 ? $" box ({x0},{y0})-({x1},{y1}) [{string.Join(",", types.Select(t => t.Key + "=" + t.Value))}]" : "")} bldgs{b}";
    }

    private static string Name(SimRules rules, ProductionCategory cat, int type) => cat switch
    {
        ProductionCategory.Structure or ProductionCategory.Defence => type >= 0 && type < rules.Buildings.Count ? rules.Buildings[type].Id : "?",
        ProductionCategory.Vehicle => type >= 0 && type < rules.Vehicles.Count ? rules.Vehicles[type].Id : "?",
        ProductionCategory.Infantry => type >= 0 && type < rules.Infantry.Count ? rules.Infantry[type].Id : "?",
        _ => type.ToString(),
    };
}
