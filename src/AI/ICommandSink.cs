// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.AI;

/// <summary>Where an AI sends its orders. The driver turns each call into an ordinary <see cref="Command"/>
/// (ExecTick, Player and Sequence are stamped by the driver, never by the AI).</summary>
public interface ICommandSink
{
    /// <summary>Issues one command. <paramref name="subjectKind"/> is the store of <paramref name="subject"/>; Arg is
    /// filled per the Command conventions (Move/Stop: subject kind; Attack: low byte subject kind, bits 8..15 target
    /// kind; AttackMove/Guard: low byte subject kind; any other kind: subject kind). <paramref name="arg2"/> goes to Arg2.</summary>
    void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default,
        ActorRef target = default, int arg2 = 0);
}
