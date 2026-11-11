// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// Actor-store view (M1.5-01). The sim has three live actor kinds with separate stores: vehicles (UnitStore),
// infantry (InfantryStore) and buildings (BuildingStore). Code that has to treat them alike goes through the
// accessors below instead of repeating a three-way switch.
//
// ADDING A 4TH KIND: extend ActorKind and LiveKinds, then every switch in this file, plus these sites that
// know about kinds on their own:
//   - Simulation.cs        : the ctor (store + state part), StoreOf, TryResolve, ApplyInput/Apply (Sell, Place),
//                            Spawn*/PlaceBuilding, RunCleanup (removal of dead actors), Snapshot (views)
//   - Simulation.Movement.cs: OnActorSpawned / OnActorRemoving (occupancy claim and release)
//   - Simulation.Combat.cs : ApplyCombatCommand (which kinds may be ordered), RunCombat (reload loop, building
//                            turrets are a mission-less kind), Aim (mobile only), Hurt (WakeOnHit is mobile only)
//   - Combat/CombatState.cs: one entry array per kind
//   - Vision/VisionState.cs: KindCount (actor reveal slots)
//   - Simulation.Hashing.cs / Serialization: state-part order and layout
// Iteration order is fixed: vehicles, infantry, buildings, each by ascending slot index. Hashes depend on it.
public sealed partial class Simulation
{
    /// <summary>Number of live actor kinds (Vehicle, Infantry, Building), iterated in that order as (ActorKind)0..2.</summary>
    internal const int LiveKinds = 3;

    internal SlotArray SlotsOf(ActorKind kind) => kind switch
    {
        ActorKind.Vehicle => _units.Slots,
        ActorKind.Infantry => _infantry.Slots,
        _ => _buildings.Slots,
    };

    /// <summary>Slot capacity of the kind's store (loops run 1 to CapacityOf - 1).</summary>
    internal int CapacityOf(ActorKind kind) => kind switch
    {
        ActorKind.Vehicle => _units.Capacity,
        ActorKind.Infantry => _infantry.Capacity,
        _ => _buildings.Capacity,
    };

    /// <summary>True if the slot holds a live actor (health may still be 0 until cleanup).</summary>
    internal bool IsLive(ActorKind kind, int i) => SlotsOf(kind).IsIndexAlive(i);

    internal EntityId IdAt(ActorKind kind, int i) => SlotsOf(kind).IdOf(i);

    internal byte PlayerAt(ActorKind kind, int i) => kind switch
    {
        ActorKind.Vehicle => _units.Player[i],
        ActorKind.Infantry => _infantry.Player[i],
        _ => _buildings.Player[i],
    };

    internal int TypeAt(ActorKind kind, int i) => kind switch
    {
        ActorKind.Vehicle => _units.TypeIndex[i],
        ActorKind.Infantry => _infantry.TypeIndex[i],
        _ => _buildings.TypeIndex[i],
    };

    internal int HealthAt(ActorKind kind, int i) => kind switch
    {
        ActorKind.Vehicle => _units.Health[i],
        ActorKind.Infantry => _infantry.Health[i],
        _ => _buildings.Health[i],
    };

    /// <summary>Position of the actor; for a building, the centre of its footprint.</summary>
    internal LeptonCoord PositionAt(ActorKind kind, int i) => kind switch
    {
        ActorKind.Vehicle => _units.Position[i],
        ActorKind.Infantry => _infantry.Position[i],
        _ => BuildingCentre(i),
    };

    internal ArmorClass ArmorAt(ActorKind kind, int i)
    {
        SimRules r = RulesOf(PlayerAt(kind, i));
        return kind switch
        {
            ActorKind.Vehicle => IsHiddenSub(kind, i) ? ArmorClass.Submerged : r.Vehicles[_units.TypeIndex[i]].Armor,   // D-205 (LE-06): submerged armour only while dived and unseen
            ActorKind.Infantry => r.Infantry[_infantry.TypeIndex[i]].Armor,
            _ => r.Buildings[_buildings.TypeIndex[i]].Armor,
        };
    }

    /// <summary>Sight radius in cells of any live kind.</summary>
    internal int SightOf(ActorKind kind, int i)
    {
        SimRules r = RulesOf(PlayerAt(kind, i));
        return kind switch
        {
            ActorKind.Vehicle => r.Vehicles[_units.TypeIndex[i]].Sight,
            ActorKind.Infantry => r.Infantry[_infantry.TypeIndex[i]].Sight,
            _ => r.Buildings[_buildings.TypeIndex[i]].Sight,
        };
    }

    /// <summary>Primary weapon index of the actor's type, or -1.</summary>
    internal int WeaponOf(ActorKind kind, int i)
    {
        SimRules r = RulesOf(PlayerAt(kind, i));
        int w = kind switch
        {
            ActorKind.Vehicle => r.Vehicles[_units.TypeIndex[i]].PrimaryWeapon,
            ActorKind.Infantry => r.Infantry[_infantry.TypeIndex[i]].PrimaryWeapon,
            ActorKind.Building => r.Buildings[_buildings.TypeIndex[i]].PrimaryWeapon,
            _ => -1,
        };
        if (kind == ActorKind.Vehicle && w >= 0 && r.Vehicles[_units.TypeIndex[i]].Ammo > 0 && _naval.AmmoLeft[i] <= 0) return -1;   // D-156: out of rounds, cannot attack
        return r.Weapons != null && w >= 0 && w < r.Weapons.Count ? w : -1;
    }

    private byte PlayerOf(ActorKind kind, int i) => PlayerAt(kind, i);

    private LeptonCoord ShooterPos(ActorKind kind, int i) => PositionAt(kind, i);

    /// <summary>The cell an actor reveals vision from: its own cell, or for a building the footprint's middle cell.</summary>
    private CellCoord VisionCellOf(ActorKind kind, int i)
    {
        if (kind != ActorKind.Building) return PositionAt(kind, i).ToCell();
        BuildingType bt = RulesOf(_buildings.Player[i]).Buildings[_buildings.TypeIndex[i]];
        CellCoord c = _buildings.Cell[i];
        return new CellCoord(c.X + bt.FootprintW / 2, c.Y + bt.FootprintH / 2);
    }

    /// <summary>Footprint size in cells of a building slot.</summary>
    private BuildingType BuildingTypeAt(int i) => RulesOf(_buildings.Player[i]).Buildings[_buildings.TypeIndex[i]];
}
