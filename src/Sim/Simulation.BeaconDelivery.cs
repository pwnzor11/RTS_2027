// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.BeaconDelivery;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// S44 M-15 (BACKLOG M-15; design 07_expedition.md section 2.1: "exp_beacon ... can be delivered by landing craft or Skyhook airlift to any soft
// land cell"; Pioneer and Combat Diver build one). Command DropBeacon (Subject = actor, Arg low byte = ActorKind, Cell = target cell):
//  * Who: a vehicle whose type has beacon_drop (landing craft, Skyhook) or a soldier whose type has beacon_build (Pioneer, Combat Diver). The
//    value names the beacon building (a beachhead source); the owner pays that building's cost when the beacon is placed.
//  * Where: a soft land cell (buildable terrain, no overlay, no building, no unit on it), anywhere on the map. The Expedition beachhead-zone
//    rule is waived: that is how a beacon extends the zone (the beacon is a beachhead source like any other afterwards).
//  * How: the actor closes to within BeaconReach cells of the target (a ship goes to the nearest sea cell beside it, so a landing craft beaches at
//    the shore; a Skyhook flies there). A vehicle drops the beacon the tick it is there and idle; a soldier then builds it for the beacon's
//    build time standing next to the cell (leaving, dying or a new order cancels, nothing is paid). The structure-queue path is unchanged.
// ASSUMED (S44): a vehicle drop is instant, a soldier build takes the building's build_time (200 ticks = 10 s), the cost is paid at placement
// (an order is refused when the owner cannot afford it, and dropped if the money is gone by the time it is placed), reach 1 cell.
public sealed partial class Simulation
{
    /// <summary>The actor must be within this many cells (Chebyshev) of the target cell to drop or build.</summary>
    internal const int BeaconReach = 1;

    /// <summary>Set only while a beacon is being placed: <see cref="CheckBeachheadAndWater"/> then skips the beachhead-zone test.</summary>
    private bool _beaconIgnoresZone;

    /// <summary>The "beacon_orders" part (S44 M-15).</summary>
    internal BeaconOrderState BeaconOrders => _beaconOrders;

    /// <summary>Building type index of the beacon the actor can put down, or -1 (a type without beacon_drop / beacon_build, or an unknown id).</summary>
    internal int BeaconTypeOf(ActorKind kind, int slot)
    {
        SimRules rules = RulesOf(kind == ActorKind.Infantry ? _infantry.Player[slot] : _units.Player[slot]);
        string id = kind == ActorKind.Infantry
            ? rules.Infantry[_infantry.TypeIndex[slot]].BeaconBuild
            : rules.Vehicles[_units.TypeIndex[slot]].BeaconDrop;
        if (id.Length == 0) return -1;
        for (int b = 0; b < rules.Buildings.Count; b++)
            if (string.Equals(rules.Buildings[b].Id, id, StringComparison.Ordinal)) return b;
        return -1;
    }

    /// <summary>True if the terrain and buildings allow a beacon on <paramref name="cell"/> (units are checked when it is placed).</summary>
    internal bool BeaconSiteOk(byte player, CellCoord cell)
    {
        if (!InMap(cell)) return false;
        LandType land = _map.LandAt(cell);
        if (land is LandType.Water or LandType.Shallow) return false;
        return RulesOf(player).Terrain.Buildable(land) && _map.OverlayAt(cell) == OverlayKind.None && _map.BuildingAt(cell).IsNone;
    }

    /// <summary>PlayerView: does the own actor carry out DropBeacon at all (its type names a beacon)?</summary>
    internal bool CanBuildBeacon(byte player, ActorKind kind, EntityId actor)
    {
        MobileStore? store = StoreOf(kind);
        if (store is null || !store.Slots.IsAlive(actor) || store.Player[actor.Index] != player) return false;
        return BeaconTypeOf(kind, actor.Index) >= 0;
    }

