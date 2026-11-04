// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Quick-Set Foam constants (game.toml <c>[foam]</c>, design 08 §4, D-077, M4). SpeedPermille = ground speed multiplier on a
/// foamed cell (tune).</summary>
public sealed record FoamRules(int SpeedPermille)
{
    /// <summary>Design 08 starting value: half speed.</summary>
    public static FoamRules Default { get; } = new(500);
}
