// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Sim-side infantry type (5 per cell, crushable by Crusher vehicles).</summary>
public sealed record InfantryType(string Id, int Speed, int MaxHealth, ArmorClass Armor, int Sight,
    int PrimaryWeapon, int Cost, IReadOnlyList<string> Prerequisites)
{
    /// <summary>Build time in ticks; 0 = derive from cost.</summary>
    public int BuildTime { get; init; }
}
