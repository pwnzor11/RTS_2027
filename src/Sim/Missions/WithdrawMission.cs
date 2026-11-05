// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>MissionKind.Withdraw handler (OWNER: M6-05; stub placed by the lead in session 9).</summary>
internal static class WithdrawMission
{
    /// <summary>Runs one step of the mission; returns ticks to sleep.</summary>
    public static int Handle(Simulation sim, MobileStore store, ActorKind kind, int i) => MissionHandlers.GuardSleepTicks;
}
