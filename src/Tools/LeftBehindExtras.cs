// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Tools;

/// <summary>Session 10 measurement aid (`ai match --extras`): per game and per side, whether a Left Behind side ever had a Hauler, its
/// salvage orders, the ash drained from wrecks by its salvagers (estimated from the wreck values, see <see cref="Observe"/>) and the
/// Second Drift orders. Read-only; it never touches the sim.</summary>
internal sealed class LeftBehindExtras(SimRules rules)
{
    private const int SampleEvery = 10;
    private readonly int _haulerType = SimRules.IndexOf(rules.Vehicles, "lb_hauler", v => v.Id);
    private readonly bool[] _hauler = new bool[2];
    private readonly long[] _drained = new long[2];
    private readonly int[] _salvage = new int[2], _storms = new int[2];
    private readonly Dictionary<int, (int Value, int Ticks)> _prev = new();
    private readonly List<WreckView> _wrecks = new();
    private readonly List<(CellCoord Cell, int Player)> _salvagers = new();

    /// <summary>Samples the game; the sim's wreck values give the drain: a wreck's value only falls faster than its linear decay
    /// (previous value / previous ticks left, per tick) when a salvager takes ash, so the excess is counted for the player owning the
    /// nearest salvaging unit. A slight under-estimate (a drained wreck decays more slowly than the rate used).</summary>
    public void Observe(Simulation sim, IAiPlayer aiA, IAiPlayer aiB, byte slotA, byte slotB)
    {
        var ais = new IAiPlayer[2];
        ais[slotA] = aiA; ais[slotB] = aiB;
        for (int p = 0; p < 2; p++)
            if (ais[p] is RecruitAi r)
            {
                _salvage[p] = r.SalvageOrders; _storms[p] = r.StormOrders;
            }
        if (sim.Tick % SampleEvery != 0) return;
        _salvagers.Clear();
        for (int i = 1; i < sim.Units.Capacity; i++)
        {
            if (!sim.Units.Slots.IsIndexAlive(i)) continue;
            int type = sim.Units.TypeIndex[i];
            byte pl = sim.Units.Player[i];
            if (pl > 1) continue;
            if (type == _haulerType && _haulerType >= 0) _hauler[pl] = true;
            if (rules.Vehicles[type].SalvageRate > 0) _salvagers.Add((sim.Units.Position[i].ToCell(), pl));
        }
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] <= 1 && rules.Infantry[sim.Infantry.TypeIndex[i]].SalvageRate > 0)
                _salvagers.Add((sim.Infantry.Position[i].ToCell(), sim.Infantry.Player[i]));
        sim.ViewFor(0).CopyWrecks(_wrecks);
        var seen = new HashSet<int>();
        foreach (WreckView w in _wrecks)
        {
            seen.Add(w.Slot);
            if (_prev.TryGetValue(w.Slot, out var pv) && pv.Ticks > w.TicksLeft && pv.Ticks > 0)
            {
                int dt = pv.Ticks - w.TicksLeft;
                int decay = (int)Math.Ceiling((double)pv.Value * dt / pv.Ticks);
                int excess = pv.Value - w.Value - decay;
                if (excess > 0)
                {
                    int owner = -1; int bd = int.MaxValue;
                    foreach (var (cell, pl) in _salvagers)
                    {
                        int d = Math.Max(Math.Abs(cell.X - w.Cell.X), Math.Abs(cell.Y - w.Cell.Y));
                        if (d < bd) { bd = d; owner = pl; }
                    }
                    if (owner >= 0 && bd <= 3) _drained[owner] += excess;
                }
            }
            _prev[w.Slot] = (w.Value, w.TicksLeft);
        }
        foreach (int slot in _prev.Keys.ToArray()) if (!seen.Contains(slot)) _prev.Remove(slot);
    }

    /// <summary>One-line summary for the side that played in <paramref name="slot"/>.</summary>
    public string Line(int slot) =>
        $"hauler {(_hauler[slot] ? "y" : "n")}, salvage orders {_salvage[slot]}, ash drained {_drained[slot]}, second drift {_storms[slot]}";

    /// <summary>Raw values for the tally: (hauler, salvage orders, ash drained, storms).</summary>
    public (bool Hauler, int Orders, long Drained, int Storms) Of(int slot) => (_hauler[slot], _salvage[slot], _drained[slot], _storms[slot]);
}
