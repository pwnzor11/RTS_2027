// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>
/// Burrow, Surface and Deploy mission handlers (M3-03, design 05 §1/§4). Burrow = dive, travel underground and surface at the
/// goal (<c>Destination</c>); Deploy = the same for a Bore, then break through into its DeploysInto building (any other deployer
/// deploys in place); Surface = come up where the unit is. The state machine lives in Simulation.BurrowMove.cs. Only vehicles
/// burrow: any other actor given these missions falls back to Guard.
/// </summary>
internal static class BurrowMissions
{
    public static int HandleBurrow(Simulation sim, MobileStore store, ActorKind kind, int i) =>
        kind == ActorKind.Vehicle ? sim.RunBurrowMission(i, false) : NotBurrower(store, i);

    public static int HandleDeploy(Simulation sim, MobileStore store, ActorKind kind, int i) =>
        kind == ActorKind.Vehicle ? sim.RunBurrowMission(i, true) : NotBurrower(store, i);

    public static int HandleSurface(Simulation sim, MobileStore store, ActorKind kind, int i) =>
        kind == ActorKind.Vehicle ? sim.RunSurfaceMission(i) : NotBurrower(store, i);

    private static int NotBurrower(MobileStore store, int i)
    {
        store.NextMission[i] = MissionKind.Guard;
        return 1;
    }
}
