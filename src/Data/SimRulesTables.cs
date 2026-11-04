// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>The parsed, id-sorted type tables the post-parse checks need (M4-01: effect targets, tier sugar).</summary>
internal sealed record SimRulesTables(IReadOnlyList<VehicleType> Vehicles, IReadOnlyList<InfantryType> Infantry,
    IReadOnlyList<BuildingType> Buildings, IReadOnlyList<WeaponType> Weapons, IReadOnlyList<WarheadType> Warheads);
