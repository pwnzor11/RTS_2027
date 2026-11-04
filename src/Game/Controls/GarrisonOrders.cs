// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Controls;

/// <summary>What a right-click with one selected unit on a building means.</summary>
public enum BuildingClick
{
    /// <summary>Nothing special: the click falls through to a Move.</summary>
    None,
    /// <summary>Attack it.</summary>
    Attack,
    /// <summary>An Engineer enters to capture it.</summary>
    Capture,
    /// <summary>An Engineer enters to repair it.</summary>
    Repair,
    /// <summary>Enter an own tunnel entrance.</summary>
    EnterTunnel,
    /// <summary>Infantry enters a garrisonable building (M4).</summary>
    Garrison,
}

/// <summary>
/// Pure order picking for a right-click on a building (design 08 §3), so it is unit-tested. The sim re-checks every order; this only
/// decides which one the controls offer. Garrisoning: infantry with <c>can_garrison</c> may enter a garrisonable building that is
/// own or allied with room, or neutral and empty.
/// </summary>
public static class GarrisonOrders
{
    /// <summary>True if the infantry type may garrison and the building type is garrisonable (and not a tunnel entrance).</summary>
    public static bool CanGarrison(SimRules rules, ActorKind subjectKind, int subjectType, int buildingType)
    {
        if (subjectKind != ActorKind.Infantry || (uint)subjectType >= (uint)rules.Infantry.Count) return false;
        if ((uint)buildingType >= (uint)rules.Buildings.Count) return false;
        BuildingType b = rules.Buildings[buildingType];
        return rules.Infantry[subjectType].CanGarrison && b.Garrison > 0 && b.Entrance == TunnelSize.None;
    }

    /// <summary>Capacity of a building type (0 = not garrisonable).</summary>
    public static int CapacityOf(SimRules rules, int buildingType) =>
        (uint)buildingType < (uint)rules.Buildings.Count ? rules.Buildings[buildingType].Garrison : 0;

    /// <summary>
    /// What a right-click on <paramref name="building"/> does for a selected unit. <paramref name="relation"/> is the building owner
    /// relative to the local player. Garrison wins over Attack for neutral and allied garrisonables; Engineers still capture.
    /// </summary>
    public static BuildingClick Classify(SimRules rules, ActorKind subjectKind, int subjectType, BuildingView building, Relation relation)
    {
        if (building.Ghost) return BuildingClick.None;
        switch (relation)
        {
            case Relation.Self:
                if (M3Orders.EngineerOrderOn(rules, subjectKind, subjectType, true, building.TypeIndex, building.Health) == EngineerOrder.Repair)
                    return BuildingClick.Repair;
                if (M3Orders.CanEnterTunnel(rules, subjectKind, subjectType, building.TypeIndex)) return BuildingClick.EnterTunnel;
                if (HasRoom(rules, subjectKind, subjectType, building)) return BuildingClick.Garrison;
                return BuildingClick.None;
            case Relation.Ally:
                return HasRoom(rules, subjectKind, subjectType, building) ? BuildingClick.Garrison : BuildingClick.Attack;
            case Relation.Neutral:
                if (M3Orders.IsEngineer(rules, subjectKind, subjectType)) return BuildingClick.Capture;
                return building.Occupants == 0 && HasRoom(rules, subjectKind, subjectType, building) ? BuildingClick.Garrison : BuildingClick.Attack;
            default:
                return M3Orders.IsEngineer(rules, subjectKind, subjectType) ? BuildingClick.Capture : BuildingClick.Attack;
        }
    }

    /// <summary>True if D with this building selected should send Unload: it is the local player's and holds occupants.</summary>
    public static bool CanUnload(BuildingView? building, byte localPlayer) =>
        building is { Ghost: false, Occupants: > 0 } b && b.Player == localPlayer;

    /// <summary>The Unload command for a garrisoned building (Subject = building, Arg low byte = Building).</summary>
    public static Command Unload(byte player, EntityId building) =>
        new(0, player, 0, CommandKind.Unload, building, EntityId.None, default, (int)ActorKind.Building);

    private static bool HasRoom(SimRules rules, ActorKind kind, int type, BuildingView b) =>
        CanGarrison(rules, kind, type, b.TypeIndex) && b.Occupants < CapacityOf(rules, b.TypeIndex);
}
