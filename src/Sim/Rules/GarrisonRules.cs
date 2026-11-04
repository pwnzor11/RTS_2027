// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Garrison constants (game.toml <c>[garrison]</c>, design 08 §3, M4). All (tune). EjectDamagePermille = share of max health
/// each occupant loses when its building dies or is sold; EnterRangeCells = how close (cells, to the footprint) a soldier must be to
/// get in.</summary>
public sealed record GarrisonRules(int EjectDamagePermille, int EnterRangeCells)
{
    /// <summary>Design 08 starting values.</summary>
    public static GarrisonRules Default { get; } = new(200, 1);
}
