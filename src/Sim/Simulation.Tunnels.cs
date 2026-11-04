// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// Tunnel network transit (design 05 §1, D-074). OWNER: M3-02. Fill these bodies; do not change their signatures without telling
// the lead. State: Tunnels (TunnelState.Queue / Transit / NextTicket, Vehicle/InfantryLayer). Constants: Setup.Rules.Tunnels.
public sealed partial class Simulation
{
    /// <summary>Per tick (inside RunTunnels, before RunBurrowing): draw queued units in at each entrance's TicksPerPoint
    /// within the network cap, count transit down, surface arrivals at the destination exit.</summary>
    private void RunTunnelTransit()
    {
        // M3-02 fills this
    }

    /// <summary>Enter with Target = own entrance building (the destination). <paramref name="unit"/> is an own live mobile actor.
    /// Refuse when the destination does not fit the unit's TunnelSize.</summary>
    private void ApplyEnterTunnel(Command c, ActorRef unit)
    {
        // M3-02 fills this
    }

    /// <summary>An own entrance building (slot <paramref name="buildingIndex"/>) is about to be removed: drop its queue and
    /// re-route transit units headed for it (D2), or lose them.</summary>
    private void OnEntranceRemoving(int buildingIndex)
    {
        // M3-02 fills this
    }
}
