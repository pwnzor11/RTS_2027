// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using System.Text;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Tools;

/// <summary>
/// s41 (M13-43): unit-v-unit matchups without any AI. Two equal-cost armies stand on a flat map, attack-move at each other, and the fight
/// ends when one side has no mobile unit left (or at the tick limit). The score is the share of its own army value (cost x health left) the
/// first side keeps, minus the second side's share, in permille (-1000 .. 1000). Each matchup is played over several seeds and layouts with
/// the sides swapped, so position bias cancels. Used by <c>Tools ai duels</c> and the balance fixtures (tests/Tools.Tests/DuelFixtureTests).
/// </summary>
public static class DuelMatrix
{
    /// <summary>Map size of every duel.</summary>
    public const int MapW = 72, MapH = 48;

    /// <summary>One played duel.</summary>
    public readonly record struct Result(int SpentA, int SpentB, long LeftA, long LeftB, int EndTick)
    {
        /// <summary>Kept-value share of A minus that of B, permille.</summary>
        public int Score => (int)(SpentA == 0 ? 0 : LeftA * 1000 / SpentA) - (int)(SpentB == 0 ? 0 : LeftB * 1000 / SpentB);
    }

    /// <summary>Averaged result of one matchup over <c>seeds</c> x 2 sides.</summary>
    public sealed record Summary(string A, string B, int Games, int MeanScore, int WinsA, int WinsB, int Draws, int MeanEndTick, int SpentA, int SpentB);

