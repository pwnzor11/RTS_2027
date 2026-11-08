// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Controls;

/// <summary>What a right-click means for a harvester (M11-10a).</summary>
public enum HarvestClick
{
    /// <summary>Not a harvest click: the click falls through to the other orders (a Move for most units).</summary>
    None,
    /// <summary>Harvest at the clicked resource cell (sends <see cref="CommandKind.Harvest"/> with that cell).</summary>
    Harvest,
    /// <summary>Deliver the cargo to the clicked own refinery (sends <see cref="CommandKind.Return"/>).</summary>
    Return,
}

/// <summary>
/// Pure right-click rules for harvesters (scrapers and every other vehicle with <c>harvest_capacity</c> above 0, of all factions):
/// a click on a resource field is a Harvest there, a click on an own refinery is a Return. Salvage on wrecks stays in
/// <see cref="SalvageOrders"/>. The cursor code can ask the same question with <see cref="Classify"/> to show a harvest cursor.
/// </summary>
public static class HarvestOrders
{
    /// <summary>Cargo capacity of a unit type (0 = cannot harvest; infantry and unknown types give 0).</summary>
    public static int CapacityOf(SimRules rules, ActorKind kind, int typeIndex) =>
        kind == ActorKind.Vehicle && (uint)typeIndex < (uint)rules.Vehicles.Count ? rules.Vehicles[typeIndex].HarvestCapacity : 0;

    /// <summary>True for a vehicle with harvest_capacity above 0.</summary>
    public static bool CanHarvest(SimRules rules, ActorKind kind, int typeIndex) => CapacityOf(rules, kind, typeIndex) > 0;

    /// <summary>True when an overlay is something a harvester collects (ashfall or pitchblende; the sim clears the overlay when a cell is used up).</summary>
    public static bool IsResource(OverlayKind overlay) => overlay is OverlayKind.Ashfall or OverlayKind.Pitchblende;

    /// <summary>True when the building type is a refinery (harvesters unload there).</summary>
    public static bool IsRefinery(SimRules rules, int buildingType) =>
        (uint)buildingType < (uint)rules.Buildings.Count && rules.Buildings[buildingType].Refinery;

    /// <summary>
    /// What a right-click does for one selected unit.
    /// </summary>
    /// <param name="rules">The rules.</param>
    /// <param name="kind">The unit's kind.</param>
    /// <param name="typeIndex">The unit's type index.</param>
    /// <param name="overlay">The overlay on the clicked cell (<see cref="OverlayKind.None"/> off the map).</param>
    /// <param name="building">The building under the cursor, or null.</param>
    /// <param name="buildingIsOwn">True when <paramref name="building"/> belongs to the local player.</param>
    public static HarvestClick Classify(SimRules rules, ActorKind kind, int typeIndex, OverlayKind overlay, BuildingView? building, bool buildingIsOwn)
    {
        if (!CanHarvest(rules, kind, typeIndex)) return HarvestClick.None;
        if (building is { Ghost: false } b)
            return buildingIsOwn && IsRefinery(rules, b.TypeIndex) ? HarvestClick.Return : HarvestClick.None;
        return IsResource(overlay) ? HarvestClick.Harvest : HarvestClick.None;
    }
}