    private void ApplyDropBeacon(Command c)
    {
        var kind = (ActorKind)(c.Arg & 0xFF);
        if (kind is not (ActorKind.Vehicle or ActorKind.Infantry) || StoreOf(kind) is not { } store) return;
        int i = c.Subject.Index;
        if (!store.Slots.IsAlive(c.Subject) || store.Player[i] != c.Player || store.Health[i] <= 0) return;
        if (ActorLayers.IsCarried(_tunnels.LayerOf(kind, i))) return;
        int type = BeaconTypeOf(kind, i);
        if (type < 0 || !BeaconSiteOk(c.Player, c.Cell)) return;
        BuildingType bt = RulesOf(c.Player).Buildings[type];
        if (Funds(c.Player) < bt.Cost) return;
        CellCoord here = store.Position[i].ToCell(), target = c.Cell;
        if (LeptonMath.ChebyshevCells(here, target) > BeaconReach)
        {
            CellCoord goal = ApproachCell(kind, store, i, here, target);
            if (goal.X < 0) return;   // no reachable cell beside the target (a ship far from this shore)
            ApplyMovementCommand(new Command(c.ExecTick, c.Player, c.Sequence, CommandKind.Move, c.Subject, default, goal, (int)kind));
            if (store.NextMission[i] != MissionKind.Move || store.NextCell[i] != goal) return;   // the move was refused
        }
        _beaconOrders.Set(kind, i, target);
    }

    /// <summary>The cell beside <paramref name="target"/> (8 neighbours) nearest to the actor that it may be sent to; X = -1 when none.</summary>
    private CellCoord ApproachCell(ActorKind kind, MobileStore store, int i, CellCoord here, CellCoord target)
    {
        CellCoord best = new(-1, -1);
        int bestD = int.MaxValue;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var c = new CellCoord(target.X + dx, target.Y + dy);
                if ((dx == 0 && dy == 0) || !InMap(c) || MoveGoalRefused(kind, store, i, c)) continue;
                int d = LeptonMath.ChebyshevCells(here, c);
                if (d < bestD) { bestD = d; best = c; }   // first in row-major order wins a tie
            }
        return best;
    }

    /// <summary>Per tick after movement (slot order): pending beacon orders arrive, build and place.</summary>
    private void RunBeaconDelivery()
    {
        RunBeaconOrders(ActorKind.Vehicle, _units);
        RunBeaconOrders(ActorKind.Infantry, _infantry);
    }

    private void RunBeaconOrders(ActorKind kind, MobileStore store)
    {
        BeaconOrderState o = _beaconOrders;
        for (int i = 1; i < store.Capacity; i++)
        {
            if (!o.IsSet(kind, i)) continue;
            if (!store.Slots.IsIndexAlive(i) || store.Health[i] <= 0) { o.Clear(kind, i); continue; }
            CellCoord target = o.Target(kind, i);
            if (HeadingAwayFrom(store, i, target)) { o.Clear(kind, i); continue; }   // re-ordered elsewhere
            bool moving = store.Mission[i] == MissionKind.Move || store.NextMission[i] == MissionKind.Move;
            if (LeptonMath.ChebyshevCells(store.Position[i].ToCell(), target) > BeaconReach)
            {
                if (!moving) o.Clear(kind, i);   // stopped or blocked short of the target
                continue;
            }
            if (moving) continue;   // wait until it stands still beside the cell
            int type = BeaconTypeOf(kind, i);
            if (type < 0) { o.Clear(kind, i); continue; }
            byte owner = store.Player[i];
            BuildingType bt = RulesOf(owner).Buildings[type];
            if (kind == ActorKind.Infantry && o.Advance(kind, i) < SimRules.BuildTicks(bt.Cost, bt.BuildTime, RulesOf(owner).Economy)) continue;
            PlaceBeacon(owner, type, bt, target);
            o.Clear(kind, i);
        }
    }

    /// <summary>True if the actor's current or queued Move goal is more than <see cref="BeaconReach"/> cells from the target.</summary>
    private static bool HeadingAwayFrom(MobileStore store, int i, CellCoord target)
    {
        if (store.NextMission[i] == MissionKind.Move) return LeptonMath.ChebyshevCells(store.NextCell[i], target) > BeaconReach;
        return store.Mission[i] == MissionKind.Move && store.PathActive[i] && LeptonMath.ChebyshevCells(store.PathGoal[i], target) > BeaconReach;
    }

    /// <summary>Pays for and places the beacon (zone rule waived); false and nothing paid when it is not possible any more.</summary>
    private bool PlaceBeacon(byte owner, int type, BuildingType bt, CellCoord cell)
    {
        if (Funds(owner) < bt.Cost || RadiationDeniesBuilding(bt, cell)) return false;
        _beaconIgnoresZone = true;
        PlacementResult r;
        try { r = CanPlace(owner, type, cell); }
        finally { _beaconIgnoresZone = false; }
        if (r != PlacementResult.Ok) return false;
        EntityId id = PlaceBuildingNow(owner, type, cell, bt.MaxHealth);
        if (id.IsNone) return false;
        Spend(owner, bt.Cost);
        OnStatsBuilt(owner, ActorKind.Building);
        return true;
    }
}
