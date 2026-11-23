// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// Movement options (game.toml <c>[movement]</c>, S41 M13-44). <see cref="StuckRecovery"/>: 0 = off (the movement of every earlier
/// session, byte for byte), 1 = a per-unit stuck clock measured by net displacement and a recovery ladder (replan, sidestep, relax,
/// abandon) for units that stand wedged with a move goal.
/// </summary>
public sealed record MovementRules(int StuckRecovery)
{
    /// <summary>Off.</summary>
    public static MovementRules Default { get; } = new(0);
}
