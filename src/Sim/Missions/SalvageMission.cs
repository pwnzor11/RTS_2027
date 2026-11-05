// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Movement;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.Salvage;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>
/// MissionKind.Salvage handler (M6-04). The wreck being worked is the cell in <c>store.Destination</c>. Each call: if that cell holds no
/// wreck, move on to the nearest wreck within <see cref="Simulation.SalvageSearchRadius"/> cells (none: a loaded hopper returns to
/// dock, anything else guards); walk to within one cell of it; every <see cref="Simulation.SalvageDrainTicks"/> ticks drain
/// min(salvage_rate x upgrade permille / 1000, wreck value, room in the hopper). Vehicles with a hopper fill it (Cargo / CargoValue,
/// the same hopper as ash) and switch to Return when it is full or no wreck is left; the ordinary unload pays and counts it, then
/// <see cref="TryResume"/> sends them back to the nearest wreck. Salvagers without a hopper are paid directly on each drain.
/// </summary>
internal static class SalvageMission
{
    /// <summary>Runs one step of the mission; returns ticks to sleep.</summary>
    public static int Handle(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        if (store.Health[i] <= 0) return 1;
        byte player = store.Player[i];
        int rate = sim.SalvageRateOf(kind, i);
        if (rate <= 0) return Assign(sim, store, kind, i, MissionKind.Guard);
        int cap = sim.HopperCapacityOf(kind, i);
        EconomyState eco = sim.Economy;
        EntityId me = store.Slots.IdOf(i);
        if (cap > 0) eco.Touch(i, me);
        if (store.Status[i] == MissionStatus.Init) store.Status[i] = MissionStatus.Waiting;
        EconomyRules er = sim.RulesOf(player).Economy;
        int unitValue = Math.Max(1, er.CreditsAshfall);
        int room = cap > 0 ? (cap - eco.Cargo[i]) * unitValue : int.MaxValue;
        if (cap > 0 && room <= 0) return ToDock(sim, store, kind, i);

        CellCoord cur = store.Position[i].ToCell();
        CellCoord target = store.Destination[i].ToCell();
        int w = target == default ? 0 : sim.FindWreckAt(target);
        if (w == 0)
        {
            w = sim.FindNearestWreck(cur);
            if (w == 0) return cap > 0 && eco.Cargo[i] > 0 ? ToDock(sim, store, kind, i) : Assign(sim, store, kind, i, MissionKind.Guard);
            store.Destination[i] = LeptonCoord.CenterOf(new CellCoord(sim.Salvage.CellX[w], sim.Salvage.CellY[w]));
            target = store.Destination[i].ToCell();
        }

        if (Occupancy.Chebyshev(cur, target) > 1 || store.NextCellReserved[i])
        {
            int sleep = MoveMission.StepToward(sim, store, kind, i, target);
            if (sleep >= MoveMission.WaitTicks) return cap > 0 && eco.Cargo[i] > 0 ? ToDock(sim, store, kind, i) : Assign(sim, store, kind, i, MissionKind.Guard);   // unreachable
            return sleep;
        }

        SalvageState s = sim.Salvage;
        int amount = (int)Math.Max(1, (long)rate * sim.UpgradePermille(player, u => u.SalvagePermille) / 1000);
        int d = Math.Min(Math.Min(amount, s.Value[w]), room);
        if (d <= 0) return Simulation.SalvageDrainTicks;
        s.Value[w] -= d;
        if (s.Value[w] <= 0) s.Clear(w);
        if (cap > 0)
        {
            eco.Cargo[i] += (d + unitValue - 1) / unitValue;
            eco.CargoValue[i] += d;
            if (eco.Cargo[i] >= cap) return ToDock(sim, store, kind, i);
        }
        else
        {
            int space = Math.Max(0, sim.StorageCap(player) - sim.Players.Stored[player]);
            int paid = Math.Min(d, space);   // the overflow is lost, as for an unload
            sim.Players.Stored[player] += paid;
            sim.OnStatsHarvested(player, paid);
        }
        return Simulation.SalvageDrainTicks;
    }

    /// <summary>After an unload: a hopper salvager that was salvaging goes to the nearest wreck within range. False = nothing to resume.</summary>
    internal static bool TryResume(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        EconomyState eco = sim.Economy;
        if (kind != ActorKind.Vehicle || eco.HarvestState[i] != Simulation.SalvagingMarker || sim.SalvageRateOf(kind, i) <= 0) return false;
        int w = sim.FindNearestWreck(store.Position[i].ToCell());
        if (w == 0) { eco.HarvestState[i] = 0; return false; }
        store.Destination[i] = LeptonCoord.CenterOf(new CellCoord(sim.Salvage.CellX[w], sim.Salvage.CellY[w]));
        return Assign(sim, store, kind, i, MissionKind.Salvage) > 0;
    }

    private static int ToDock(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        sim.Economy.HarvestState[i] = Simulation.SalvagingMarker;
        return Assign(sim, store, kind, i, MissionKind.Return);
    }

    private static int Assign(Simulation sim, MobileStore store, ActorKind kind, int i, MissionKind mission)
    {
        sim.AssignMission(new ActorRef(kind, store.Slots.IdOf(i)), mission);
        return 1;
    }
}
