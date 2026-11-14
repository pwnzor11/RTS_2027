// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.AI;

/// <summary>
/// S24 (M5-12, D-241): the standing-order half of a commander. <see cref="AiDriver"/> asks it once per tick, right after the directive poll, and
/// appends the commands it returns to the same output list with the slot's own command sequence, so orders enter the recorded command stream
/// (skirmish, LAN, replays) exactly like directives. Implementations must not block.
/// </summary>
public interface IOrderSource
{
    /// <summary>Appends the <see cref="CommandKind.CommanderOrder"/> commands that became due, for <paramref name="player"/>, executing at
    /// <paramref name="execTick"/>. Each command advances <paramref name="sequence"/> by one.</summary>
    void TakeOrderCommands(int execTick, byte player, ref ushort sequence, List<Command> outCommands);
}
