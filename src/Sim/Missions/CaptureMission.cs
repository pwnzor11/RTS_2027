// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>
/// MissionKind.Capture (M3-06): infantry walks to <c>Target</c> and enters it. For an Engineer the target is a building (capture or
/// repair, the soldier is consumed); for any other soldier the target is a transport vehicle (it boards on arrival). The mission ends
/// in Guard when the target is gone or the action no longer applies.
/// </summary>
internal static class CaptureMission
{
    public static int Handle(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        if (kind != ActorKind.Infantry) { End(sim, store, kind, i); return 1; }
        EntityId target = store.Target[i];
        bool engineer = sim.RulesOf(store.Player[i]).Infantry[store.TypeIndex[i]].Engineer;
        CellCoord cur = ((InfantryStore)store).SlotCell[i];
        if (engineer)
        {
            if (!sim.Buildings.Slots.IsAlive(target) || sim.EngineerActionOn(store.Player[i], target.Index) == Simulation.EngineerAction.None)
            { End(sim, store, kind, i); return 1; }
            int b = target.Index;
            CellCoord tl = sim.Buildings.Cell[b];
            var bt = sim.RulesOf(sim.Buildings.Player[b]).Buildings[sim.Buildings.TypeIndex[b]];
            int dx = Math.Max(Math.Max(tl.X - cur.X, cur.X - (tl.X + bt.FootprintW - 1)), 0);
            int dy = Math.Max(Math.Max(tl.Y - cur.Y, cur.Y - (tl.Y + bt.FootprintH - 1)), 0);
            if (Math.Max(dx, dy) <= 1)
            {
                // M4-03: capturing an occupied building first throws the garrison out (no damage).
                if (sim.EngineerActionOn(store.Player[i], b) == Simulation.EngineerAction.Capture && sim.IsGarrisonable(b)) sim.EjectGarrison(b, false, true);
                sim.EngineerEnter(i, target);   // consumed on success; ignored otherwise
                return 1;
            }
            // Walk to the footprint cell nearest the soldier; the path service gives a partial route that ends next to the walls.
            CellCoord goal = new(Math.Clamp(cur.X, tl.X, tl.X + bt.FootprintW - 1), Math.Clamp(cur.Y, tl.Y, tl.Y + bt.FootprintH - 1));
            return MoveMission.StepToward(sim, store, kind, i, goal);
        }
        if (sim.CombatData.At(kind, i).AttackTarget == new ActorRef(ActorKind.Building, target)) return WalkIn(sim, store, kind, i, target, cur);   // M4-03
        if (!sim.Units.Slots.IsAlive(target)) { End(sim, store, kind, i); return 1; }
        CellCoord tc = sim.Units.Position[target.Index].ToCell();
        if (Math.Max(Math.Abs(tc.X - cur.X), Math.Abs(tc.Y - cur.Y)) <= 1)
        {
            if (!sim.BoardNow(i, target)) End(sim, store, kind, i);
            return 1;
        }
        return MoveMission.StepToward(sim, store, kind, i, tc);
    }

    /// <summary>M4-03: a soldier walking into a garrisonable building (marked by AttackTarget = the building); enters within
    /// enter_range_cells of the footprint. The mission ends in Guard when the building is gone or the soldier may no longer enter.</summary>
    private static int WalkIn(Simulation sim, MobileStore store, ActorKind kind, int i, EntityId target, CellCoord cur)
    {
        if (!sim.Buildings.Slots.IsAlive(target)) { End(sim, store, kind, i); return 1; }
        int b = target.Index;
        if (sim.GarrisonReach(i, b) <= sim.RulesOf(store.Player[i]).Garrison.EnterRangeCells)
        {
            if (!sim.GarrisonNow(i, target)) End(sim, store, kind, i);
            return 1;
        }
        var bt = sim.RulesOf(sim.Buildings.Player[b]).Buildings[sim.Buildings.TypeIndex[b]];
        CellCoord tl = sim.Buildings.Cell[b];
        CellCoord goal = new(Math.Clamp(cur.X, tl.X, tl.X + bt.FootprintW - 1), Math.Clamp(cur.Y, tl.Y, tl.Y + bt.FootprintH - 1));
        return MoveMission.StepToward(sim, store, kind, i, goal);
    }

    private static void End(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        ref var e = ref sim.CombatData.At(kind, i);
        if (e.AttackTarget.Kind == ActorKind.Building) e.AttackTarget = default;   // M4-03: clear the garrison-walk mark
        sim.AssignMission(new ActorRef(kind, store.Slots.IdOf(i)), MissionKind.Guard);
    }
}
