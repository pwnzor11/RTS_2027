// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Sim.Pathing;

/// <summary>
/// M7-03: the movement class a vehicle path-finds and steps with. It is its <see cref="Locomotion"/>, except a large ship
/// (<see cref="VehicleType.DeepOnly"/>) which uses <see cref="NavalDeep"/>: the naval column with Shallow closed. The value is a
/// private extension of the enum that never reaches the rules tables (terrain lookups map it in <see cref="Map.MapState.SpeedPercent"/>).
/// </summary>
public static class MoveClass
{
    /// <summary>Deep-water-only naval class (value 64: fits the 7 bits the saved path request keeps for the locomotion).</summary>
    public const Locomotion NavalDeep = (Locomotion)64;

    /// <summary>The movement class of a vehicle type.</summary>
    public static Locomotion Of(VehicleType t) => t.Locomotion == Locomotion.Naval && t.DeepOnly ? NavalDeep : t.Locomotion;

    /// <summary>True for the two naval classes.</summary>
    public static bool IsNaval(Locomotion c) => c == Locomotion.Naval || c == NavalDeep;

    /// <summary>The rules-table locomotion behind a class (<see cref="NavalDeep"/> reads the naval column).</summary>
    public static Locomotion Table(Locomotion c) => c == NavalDeep ? Locomotion.Naval : c;
}
