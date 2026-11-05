// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Movement;
using Rts.Sim.Numerics;
using Rts.Sim.Salvage;
using Rts.Sim.World;

namespace Rts.Sim;

// M6-04 (OWNER: M6-04 package; hooks placed by the lead in session 9). Wrecks and salvage (10_m6_plan.md §3.2–3.3).
// Hooks: Apply (Harvest/Return) -> TryApplySalvageCommand first; StepCore -> RunSalvage (after RunEconomy);
// RunCleanup -> SpawnWreckFor (each dying vehicle or building, before removal); MissionKind.Salvage -> Missions/SalvageMission.cs.
//
// Rules chosen here (documented for the design notes):
//  * Wreck value = cost x salvage_percent / 100, raised to wreck_min_value, then capped at wreck_max_value (the cap wins if min > max).
//    Wreck parameters (ticks, percent, floor, cap) come from Setup.Rules.Economy; the cost from the dying actor's owner rules.
//  * Decay: each tick TicksLeft--, then Value = min(Value, ValueStart x TicksLeft / wreck_ticks). Salvaging only lowers Value, so the
//    wreck is never worth more than what salvagers left, and ValueStart keeps the original line (no state is added).
//  * Hopper units (vehicles with HarvestCapacity > 0): a drained amount d fills ceil(d / credits_ashfall) hopper units and adds d to
//    CargoValue, so the existing Return/unload pays it and counts it as harvested. Salvagers without a hopper (Tinker infantry, or a
//    vehicle with HarvestCapacity 0) are paid directly on each drain (capped by storage like an unload; stats count it).
public sealed partial class Simulation
{
    /// <summary>Radius (cells, Chebyshev) in which a salvager looks for its next wreck.</summary>
    internal const int SalvageSearchRadius = 8;

    /// <summary>Ticks between drains (salvage_rate is ash per this many ticks).</summary>
    internal const int SalvageDrainTicks = 10;

    /// <summary>Marker stored in EconomyState.HarvestState while a hopper salvager is away unloading: after the unload it resumes salvaging.</summary>
    internal const byte SalvagingMarker = 255;

    /// <summary>Harvest/Return from a unit with salvage_rate &gt; 0: route to the Salvage mission. True = handled (skip the economy).</summary>
    private bool TryApplySalvageCommand(Command c)
    {
        if (c.Kind != CommandKind.Harvest && c.Kind != CommandKind.Return) return false;
        if (c.Arg != (int)ActorKind.Vehicle && c.Arg != (int)ActorKind.Infantry) return false;
        var kind = (ActorKind)c.Arg;
        MobileStore store = kind == ActorKind.Infantry ? _infantry : _units;
        if (!store.Slots.IsAlive(c.Subject) || store.Player[c.Subject.Index] != c.Player) return false;
        int i = c.Subject.Index;
        if (SalvageRateOf(kind, i) <= 0) return false;
        int cap = HopperCapacityOf(kind, i);
        if (c.Kind == CommandKind.Harvest)
        {
            bool onWreck = c.Cell != default && InMap(c.Cell) && FindWreckAt(c.Cell) != 0;
            if (!onWreck && cap > 0) return false;   // a harvester told to harvest elsewhere keeps the ordinary economy
            if (cap > 0) { _economy.Touch(i, c.Subject); _economy.HarvestState[i] = SalvagingMarker; }
            store.Destination[i] = c.Cell != default && InMap(c.Cell) ? LeptonCoord.CenterOf(c.Cell) : default;
            Queue(store, i, MissionKind.Salvage, default, default);
            return true;
        }
        // Return: only a hopper holding ash goes to the dock; afterwards it resumes salvaging when the order came during a Salvage mission.
        if (cap > 0)
        {
            _economy.Touch(i, c.Subject);
            if (_economy.Cargo[i] <= 0) return false;
            if (store.Mission[i] == MissionKind.Salvage) _economy.HarvestState[i] = SalvagingMarker;
            Queue(store, i, MissionKind.Return, default, default);
        }
        return true;
    }

