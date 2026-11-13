// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.World;
using Rts.Sim.Rules;

namespace Rts.Tools;

/// <summary>Session 23 measurement aid (`ai match --ledger`): per side and per unit type, how many were made and how many died, summed over
/// every game of the run. It samples the unit and infantry slots every tick and compares generations, so a reused slot is still seen as a
/// death plus a birth. A unit that packs into a structure (or deploys out of one) counts as a death or a birth too. Read-only; it never
/// touches the sim.</summary>
internal sealed class UnitLedger(SimRules rules)
{
    private readonly Dictionary<(int Side, ActorKind Kind, int Type), (long Made, long Lost)> _totals = new();
    private (int Gen, int Type, byte Player)[] _veh = [], _inf = [];

    /// <summary>Starts a new game (slot memory cleared; totals kept).</summary>
    public void NewGame() { _veh = []; _inf = []; }

    /// <summary>Samples the game; <paramref name="sideOfSlot"/> maps a player slot (0/1) to side A (0) or B (1).</summary>
    public void Observe(Simulation sim, Func<byte, int> sideOfSlot)
    {
        Scan(sim.Units.Slots, sim.Units.TypeIndex, sim.Units.Player, ActorKind.Vehicle, ref _veh, sideOfSlot);
        Scan(sim.Infantry.Slots, sim.Infantry.TypeIndex, sim.Infantry.Player, ActorKind.Infantry, ref _inf, sideOfSlot);
    }

    private void Scan(SlotArray slots, int[] types, byte[] players, ActorKind kind,
        ref (int Gen, int Type, byte Player)[] seen, Func<byte, int> sideOfSlot)
    {
        if (seen.Length < slots.Capacity) Array.Resize(ref seen, slots.Capacity);
        for (int i = 1; i < slots.Capacity; i++)
        {
            bool alive = slots.IsIndexAlive(i);
            int gen = alive ? slots.GenerationOf(i) + 1 : 0;   // stored +1 so 0 means empty
            var old = seen[i];
            bool had = old.Gen != 0;
            if (had && (!alive || old.Gen != gen))
            {
                if (old.Player <= 1) Add(sideOfSlot(old.Player), kind, old.Type, 0, 1);
                seen[i] = default;
            }
            if (alive && (!had || old.Gen != gen))
            {
                byte p = players[i];
                seen[i] = (gen, types[i], p);
                if (p <= 1) Add(sideOfSlot(p), kind, types[i], 1, 0);
            }
        }
    }

    private void Add(int side, ActorKind kind, int type, long made, long lost)
    {
        var key = (side, kind, type);
        _totals.TryGetValue(key, out var v);
        _totals[key] = (v.Made + made, v.Lost + lost);
    }

    /// <summary>The ledger as text: one block per side, types sorted by credits made (count x cost), with made, lost and credits lost.</summary>
    public IEnumerable<string> Lines(int games)
    {
        for (int side = 0; side < 2; side++)
        {
            yield return $"Unit ledger, side {(side == 0 ? "A" : "B")} ({games} games): type | made | lost | lost % | cost | credits made | credits lost";
            var rows = new List<(string Id, long Made, long Lost, int Cost)>();
            foreach (var kv in _totals)
            {
                if (kv.Key.Side != side) continue;
                (string id, int cost) = kv.Key.Kind == ActorKind.Vehicle
                    ? (rules.Vehicles[kv.Key.Type].Id, rules.Vehicles[kv.Key.Type].Cost)
                    : (rules.Infantry[kv.Key.Type].Id, rules.Infantry[kv.Key.Type].Cost);
                rows.Add((id, kv.Value.Made, kv.Value.Lost, cost));
            }
            long madeC = 0, lostC = 0;
            foreach (var r in rows.OrderByDescending(r => r.Made * r.Cost).ThenBy(r => r.Id, StringComparer.Ordinal))
            {
                long pct = r.Made > 0 ? r.Lost * 100 / r.Made : 0;
                madeC += r.Made * r.Cost; lostC += r.Lost * r.Cost;
                yield return $"  {r.Id} | {r.Made} | {r.Lost} | {pct} % | {r.Cost} | {r.Made * r.Cost} | {r.Lost * r.Cost}";
            }
            yield return $"  total credits made {madeC}, lost {lostC}";
        }
    }
}
