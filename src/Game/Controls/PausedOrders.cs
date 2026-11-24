// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>One order waiting while the game is paused: who, what, where (or at whom).</summary>
/// <param name="Subject">The unit.</param>
/// <param name="SubjectKind">Vehicle or infantry.</param>
/// <param name="Cell">The destination cell (ignored for an attack on a unit; see <paramref name="Target"/>).</param>
/// <param name="Attack">True for an attack-move, an attack or an attack-ground (drawn red), false for a move or guard (green).</param>
/// <param name="Target">The unit attacked, or <see cref="EntityId.None"/> when the order is aimed at a cell.</param>
/// <param name="TargetKind">Kind of <paramref name="Target"/> (meaningful when it is set).</param>
public readonly record struct PausedOrder(EntityId Subject, ActorKind SubjectKind, CellCoord Cell, bool Attack, EntityId Target, ActorKind TargetKind);

/// <summary>
/// A43-QOL-08 (tactical pause): while single-player is paused the host already holds every issued command until the first tick after
/// unpause. This reads that queue (<c>SimHost.Pending</c>) so the screen can show the player what is waiting. Read only: nothing here
/// issues or changes a command.
/// </summary>
public static class PausedOrders
{
    /// <summary>
    /// Collects the unit orders of <paramref name="player"/> in <paramref name="pending"/> that have a place to draw, in issue order. A plain
    /// order replaces what the same unit was given earlier (the sim does the same: any plain order clears the queue); a queued waypoint adds.
    /// </summary>
    public static List<PausedOrder> Collect(IReadOnlyList<Command> pending, byte player)
    {
        var list = new List<PausedOrder>();
        foreach (Command c in pending)
        {
            if (c.Player != player) continue;
            ActorKind kind = (ActorKind)(c.Arg & 0xFF);
            PausedOrder? order = c.Kind switch
            {
                CommandKind.Move or CommandKind.Guard => new PausedOrder(c.Subject, kind, c.Cell, false, EntityId.None, ActorKind.Vehicle),
                CommandKind.AttackMove or CommandKind.AttackGround => new PausedOrder(c.Subject, kind, c.Cell, true, EntityId.None, ActorKind.Vehicle),
                CommandKind.QueueOrder => new PausedOrder(c.Subject, kind, c.Cell, (c.Arg & 0x100) != 0, EntityId.None, ActorKind.Vehicle),
                CommandKind.Attack => new PausedOrder(c.Subject, kind, default, true, c.Target, (ActorKind)((c.Arg >> 8) & 0xFF)),
                _ => null,
            };
            if (order is not { } o) continue;
            if (c.Kind != CommandKind.QueueOrder) list.RemoveAll(x => x.Subject == o.Subject && x.SubjectKind == o.SubjectKind);
            list.Add(o);
        }

        return list;
    }
}
