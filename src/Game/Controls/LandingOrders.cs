// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Controls;

/// <summary>
/// Pure order picking for landing transports (M7-14, D-139b): a landing craft (<c>unload_ticks</c> above 0) or an aircraft transport
/// (Skyhook) that carries passengers answers a right-click on a land cell with an Unload at that cell instead of a Move. The sim
/// re-checks everything (<c>Simulation.ApplyLandingUnload</c>); this only decides which order the controls offer.
/// </summary>
public static class LandingOrders
{
    /// <summary>True for a vehicle type that unloads onto a chosen shore cell: it carries passengers and has unload_ticks or flies.</summary>
    public static bool IsLandingTransport(SimRules rules, ActorKind kind, int typeIndex)
    {
        if (kind != ActorKind.Vehicle || (uint)typeIndex >= (uint)rules.Vehicles.Count) return false;
        VehicleType v = rules.Vehicles[typeIndex];
        return v.Passengers > 0 && (v.UnloadTicks > 0 || v.Locomotion == Locomotion.Air);
    }

    /// <summary>True if foot soldiers can stand on the cell (the sim's own test for a shore cell: terrain speed for foot above 0).</summary>
    public static bool FootStandable(SimRules rules, MapData map, CellCoord cell) =>
        map.Contains(cell) && rules.Terrain.SpeedPercent(map.Land[map.IndexOf(cell)], Locomotion.Foot) > 0;

    /// <summary>
    /// True if a right-click on <paramref name="cell"/> should send Unload: the unit is a landing transport with at least one passenger
    /// aboard and the cell is land.
    /// </summary>
    public static bool ShouldUnload(SimRules rules, MapData map, ActorKind kind, int typeIndex, int passengers, CellCoord cell) =>
        passengers > 0 && IsLandingTransport(rules, kind, typeIndex) && FootStandable(rules, map, cell);
}
