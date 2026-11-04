// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>Handler for one mission kind: runs one step for the actor at <c>i</c> and returns ticks to sleep (&gt;= 1).</summary>
internal delegate int MissionHandler(Simulation sim, MobileStore store, ActorKind kind, int i);

/// <summary>Static handler table indexed by <see cref="MissionKind"/>; no reflection, fully deterministic.</summary>
internal static class MissionHandlers
{
    public const int GuardSleepTicks = 15;
    /// <summary>Facing difference (of 256) above which a vehicle stops to turn: one 16-facing step.</summary>
    public const int TurnThreshold = 256 / 16;

    private static readonly MissionHandler[] Table = BuildTable();

    private static MissionHandler[] BuildTable()
    {
        var t = new MissionHandler[Enum.GetValues<MissionKind>().Length];
        MissionHandler idle = Idle;
        for (int k = 0; k < t.Length; k++) t[k] = idle;
        t[(int)MissionKind.Move] = MoveMission.HandleMove;      // MoveMission.cs (M1-08)
        t[(int)MissionKind.Stop] = MoveMission.HandleStop;      // MoveMission.cs (M1-08)
        t[(int)MissionKind.Guard] = CombatMissions.HandleGuard;   // CombatMissions.cs (M1-09)
        t[(int)MissionKind.Attack] = CombatMissions.HandleAttack; // CombatMissions.cs (M1-09)
        t[(int)MissionKind.AttackMove] = CombatMissions.HandleAttackMove; // CombatMissions.cs (M1-09)
        t[(int)MissionKind.Harvest] = HarvestMission.HandleHarvest; // HarvestMission.cs (M2-07)
        t[(int)MissionKind.Return] = HarvestMission.HandleReturn;   // HarvestMission.cs (M2-07)
        t[(int)MissionKind.Capture] = CaptureMission.Handle;   // CaptureMission.cs (M3-06)
        return t;
    }

    public static int Handle(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        int k = (int)store.Mission[i];
        int sleep = k < Table.Length ? Table[k](sim, store, kind, i) : GuardSleepTicks;
        return sleep < 1 ? 1 : sleep;
    }

    // Guard/Sleep/None/not-yet-implemented kinds: idle (target acquisition arrives with M1-09).
    private static int Idle(Simulation sim, MobileStore store, ActorKind kind, int i) => GuardSleepTicks;

}
