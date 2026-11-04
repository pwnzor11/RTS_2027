// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>MissionKind.Garrisoned (M4): a soldier inside a building acquires targets and fires out. OWNER: M4-03 (stub by M4-01:
/// idles like Guard).</summary>
internal static class GarrisonMission
{
    public static int Handle(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        // M4-03 fills
        return MissionHandlers.GuardSleepTicks;
    }
}
