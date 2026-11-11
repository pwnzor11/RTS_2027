// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Production;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// M6-03 (OWNER: M6-03 package; hooks placed by the lead in session 9). Rig pack/unpack (docs/design/10_m6_plan.md §3.2–3.3, D-102 Q-A).
// Hooks: Apply (CommandKind.Pack) -> ApplyPack; StepCore -> RunRigs (after RunProduction); MissionKind.Pack -> Missions/RigMission.cs;
// PlayerView.PackProgressPermille -> PackProgressOf.
//
// Decisions (M6-03):
//  * Timing: a Pack command executed on tick T sets the counter to N = pack_ticks x upgrade permille / 1000 (min 1); RunRigs counts it
//    down on the same tick, so the swap happens during the Step of tick T + N - 1 (N steps in all). Unpack counts the same way from the
//    tick the rig has stopped inside the footprint.
//  * Production queues are owned per player per category lane, not per building, so they are never moved: the lane keeps its items and
//    its progress. A lane is *paused* while the player has a packing or packed rig producing that category and no other live building
//    producing it: unit lanes stop because a packing building no longer counts as a factory (CountFactories/PrimaryFactoryOf),
//    structure lanes via RigLanePaused in RunProductionTick. RigState.PausedQueue (per vehicle slot) records lane + 1 as the handle.
//  * Unpack site rules: as a normal Place (map, buildable terrain, no building, no other ground actor, one-cell-gap rule) but no
//    build-radius requirement. The rig itself may stand on the footprint.
//  * A Pack/Move order given to a rig that is unpacking aborts the unpack (the rig stays packed); only death is not "an order".
public sealed partial class Simulation
{
    /// <summary>CommandKind.Pack: Subject = an own rig building (pack) or an own packed rig vehicle (unpack at Cell = top-left).</summary>
    private void ApplyPack(Command c)
    {
        if (_players.IsNeutral(c.Player)) return;
        // An EntityId does not say which store it indexes: a packed rig of the player wins over a building with the same slot and generation.
        bool unpack = _units.Slots.IsAlive(c.Subject) && _units.Player[c.Subject.Index] == c.Player && _rigs.IsPackedRig(c.Subject.Index);
        if (!unpack && _buildings.Slots.IsAlive(c.Subject))
        {
            int b = c.Subject.Index;
            if (_buildings.Player[b] != c.Player || _buildings.Health[b] <= 0 || _rigs.PackTicksLeft[b] != 0) return;
            SimRules rules = RulesOf(c.Player);
            BuildingType bt = rules.Buildings[_buildings.TypeIndex[b]];
            if (bt.PacksInto < 0 || bt.PacksInto >= rules.Vehicles.Count || _tunnels.BuildingMorphType[b] != 0) return;
            _rigs.PackTicksLeft[b] = ScaledPackTicks(c.Player, bt.PackTicks);
            SupplyChanged();   // SUP-1: a packing rig stops counting at once
            return;
        }
        if (!_units.Slots.IsAlive(c.Subject)) return;
        int i = c.Subject.Index;
        if (_units.Player[i] != c.Player || _units.Health[i] <= 0 || !_rigs.IsPackedRig(i) || _rigs.UnpackTicksLeft[i] != 0) return;
        int into = UnpackTypeOf(i);
        if (into < 0 || into >= RulesOf(c.Player).Buildings.Count || !InMap(c.Cell)) return;
        if (!UnpackSiteOk(c.Player, into, c.Cell, i)) return;
        _rigs.UnpackX[i] = c.Cell.X; _rigs.UnpackY[i] = c.Cell.Y;
        Queue(_units, i, MissionKind.Pack, c.Cell, default);
    }

    /// <summary>Building type a packed rig unpacks into: the type it was packed from (RigState.PackedFrom), so one generic packed
    /// vehicle (lb_mother_rig_packed) serves every rig; else the vehicle's unpacks_into (start-kit rigs). Lead fix, session 9.</summary>
    internal int UnpackTypeOf(int v)
    {
        int from = _rigs.PackedFrom[v] - 1;
        return from >= 0 ? from : RulesOf(_units.Player[v]).Vehicles[_units.TypeIndex[v]].UnpacksInto;
    }

    /// <summary>pack_ticks scaled by the owner's Quick Pack upgrades (min 1).</summary>
    internal int ScaledPackTicks(byte player, int baseTicks) =>
        (int)Math.Max(1L, (long)baseTicks * UpgradePermille(player, u => u.PackTicksPermille) / 1000);

