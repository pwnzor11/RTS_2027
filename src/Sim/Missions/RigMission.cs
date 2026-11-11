// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Movement;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>
/// MissionKind.Pack handler (M6-03): a packed rig drives onto the footprint chosen by the Pack command (RigState.UnpackX/Y), stops, and
/// starts the unpack timer; Simulation.RunRigs counts it down. Ends in Guard when the order no longer applies (not a packed rig, site
/// blocked before the rig starts).
/// </summary>
internal static class RigMission
{
    /// <summary>Runs one step of the mission; returns ticks to sleep.</summary>
    public static int Handle(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        if (kind != ActorKind.Vehicle || !sim.Rigs.IsPackedRig(i)) { End(sim, store, kind, i); return 1; }
        if (sim.Rigs.UnpackTicksLeft[i] > 0) return 1;   // unpacking: stand still, RunRigs counts down
        byte owner = store.Player[i];
        VehicleType vt = sim.RulesOf(owner).Vehicles[store.TypeIndex[i]];
        int into = sim.UnpackTypeOf(i);   // D-205 (SIM-RULES-07): the type it was packed from, as the Pack command and FinishUnpack use
        if (into < 0 || into >= sim.RulesOf(owner).Buildings.Count) { End(sim, store, kind, i); return 1; }
        BuildingType bt = sim.RulesOf(owner).Buildings[into];
        var tl = new CellCoord(sim.Rigs.UnpackX[i], sim.Rigs.UnpackY[i]);
        if (!sim.UnpackSiteOk(owner, into, tl, i)) { End(sim, store, kind, i); return 1; }
        CellCoord cur = Occupancy.CurCell(kind, store, i);
        bool inside = cur.X >= tl.X && cur.X < tl.X + bt.FootprintW && cur.Y >= tl.Y && cur.Y < tl.Y + bt.FootprintH;
        if (inside && !store.NextCellReserved[i])
        {
            sim.Rigs.UnpackTicksLeft[i] = sim.ScaledPackTicks(owner, vt.PackTicks);
            return 1;
        }
        return MoveMission.StepToward(sim, store, kind, i, inside ? cur : Simulation.FootprintCentre(tl, bt));
    }

    private static void End(Simulation sim, MobileStore store, ActorKind kind, int i) =>
        sim.AssignMission(new ActorRef(kind, store.Slots.IdOf(i)), MissionKind.Guard);
}
