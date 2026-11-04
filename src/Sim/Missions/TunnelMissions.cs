// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>
/// Tunnel network missions (design 05 §1, M3-02). EnterTunnel walks to the nearest own entrance that fits and joins its
/// queue; Transit is the marker mission while queued, underground and surfacing (Simulation.RunTunnelTransit drives it).
/// The logic lives in Simulation.Tunnels.cs because it touches the "tunnels" part.
/// </summary>
internal static class TunnelMissions
{
    public static int HandleEnterTunnel(Simulation sim, MobileStore store, ActorKind kind, int i) => sim.TunnelEnterStep(store, kind, i);

    public static int HandleTransit(Simulation sim, MobileStore store, ActorKind kind, int i) => sim.TunnelTransitStep(store, kind, i);
}