    /// <summary>Per tick after the economy: wreck decay and expiry in slot order.</summary>
    private void RunSalvage()
    {
        SalvageState w = _salvage;
        int ticks = Setup.Rules.Economy.WreckTicks;
        for (int s = 1; s < w.TicksLeft.Length; s++)
        {
            if (w.TicksLeft[s] <= 0) continue;
            if (--w.TicksLeft[s] <= 0) { w.Clear(s); continue; }
            if (ticks > 0)
                w.Value[s] = (int)Math.Min(w.Value[s], (long)w.ValueStart[s] * w.TicksLeft[s] / ticks);
            if (w.Value[s] <= 0) w.Clear(s);
        }
    }

    /// <summary>RunCleanup, for a dying vehicle or building (any team) before it is removed: add a wreck when wreck_ticks &gt; 0.</summary>
    private void SpawnWreckFor(ActorKind kind, int i)
    {
        Rules.EconomyRules er = Setup.Rules.Economy;
        if (er.WreckTicks <= 0 || (kind != ActorKind.Vehicle && kind != ActorKind.Building)) return;
        int cost; CellCoord cell;
        if (kind == ActorKind.Vehicle)
        {
            cost = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].Cost;
            cell = _units.Position[i].ToCell();
        }
        else
        {
            cost = RulesOf(_buildings.Player[i]).Buildings[_buildings.TypeIndex[i]].Cost;
            cell = BuildingCentre(i).ToCell();
        }
        int value = (int)Math.Min(er.WreckMaxValue, Math.Max(er.WreckMinValue, (long)cost * er.SalvagePercent / 100));
        if (value <= 0) return;
        SalvageState w = _salvage;
        int slot = 0;
        for (int s = 1; s < w.TicksLeft.Length; s++) if (w.TicksLeft[s] <= 0) { slot = s; break; }
        if (slot == 0)
        {
            slot = 1;   // full: replace the oldest wreck (fewest ticks left, lowest slot on ties)
            for (int s = 2; s < w.TicksLeft.Length; s++) if (w.TicksLeft[s] < w.TicksLeft[slot]) slot = s;
        }
        w.CellX[slot] = cell.X; w.CellY[slot] = cell.Y;
        w.Value[slot] = value; w.ValueStart[slot] = value; w.TicksLeft[slot] = er.WreckTicks;
    }

    /// <summary>Lowest live wreck slot holding value on <paramref name="cell"/>, or 0.</summary>
    internal int FindWreckAt(CellCoord cell)
    {
        SalvageState w = _salvage;
        for (int s = 1; s < w.TicksLeft.Length; s++)
            if (w.TicksLeft[s] > 0 && w.Value[s] > 0 && w.CellX[s] == cell.X && w.CellY[s] == cell.Y) return s;
        return 0;
    }

    /// <summary>Nearest live wreck within <see cref="SalvageSearchRadius"/> cells (Chebyshev) of <paramref name="from"/>; squared distance, then slot order. 0 = none.</summary>
    internal int FindNearestWreck(CellCoord from)
    {
        SalvageState w = _salvage;
        int best = 0; long bestD = long.MaxValue;
        for (int s = 1; s < w.TicksLeft.Length; s++)
        {
            if (w.TicksLeft[s] <= 0 || w.Value[s] <= 0) continue;
            int dx = w.CellX[s] - from.X, dy = w.CellY[s] - from.Y;
            if (Math.Abs(dx) > SalvageSearchRadius || Math.Abs(dy) > SalvageSearchRadius) continue;
            long d = (long)dx * dx + (long)dy * dy;
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    /// <summary>Ash per <see cref="SalvageDrainTicks"/> ticks of the actor's type; 0 = cannot salvage.</summary>
    internal int SalvageRateOf(ActorKind kind, int i) => kind switch
    {
        ActorKind.Vehicle => RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].SalvageRate,
        ActorKind.Infantry => RulesOf(_infantry.Player[i]).Infantry?[_infantry.TypeIndex[i]].SalvageRate ?? 0,
        _ => 0,
    };

    /// <summary>Hopper size (units) of a vehicle; 0 for infantry and non-harvesters.</summary>
    internal int HopperCapacityOf(ActorKind kind, int i) =>
        kind == ActorKind.Vehicle ? RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].HarvestCapacity : 0;
}
