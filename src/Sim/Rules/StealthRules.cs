// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Stealth constants (game.toml <c>[stealth]</c>, design 05 §3). DelayTicks = ticks without moving or firing before a
/// stealth unit becomes invisible to enemies (tune).</summary>
public sealed record StealthRules(int DelayTicks)
{
    /// <summary>Design 05 starting value.</summary>
    public static StealthRules Default { get; } = new(60);
}
