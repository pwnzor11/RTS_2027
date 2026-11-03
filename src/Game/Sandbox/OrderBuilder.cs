// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Sandbox;

/// <summary>
/// Builds one sim <see cref="Command"/> for one subject, encoding Arg as the sim expects (see the Command doc):
/// Move, Stop and Scatter carry the subject kind; AttackMove and Guard carry the subject kind; Attack carries the
/// subject kind in the low byte and the target kind in bits 8..15.
/// </summary>
public static class OrderBuilder
{
    /// <summary>Builds the command. <paramref name="target"/> is used by Attack only.</summary>
    public static Command Build(int execTick, byte player, ushort sequence, CommandKind kind, UnitKey subject,
        CellCoord cell, ActorRef target)
    {
        int subjectKind = (int)subject.Kind;
        return kind switch
        {
            CommandKind.Attack => new Command(execTick, player, sequence, kind, subject.Id, target.Id, default,
                subjectKind | ((int)target.Kind << 8)),
            _ => new Command(execTick, player, sequence, kind, subject.Id, EntityId.None, cell, subjectKind),
        };
    }
}
