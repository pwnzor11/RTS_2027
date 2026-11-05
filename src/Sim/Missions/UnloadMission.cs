// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>MissionKind.Unload (M7-04): a landing craft or Skyhook puts its passengers ashore; the logic lives in Simulation.Landing.cs.</summary>
internal static class UnloadMission
{
    /// <summary>Runs one step of the mission; returns ticks to sleep.</summary>
    public static int Handle(Simulation sim, MobileStore store, ActorKind kind, int i) =>
        kind == ActorKind.Vehicle ? sim.RunUnloadMission(i) : MissionHandlers.GuardSleepTicks;
}