    /// <summary>Footprint of building type <paramref name="type"/> at <paramref name="topLeft"/> is placeable for a rig (vehicle slot
    /// <paramref name="self"/> may stand on it): no build radius, otherwise the CanPlace rules.</summary>
    internal bool UnpackSiteOk(byte player, int type, CellCoord topLeft, int self)
    {
        BuildingType bt = RulesOf(player).Buildings[type];
        int w = bt.FootprintW, h = bt.FootprintH, x0 = topLeft.X, y0 = topLeft.Y;
        if (x0 < 0 || y0 < 0 || x0 + w > Setup.Map.Width || y0 + h > Setup.Map.Height) return false;
        TerrainTable table = RulesOf(player).Terrain;
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                var c = new CellCoord(x, y);
                if (!table.Buildable(_map.LandAt(c)) || _map.OverlayAt(c) != OverlayKind.None || !_map.BuildingAt(c).IsNone) return false;
                EntityId occ = _map.VehicleAt(c);
                if (!occ.IsNone && occ.Index != self) return false;   // MapState.IsBuildable would also refuse the rig's own cell
                for (int sub = 0; sub < InfantryStore.SubSlotCount; sub++)
                    if (!_map.InfantryAt(c, sub).IsNone) return false;
            }
        for (int v = 1; v < _units.Capacity; v++)
            if (v != self && _units.Slots.IsIndexAlive(v) && InRect(_units.Position[v].ToCell(), x0, y0, w, h) && !IsAirUnit(ActorKind.Vehicle, v)) return false;
        for (int n = 1; n < _infantry.Capacity; n++)
            if (_infantry.Slots.IsIndexAlive(n) && InRect(_infantry.Position[n].ToCell(), x0, y0, w, h)) return false;
        if (bt.Wall || bt.Gate) return true;
        return !SealsRegion(x0, y0, w, h);
    }

    /// <summary>The centre cell of a footprint (rounded toward the top-left).</summary>
    internal static CellCoord FootprintCentre(CellCoord topLeft, BuildingType bt) =>
        new(topLeft.X + (bt.FootprintW - 1) / 2, topLeft.Y + (bt.FootprintH - 1) / 2);

    /// <summary>True while the player's <paramref name="category"/> structure lane must not advance: a rig producing it is packing or
    /// packed and no other live building of the player produces it.</summary>
    private bool RigLanePaused(byte player, ProductionCategory category)
    {
        bool rig = false;
        SimRules rules = RulesOf(player);
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (!_buildings.Slots.IsIndexAlive(i) || _buildings.Player[i] != player || BuildingTypeAt(i).Produces != category) continue;
            if (_rigs.PackTicksLeft[i] == 0) return false;
            rig = true;
        }
        if (rig) return true;
        for (int v = 1; v < _units.Capacity; v++)
        {
            if (!_units.Slots.IsIndexAlive(v) || _units.Player[v] != player || !_rigs.IsPackedRig(v)) continue;
            int from = _rigs.PackedFrom[v] - 1;
            if (from < rules.Buildings.Count && rules.Buildings[from].Produces == category) return true;
        }
        return _players.Faction[player] == Faction.LeftBehind && RootRigDown(player, rules);   // M13-09 (D-102)
    }

    /// <summary>M13-09 (D-102: a packed rig cannot produce): true when a Left Behind player's conyard-root rig (a packable building with no
    /// prerequisites, the Mother Rig) is packing or packed and no root rig of theirs stands unpacked. Shipped data gives no building
    /// <c>Produces</c> = Structure or Defence, so the structure and defence lanes are tied to the root rig instead.</summary>
    private bool RootRigDown(byte player, SimRules rules)
    {
        bool packing = false;
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (!_buildings.Slots.IsIndexAlive(i) || _buildings.Player[i] != player) continue;
            BuildingType bt = BuildingTypeAt(i);
            if (bt.PacksInto < 0 || bt.Prerequisites.Count != 0) continue;
            if (_rigs.PackTicksLeft[i] == 0) return false;
            packing = true;
        }
        if (packing) return true;
        for (int v = 1; v < _units.Capacity; v++)
        {
            if (!_units.Slots.IsIndexAlive(v) || _units.Player[v] != player || !_rigs.IsPackedRig(v)) continue;
            int from = _rigs.PackedFrom[v] - 1;
            if (from < rules.Buildings.Count && rules.Buildings[from].PacksInto >= 0 && rules.Buildings[from].Prerequisites.Count == 0) return true;
        }
        return false;
    }

    /// <summary>Per tick after production: advance packing and unpacking timers.</summary>
    private void RunRigs()
    {
        for (int b = 1; b < _buildings.Capacity; b++)
        {
            if (_rigs.PackTicksLeft[b] <= 0 || !_buildings.Slots.IsIndexAlive(b)) continue;
            if (_buildings.Health[b] <= 0) continue;   // dying: the normal death path removes it this tick
            if (--_rigs.PackTicksLeft[b] == 0) FinishPack(b);
        }
        for (int v = 1; v < _units.Capacity; v++)
        {
            if (_rigs.UnpackTicksLeft[v] <= 0 || !_units.Slots.IsIndexAlive(v) || _units.Health[v] <= 0) continue;
            if (_units.Mission[v] != MissionKind.Pack || _units.NextMission[v] != MissionKind.None)
            {   // re-ordered while unpacking: abort, stay packed
                _rigs.UnpackTicksLeft[v] = 0;
                continue;
            }
            if (--_rigs.UnpackTicksLeft[v] == 0) FinishUnpack(v);
        }
    }

    private void FinishPack(int b)
    {
        byte owner = _buildings.Player[b];
        SimRules rules = RulesOf(owner);
        int btype = _buildings.TypeIndex[b];
        BuildingType bt = rules.Buildings[btype];
        CellCoord tl = _buildings.Cell[b];
        int health = _buildings.Health[b];
        int permille = (int)((long)health * 1000 / Math.Max(1, bt.MaxHealth));
        EntityId id = _buildings.Slots.IdOf(b);
        RemoveBuilding(id);
        CellCoord centre = FootprintCentre(tl, bt);
        VehicleType vt = rules.Vehicles[bt.PacksInto];
        if (!FindFreeExitCell(centre, Pathing.MoveClass.Of(vt), out CellCoord cell)) cell = centre;
        EntityId vid = SpawnVehicle(owner, bt.PacksInto, cell);
        if (vid.IsNone)
        {   // vehicle store full: put the building back and retry next tick
            EntityId back = PlaceBuildingNow(owner, btype, tl, health);
            if (!back.IsNone) { _rigs.PackTicksLeft[back.Index] = 1; SupplyChanged(); }   // SUP-1
            return;
        }
        int v = vid.Index;
        _units.Facing[v] = 128;   // south
        _rigs.PackedFrom[v] = btype + 1;
        _rigs.HpPermille[v] = permille;
        _units.Health[v] = (int)Math.Max(1L, (long)vt.MaxHealth * permille / 1000);
        if (bt.Produces is ProductionCategory cat) _rigs.PausedQueue[v] = ProductionState.Lane(owner, cat) + 1;
        QueueGuard(v);
    }

    private void FinishUnpack(int v)
    {
        byte owner = _units.Player[v];
        SimRules rules = RulesOf(owner);
        int into = UnpackTypeOf(v);
        var tl = new CellCoord(_rigs.UnpackX[v], _rigs.UnpackY[v]);
        if (into < 0 || into >= rules.Buildings.Count || !UnpackSiteOk(owner, into, tl, v))
        {   // blocked now: stay packed
            _rigs.UnpackTicksLeft[v] = 0;
            QueueGuard(v);
            return;
        }
        BuildingType bt = rules.Buildings[into];
        int vmax = Math.Max(1, rules.Vehicles[_units.TypeIndex[v]].MaxHealth);   // HP ratio of the packed rig now (damage while packed counts)
        int health = (int)Math.Max(1L, (long)bt.MaxHealth * Math.Min(1000L, (long)_units.Health[v] * 1000 / vmax) / 1000);
        EntityId b = PlaceBuildingNow(owner, into, tl, health);
        if (b.IsNone) { _rigs.UnpackTicksLeft[v] = 0; QueueGuard(v); return; }   // building store full
        RemoveVehicleForDeploy(v);   // clears the rig slot; the lane's queue resumes because no rig is packed any more
    }

    /// <summary>Pack or unpack progress of an own actor in permille (0 = idle or not a rig; 1000 = done).</summary>
    internal int PackProgressOf(byte player, ActorRef actor)
    {
        int left, total;
        if (actor.Kind == ActorKind.Building)
        {
            if (!_buildings.Slots.IsAlive(actor.Id) || _buildings.Player[actor.Id.Index] != player) return 0;
            int b = actor.Id.Index;
            left = _rigs.PackTicksLeft[b];
            if (left <= 0) return 0;
            total = ScaledPackTicks(player, RulesOf(player).Buildings[_buildings.TypeIndex[b]].PackTicks);
        }
        else if (actor.Kind == ActorKind.Vehicle)
        {
            if (!_units.Slots.IsAlive(actor.Id) || _units.Player[actor.Id.Index] != player) return 0;
            int v = actor.Id.Index;
            left = _rigs.UnpackTicksLeft[v];
            if (left <= 0) return 0;
            total = ScaledPackTicks(player, RulesOf(player).Vehicles[_units.TypeIndex[v]].PackTicks);
        }
        else return 0;
        return Math.Clamp(1000 - (int)((long)left * 1000 / Math.Max(1, total)), 0, 1000);
    }
}
