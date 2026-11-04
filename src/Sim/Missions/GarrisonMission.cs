// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>MissionKind.Garrisoned (M4): a soldier inside a building acquires targets and fires out with the building's range bonus
/// and rate-of-fire factor. OWNER: M4-03. The logic lives in <c>Simulation.RunGarrisoned</c> (Simulation.Garrison.cs).</summary>
internal static class GarrisonMission
{
    public static int Handle(Simulation sim, MobileStore store, ActorKind kind, int i) =>
        kind == ActorKind.Infantry ? sim.RunGarrisoned(store, i) : MissionHandlers.GuardSleepTicks;
}
