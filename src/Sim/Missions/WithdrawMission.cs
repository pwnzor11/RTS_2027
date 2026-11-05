// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>
/// MissionKind.Withdraw (M6-05): a raider that has plundered runs to the nearest own building, ignoring targets, then guards. The
/// deadline tick is carried in <c>Target</c> (Index = low 16 bits, Generation = high 16 bits). With no own building the withdrawal
/// ends at once (assumed: there is no start-area data in the sim). The effects withdraw flag is set for the duration.
/// </summary>
internal static class WithdrawMission
{
    /// <summary>Runs one step of the mission; returns ticks to sleep.</summary>
    public static int Handle(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        EntityId t = store.Target[i];
        int deadline = t.Index | (t.Generation << 16);
        byte player = store.Player[i];
        CellCoord cur = store.Position[i].ToCell();
        if (sim.Tick >= deadline) { End(sim, store, kind, i); return 1; }
        // nearest own building by Chebyshev distance to its footprint; slot order, first wins ties
        int best = -1, bestD = int.MaxValue;
        CellCoord goal = cur;
        var b = sim.Buildings;
        for (int s = 1; s < b.Capacity; s++)
        {
            if (!b.Slots.IsIndexAlive(s) || b.Player[s] != player || b.Health[s] <= 0) continue;
            var bt = sim.RulesOf(player).Buildings[b.TypeIndex[s]];
            CellCoord tl = b.Cell[s];
            int dx = Math.Max(Math.Max(tl.X - cur.X, cur.X - (tl.X + bt.FootprintW - 1)), 0);
            int dy = Math.Max(Math.Max(tl.Y - cur.Y, cur.Y - (tl.Y + bt.FootprintH - 1)), 0);
            int d = Math.Max(dx, dy);
            if (d >= bestD) continue;
            best = s; bestD = d;
            goal = new CellCoord(Math.Clamp(cur.X, tl.X, tl.X + bt.FootprintW - 1), Math.Clamp(cur.Y, tl.Y, tl.Y + bt.FootprintH - 1));
        }
        if (best < 0 || bestD <= 1) { End(sim, store, kind, i); return 1; }
        return MoveMission.StepToward(sim, store, kind, i, goal);
    }

    private static void End(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        sim.SetWithdrawFlag(kind, i, false);
        sim.AssignMission(new ActorRef(kind, store.Slots.IdOf(i)), MissionKind.Guard);
    }
}
