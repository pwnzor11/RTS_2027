// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Tunnels;

/// <summary>
/// One unit inside a player's network (design 05 §1): Owner = the player whose network it is in; Destination = the exit
/// entrance; TicksLeft = ticks until it surfaces (restarted on a D2 re-route); Points = the tunnel points it occupies
/// (counted against <see cref="Rules.TunnelRules.NetworkCapPoints"/>).
/// </summary>
public struct TunnelTransit
{
    /// <summary>The travelling vehicle or infantry.</summary>
    public ActorRef Unit;
    /// <summary>Network owner.</summary>
    public byte Owner;
    /// <summary>Exit entrance building.</summary>
    public EntityId Destination;
    /// <summary>Ticks until the unit surfaces.</summary>
    public int TicksLeft;
    /// <summary>Tunnel points it occupies.</summary>
    public int Points;
}
