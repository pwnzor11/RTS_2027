// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Tunnels;

/// <summary>
/// One unit waiting at a network entrance (design 05 §1, transit A3). Entrance = the building it is drawn in at;
/// Unit = the waiting actor; Destination = the entrance it will surface at; Ticket = arrival order (lower first; from
/// <see cref="TunnelState.NextTicket"/>). An entrance's queue is the entries with that Entrance, in list order.
/// </summary>
public struct TunnelQueueEntry
{
    /// <summary>Entrance building the unit is queued at.</summary>
    public EntityId Entrance;
    /// <summary>The queued vehicle or infantry.</summary>
    public ActorRef Unit;
    /// <summary>Destination entrance building.</summary>
    public EntityId Destination;
    /// <summary>Arrival ticket (lower is drawn in first).</summary>
    public int Ticket;
}