    /// <summary>
    /// Parses a side spec: <c>id[:weight][+id[:weight]...]</c>. The budget is split by weight and each part buys
    /// round(share / cost) units (at least one).
    /// </summary>
    public static List<(bool Vehicle, int Type, int Count)> Parse(SimRules rules, string spec, int budget)
    {
        var parts = new List<(bool, int, int Weight)>();
        int total = 0;
        foreach (string raw in spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int colon = raw.IndexOf(':');
            string id = colon < 0 ? raw : raw[..colon];
            int w = colon < 0 ? 1 : int.Parse(raw[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture);
            if (w <= 0) throw new ArgumentException($"weight must be positive in '{raw}'");
            int v = SimRules.IndexOf(rules.Vehicles, id, t => t.Id), i = SimRules.IndexOf(rules.Infantry, id, t => t.Id);
            if (v < 0 && i < 0) throw new ArgumentException($"unknown unit '{id}'");
            parts.Add((v >= 0, v >= 0 ? v : i, w));
            total += w;
        }
        if (parts.Count == 0) throw new ArgumentException("empty side spec");
        // floor counts (at least one of each), then fill: the part furthest below its share buys one more while it still fits the budget,
        // so both sides of a duel spend within one unit of the budget
        var counts = new int[parts.Count];
        long spent = 0;
        for (int k = 0; k < parts.Count; k++)
        {
            int cost = Math.Max(1, Cost(rules, parts[k].Item1, parts[k].Item2));
            counts[k] = Math.Max(1, (int)((long)budget * parts[k].Weight / total / cost));
            spent += (long)counts[k] * cost;
        }
        while (true)
        {
            int best = -1;
            long bestDeficit = long.MinValue;
            for (int k = 0; k < parts.Count; k++)
            {
                int cost = Math.Max(1, Cost(rules, parts[k].Item1, parts[k].Item2));
                if (spent + cost > budget) continue;
                long deficit = (long)budget * parts[k].Weight / total - (long)counts[k] * cost;
                if (deficit > bestDeficit) { bestDeficit = deficit; best = k; }
            }
            if (best < 0) break;
            counts[best]++;
            spent += Math.Max(1, Cost(rules, parts[best].Item1, parts[best].Item2));
        }
        var r = new List<(bool, int, int)>();
        for (int k = 0; k < parts.Count; k++) r.Add((parts[k].Item1, parts[k].Item2, counts[k]));
        return r;
    }

    private static int Cost(SimRules rules, bool veh, int type) => veh ? rules.Vehicles[type].Cost : rules.Infantry[type].Cost;

    private static int Range(SimRules rules, bool veh, int type)
    {
        int w = veh ? rules.Vehicles[type].PrimaryWeapon : rules.Infantry[type].PrimaryWeapon;
        return w < 0 ? 0 : rules.Weapons[w].Range;
    }

    private static Faction FactionOf(SimRules rules, List<(bool Vehicle, int Type, int Count)> side)
    {
        var (veh, type, _) = side[0];
        return veh ? rules.Vehicles[type].Faction : rules.Infantry[type].Faction;
    }

    /// <summary>Plays one duel. <paramref name="variant"/> picks the layout (gap and row) and the sim seed; <paramref name="swap"/> puts A on the right.</summary>
    public static Result Fight(SimRules rules, ulong rulesHash, string specA, string specB, int budget, int variant, bool swap, int ticks = 3000)
    {
        var a = Parse(rules, specA, budget);
        var b = Parse(rules, specB, budget);
        var setup = new SimSetup((ulong)(1000 + variant), MapData.CreateFlat(MapW, MapH), 2, rules, rulesHash)
        {
            AllowDebug = true,
            Factions = swap ? [FactionOf(rules, b), FactionOf(rules, a)] : [FactionOf(rules, a), FactionOf(rules, b)],
        };
        var sim = new Simulation(setup);
        // keep-alive structures far from the fight, so a side with no units left is not defeated mid-step
        int plant = SimRules.IndexOf(rules.Buildings, "auth_power", t => t.Id);
        sim.PlaceBuilding(0, plant, new CellCoord(1, 1));
        sim.PlaceBuilding(1, plant, new CellCoord(MapW - 4, MapH - 4));
        int gap = 22 + (variant % 3) * 4, cy = MapH / 2 + (variant % 5) - 2;
        int left = MapW / 2 - gap / 2, right = MapW / 2 + (gap + 1) / 2;
        byte pa = (byte)(swap ? 1 : 0), pb = (byte)(swap ? 0 : 1);
        int xa = swap ? right : left, xb = swap ? left : right;
        int spentA = Spawn(sim, rules, pa, a, xa, cy, swap ? 1 : -1);
        int spentB = Spawn(sim, rules, pb, b, xb, cy, swap ? -1 : 1);
        var cmds = new List<Command>();
        ushort seq = 0;
        Order(sim, pa, new CellCoord(xb, cy), cmds, ref seq);
        Order(sim, pb, new CellCoord(xa, cy), cmds, ref seq);
        int end = ticks;
        for (int t = 0; t < ticks; t++)
        {
            sim.Step(t == 0 ? cmds : Array.Empty<Command>());
            if (sim.Tick % 4 != 0) continue;
            if (Count(sim, pa) == 0 || Count(sim, pb) == 0) { end = sim.Tick; break; }
        }
        return new Result(spentA, spentB, Value(sim, rules, pa), Value(sim, rules, pb), end);
    }

    /// <summary>Plays a matchup over <paramref name="seeds"/> layouts x both sides.</summary>
    public static Summary Play(SimRules rules, ulong rulesHash, string specA, string specB, int budget, int seeds, int ticks = 3000)
    {
        long score = 0, endSum = 0;
        int wa = 0, wb = 0, d = 0, spentA = 0, spentB = 0;
        for (int s = 0; s < seeds; s++)
            for (int side = 0; side < 2; side++)
            {
                Result r = Fight(rules, rulesHash, specA, specB, budget, s, side == 1, ticks);
                score += r.Score; endSum += r.EndTick; spentA = r.SpentA; spentB = r.SpentB;
                if (r.LeftA > 0 && r.LeftB == 0) wa++;
                else if (r.LeftB > 0 && r.LeftA == 0) wb++;
                else d++;
            }
        int n = seeds * 2;
        return new Summary(specA, specB, n, (int)(score / n), wa, wb, d, (int)(endSum / n), spentA, spentB);
    }

    private static int Spawn(Simulation sim, SimRules rules, byte player, List<(bool Vehicle, int Type, int Count)> side, int cx, int cy, int back)
    {
        // short range in front, long range behind; vehicles one per cell, infantry five per cell, six rows deep
        var order = side.OrderBy(p => Range(rules, p.Vehicle, p.Type)).ThenBy(p => p.Vehicle ? 0 : 1).ToList();
        int col = 0, row = 0, spent = 0;
        foreach (var (veh, type, count) in order)
        {
            int sub = 0;
            for (int k = 0; k < count; k++)
            {
                var cell = new CellCoord(cx + back * col, cy - 3 + row);
                if (veh) sim.SpawnVehicle(player, type, cell);
                else sim.SpawnInfantry(player, type, cell, sub);
                spent += Cost(rules, veh, type);
                bool nextCell = veh || ++sub == InfantryStore.SubSlotCount;
                if (!nextCell) continue;
                sub = 0;
                if (++row == 6) { row = 0; col++; }
            }
            if (sub > 0 && ++row == 6) { row = 0; col++; }
        }
        return spent;
    }

    private static void Order(Simulation sim, byte player, CellCoord target, List<Command> cmds, ref ushort seq)
    {
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player)
                cmds.Add(new Command(0, player, seq++, CommandKind.AttackMove, sim.Units.Slots.IdOf(i), EntityId.None, target, (int)ActorKind.Vehicle));
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player)
                cmds.Add(new Command(0, player, seq++, CommandKind.AttackMove, sim.Infantry.Slots.IdOf(i), EntityId.None, target, (int)ActorKind.Infantry));
    }

    private static int Count(Simulation sim, byte player)
    {
        int n = 0;
        for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player) n++;
        for (int i = 1; i < sim.Infantry.Capacity; i++) if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player) n++;
        return n;
    }

    private static long Value(Simulation sim, SimRules rules, byte player)
    {
        long v = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player)
            {
                VehicleType t = rules.Vehicles[sim.Units.TypeIndex[i]];
                v += (long)t.Cost * Math.Min(sim.Units.Health[i], t.MaxHealth) / Math.Max(1, t.MaxHealth);
            }
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player)
            {
                InfantryType t = rules.Infantry[sim.Infantry.TypeIndex[i]];
                v += (long)t.Cost * Math.Min(sim.Infantry.Health[i], t.MaxHealth) / Math.Max(1, t.MaxHealth);
            }
        return v;
    }

    /// <summary>The default roster: every ground or hover unit of the four factions with a weapon (no air, ships or harvesters).</summary>
    public static List<string> DefaultRoster(SimRules rules)
    {
        var r = new List<string>();
        foreach (InfantryType t in rules.Infantry)
            if (t.PrimaryWeapon >= 0 && t.Cost > 0 && t.Faction != Faction.Neutral) r.Add(t.Id);
        foreach (VehicleType t in rules.Vehicles)
            if (t.PrimaryWeapon >= 0 && t.Cost > 0 && t.Faction != Faction.Neutral && t.HarvestCapacity == 0
                && t.Locomotion is not (Locomotion.Air or Locomotion.Naval)
                && rules.Weapons[t.PrimaryWeapon].Targets != WeaponTargets.Air) r.Add(t.Id);
        return r;
    }

    /// <summary>Faction of a spec's first unit.</summary>
    public static Faction FactionOfSpec(SimRules rules, string spec) => FactionOf(rules, Parse(rules, spec, 1000));

    /// <summary>One CSV line per summary (header first).</summary>
    public static string Csv(IEnumerable<Summary> rows)
    {
        var sb = new StringBuilder("a,b,games,score,wins_a,wins_b,draws,end_tick,spent_a,spent_b\n");
        foreach (Summary s in rows)
            sb.Append(CultureInfo.InvariantCulture, $"{s.A},{s.B},{s.Games},{s.MeanScore},{s.WinsA},{s.WinsB},{s.Draws},{s.MeanEndTick},{s.SpentA},{s.SpentB}\n");
        return sb.ToString();
    }
}
