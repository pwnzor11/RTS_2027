// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Controls;

/// <summary>How a unit takes the Deploy order.</summary>
public enum DeployMode
{
    /// <summary>The unit cannot deploy or unload.</summary>
    None,
    /// <summary>It deploys or unloads where it stands (the cell is ignored): the land-train, a transport.</summary>
    InPlace,
    /// <summary>It deploys at a chosen cell: a burrowing deployer such as the Bore, which digs a new entrance there.</summary>
    AtCell,
}

/// <summary>What an Engineer's Enter order on a building does (the sim re-checks it).</summary>
public enum EngineerOrder
{
    /// <summary>Nothing: not an Engineer, or the building needs neither.</summary>
    None,
    /// <summary>Enter an enemy or neutral building to capture it.</summary>
    Capture,
    /// <summary>Enter an own damaged building to repair it.</summary>
    Repair,
}

/// <summary>
/// Which M3 orders a unit can take, from the rules alone (pure, so it is unit-tested). The sim re-checks everything; these only
/// decide which orders the controls offer, so a click on a Hatch does not send a tank to "enter" it.
/// </summary>
public static class M3Orders
{
    /// <summary>How a vehicle takes Deploy: a transport unloads in place, a deployer deploys in place or (Burrow locomotion) at a cell.</summary>
    public static DeployMode DeployModeOf(SimRules rules, ActorKind kind, int typeIndex)
    {
        if (kind != ActorKind.Vehicle || (uint)typeIndex >= (uint)rules.Vehicles.Count) return DeployMode.None;
        VehicleType v = rules.Vehicles[typeIndex];
        if (v.Passengers > 0) return DeployMode.InPlace;
        if (v.DeploysInto < 0) return DeployMode.None;
        return v.Locomotion == Locomotion.Burrow ? DeployMode.AtCell : DeployMode.InPlace;
    }

    /// <summary>The tunnel size of a unit type (None for Authority units and unknown types).</summary>
    public static TunnelSize TunnelSizeOf(SimRules rules, ActorKind kind, int typeIndex)
    {
        if (kind == ActorKind.Vehicle) return (uint)typeIndex < (uint)rules.Vehicles.Count ? rules.Vehicles[typeIndex].TunnelSize : TunnelSize.None;
        if (kind == ActorKind.Infantry) return (uint)typeIndex < (uint)rules.Infantry.Count ? rules.Infantry[typeIndex].TunnelSize : TunnelSize.None;
        return TunnelSize.None;
    }

    /// <summary>True if a unit may be sent into an own entrance building: it has a tunnel size and fits that entrance.</summary>
    public static bool CanEnterTunnel(SimRules rules, ActorKind subjectKind, int subjectType, int buildingType)
    {
        if ((uint)buildingType >= (uint)rules.Buildings.Count) return false;
        TunnelSize entrance = rules.Buildings[buildingType].Entrance;
        TunnelSize unit = TunnelSizeOf(rules, subjectKind, subjectType);
        return entrance != TunnelSize.None && unit != TunnelSize.None && unit <= entrance;
    }

    /// <summary>True if an infantry unit may board a vehicle type (a transport with passenger room).</summary>
    public static bool CanBoard(SimRules rules, ActorKind subjectKind, int transportType) =>
        subjectKind == ActorKind.Infantry && (uint)transportType < (uint)rules.Vehicles.Count && rules.Vehicles[transportType].Passengers > 0;

    /// <summary>True if the infantry type is an Engineer.</summary>
    public static bool IsEngineer(SimRules rules, ActorKind kind, int typeIndex) =>
        kind == ActorKind.Infantry && (uint)typeIndex < (uint)rules.Infantry.Count && rules.Infantry[typeIndex].Engineer;

    /// <summary>
    /// What a right-click with a selected unit on a building means for an Engineer: a building that is not the local player's is a
    /// capture (the sim only accepts it when the building is neutral or weak), an own building below full health is a repair.
    /// </summary>
    public static EngineerOrder EngineerOrderOn(SimRules rules, ActorKind subjectKind, int subjectType, bool ownBuilding, int buildingType, int buildingHealth)
    {
        if (!IsEngineer(rules, subjectKind, subjectType) || (uint)buildingType >= (uint)rules.Buildings.Count) return EngineerOrder.None;
        if (!ownBuilding) return EngineerOrder.Capture;
        return buildingHealth < rules.Buildings[buildingType].MaxHealth ? EngineerOrder.Repair : EngineerOrder.None;
    }
}
