// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Movement;
using Rts.Sim.Numerics;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using Rts.Sim.Tunnels;
using Rts.Sim.World;

namespace Rts.Sim;

// Free burrowing for Locomotion.Burrow vehicles (design 05 §1, D-074). OWNER: M3-03.
//   Surface --(soft cell, no structure within 1)--> Diving (DiveTicks, visible) --> Burrowed (travels under open ground, claims
//   no cells, cannot fire) --(arrival: nearest valid cell)--> Surfacing (unit telegraph, TelegraphTicks) --> Surface.
// A Bore ordered to Deploy travels the same way, then breaks through into a Hatch (entrance telegraph) instead of surfacing.
// Per-unit state lives in existing fields so no part changes: VehicleLayer, Mission (Burrow/Deploy/Surface), Destination (goal
// lepton), BlockedTicks (dive counter while Diving), PathId/PathCells/PathPos (burrowed route), Telegraphs (surfacing).
public sealed partial class Simulation
{
    // ---- command hook (called from ApplyMovementCommand) ----------------------------------------------

    /// <summary>Move for a Locomotion.Burrow vehicle becomes a Burrow mission (dive, travel, surface). Returns true when the
    /// command was consumed (also when it is ignored); false when the subject is not an own burrower, so the normal path runs.</summary>
    internal bool TryBurrowMove(Command c)
    {
        if (c.Arg != (int)ActorKind.Vehicle || !_units.Slots.IsAlive(c.Subject) || _units.Player[c.Subject.Index] != c.Player) return false;
        int i = c.Subject.Index;
        if (RulesOf(c.Player).Vehicles[_units.TypeIndex[i]].Locomotion != Locomotion.Burrow) return false;
        ActorLayer layer = _tunnels.VehicleLayer[i];
        // A surfacing unit is committed; one in a network, a queue or a transport has other orders to follow.
        if (layer is not (ActorLayer.Surface or ActorLayer.Diving or ActorLayer.Burrowed) || !InMap(c.Cell)) return true;
        MoveMission.DropRequest(this, _units, i);
        if (layer != ActorLayer.Surface) ClearBurrowPath(i);
        _units.Destination[i] = LeptonCoord.CenterOf(c.Cell);
        Queue(_units, i, MissionKind.Burrow, c.Cell, default);
        return true;
    }

    // ---- mission bodies (called from Missions/BurrowMissions.cs) --------------------------------------

    /// <summary>One step of a Burrow (<paramref name="deploy"/> false) or Deploy mission of vehicle slot <paramref name="i"/>.</summary>
    internal int RunBurrowMission(int i, bool deploy)
    {
        if (_units.Health[i] <= 0) return 1;
        VehicleType vt = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]];
        switch (_tunnels.VehicleLayer[i])
        {
            case ActorLayer.Surface: return StartDive(i, vt, deploy);
            case ActorLayer.Diving: return StepDive(i);
            case ActorLayer.Burrowed: return StepBurrowed(i, vt, deploy);
            case ActorLayer.Surfacing: return 1;   // the telegraph counts down in RunBurrowing
            default: return 10;                    // in a queue, network or transport: not ours
        }
    }

    /// <summary>One step of a Surface mission: surface where the unit is (burrowed), cancel a dive, or wait for the telegraph.</summary>
    internal int RunSurfaceMission(int i)
    {
        switch (_tunnels.VehicleLayer[i])
        {
            case ActorLayer.Surfacing: return 1;
            case ActorLayer.Burrowed:
                return BeginSurfacing(i, _units.Position[i].ToCell()) ? 1 : SurfaceRetryTicks;
            case ActorLayer.Diving:
                _tunnels.VehicleLayer[i] = ActorLayer.Surface; ClearBurrowPath(i); QueueGuard(i); return 1;
            case ActorLayer.Surface:
                QueueGuard(i); return 1;
            default: return 10;
        }
    }

    // ASSUMED (M3-03): when no valid surfacing cell exists in range the unit stays burrowed and looks again every second.
    private const int SurfaceRetryTicks = 20;

    private void QueueGuard(int i) => Queue(_units, i, MissionKind.Guard, default, default);

    private void ClearBurrowPath(int i)
    {
        MoveMission.DropRequest(this, _units, i);
        _units.PathActive[i] = false;
        _units.PathCells[i] = null;
        _units.PathPos[i] = 0;
        _units.BlockedTicks[i] = 0;
        _units.StuckCount[i] = 0;
    }

    // ---- cell rules -----------------------------------------------------------------------------------

    /// <summary>True if a structure (building footprint cell or wall overlay) is within <paramref name="ring"/> cells (Chebyshev) of <paramref name="c"/>.</summary>
    private bool StructureNear(CellCoord c, int ring)
    {
        for (int y = c.Y - ring; y <= c.Y + ring; y++)
            for (int x = c.X - ring; x <= c.X + ring; x++)
            {
                var n = new CellCoord(x, y);
                if (!InMap(n)) continue;
                if (!_map.BuildingAt(n).IsNone || _map.OverlayAt(n) == OverlayKind.Wall) return true;
            }
        return false;
    }

    /// <summary>Soft land and no structure within SurfacingRing cells: where a burrower may dive (and the base of surfacing).</summary>
    private bool DiveCellOk(CellCoord c) =>
        InMap(c) && Setup.Rules.Terrain.Soft(_map.LandAt(c)) && !StructureNear(c, Setup.Rules.Tunnels.SurfacingRing);

    private bool SurfaceAreaFree(CellCoord c, EntityId self)
    {
        EntityId v = _map.VehicleAt(c);
        if (!v.IsNone && v != self) return false;
        for (int s = 0; s < InfantryStore.SubSlotCount; s++)
            if (!_map.InfantryAt(c, s).IsNone) return false;
        List<Telegraph> list = _tunnels.Telegraphs;
        for (int t = 0; t < list.Count; t++)
            if (list[t].Cell == c && list[t].Kind == TelegraphKind.Unit && list[t].Subject.Id != self) return false;
        return true;
    }

    /// <summary>A unit may appear on <paramref name="c"/>: dive-valid, unhardened, passable for burrowers and free of other actors and telegraphs.</summary>
    private bool SurfaceCellOk(CellCoord c, EntityId self, byte owner) =>
        DiveCellOk(c) && !IsHardenedAgainst(owner, c)   // integration (M3-04): only an unfriendly team's grouting blocks
        && !IsFoamed(c)                   // M4-05: Quick-Set Foam blocks surfacing and Bore break-through (hatch sites use this too)
        && _map.IsPassableTerrain(c, Locomotion.Burrow, Setup.Rules.Terrain)
        && SurfaceAreaFree(c, self);

    // ---- surface -> dive ------------------------------------------------------------------------------

    private int StartDive(int i, VehicleType vt, bool deploy)
    {
        if (vt.Locomotion != Locomotion.Burrow)
        {
            // Not a burrower: a Deploy mission deploys in place, a Burrow mission means nothing.
            if (deploy) TryDeployInPlace(i);
            QueueGuard(i);
            return 1;
        }
        if (_units.Status[i] == MissionStatus.Init) { ClearBurrowPath(i); _units.Status[i] = MissionStatus.Waiting; }
        CellCoord cur = Occupancy.CurCell(ActorKind.Vehicle, _units, i);
        // finish a step already started before judging the cell the unit stands in
        if (_units.NextCellReserved[i]) return MoveMission.StepToward(this, _units, ActorKind.Vehicle, i, cur);
        if (DiveCellOk(cur))
        {
            _units.Position[i] = LeptonCoord.CenterOf(cur);
            _tunnels.VehicleLayer[i] = ActorLayer.Diving;
            ClearBurrowPath(i);
            return 1;
        }
        // Hard ground or next to a structure: walk (on the surface) to the nearest cell where diving is allowed.
        CellCoord walk;
        if (_units.PathActive[i] && DiveCellOk(_units.PathGoal[i])) walk = _units.PathGoal[i];
        else if (!FindDiveCell(cur, i, out walk)) { QueueGuard(i); return 1; }   // nowhere to dive: give up the order
        return MoveMission.StepToward(this, _units, ActorKind.Vehicle, i, walk);
    }

    private bool FindDiveCell(CellCoord from, int i, out CellCoord found)
    {
        EntityId self = _units.Slots.IdOf(i);
        Locomotion loco = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].Locomotion;
        for (int n = 0; Spiral.Cell(from, n, out CellCoord c); n++)
            if (DiveCellOk(c) && Occupancy.FreeForVehicle(this, c, loco, self)) { found = c; return true; }
        found = from;
        return false;
    }

    private int StepDive(int i)
    {
        if (++_units.BlockedTicks[i] < Setup.Rules.Tunnels.DiveTicks) return 1;
        _tunnels.VehicleLayer[i] = ActorLayer.Burrowed;
        Occupancy.Release(this, ActorKind.Vehicle, _units, i);   // underground: holds no cell, cannot block or be crushed
        ClearBurrowPath(i);
        return 1;
    }

    // ---- burrowed travel ------------------------------------------------------------------------------

    private int StepBurrowed(int i, VehicleType vt, bool deploy)
    {
        CellCoord goal = _units.Destination[i].ToCell();
        CellCoord cur = _units.Position[i].ToCell();
        if (_units.PathCells[i] == null)
        {
            if (_units.PathId[i] > 0)
            {
                PathStatus st = _paths.Poll(_units.PathId[i], 0, out IReadOnlyList<CellCoord> path);
                if (st == PathStatus.Pending) return 1;
                _units.PathId[i] = 0;
                if ((st == PathStatus.Found || st == PathStatus.Partial) && path.Count > 0)
                {
                    var cells = new CellCoord[path.Count];
                    for (int k = 0; k < cells.Length; k++) cells[k] = path[k];
                    _units.PathCells[i] = cells;
                    _units.PathPos[i] = 0;
                }
                else return BurrowArrived(i, deploy);   // no route: surface here
            }
            else if (cur == goal) return BurrowArrived(i, deploy);
            else
            {
                _units.PathId[i] = _paths.Request(cur, goal, Locomotion.Burrow);
                _units.PathIsGroup[i] = false; _units.PathIndex[i] = 0;
                return 1;
            }
        }
        CellCoord[] route = _units.PathCells[i]!;
        if (_units.PathPos[i] >= route.Length) return BurrowArrived(i, deploy);
        CellCoord next = route[_units.PathPos[i]];
        LeptonCoord target = LeptonCoord.CenterOf(next);
        int pct = _map.SpeedPercent(next, Locomotion.Burrow, Setup.Rules.Terrain);
        long speed = Math.Max(1L, (long)vt.Speed * pct / 100);
        LeptonCoord p = _units.Position[i];
        long dx = target.X - p.X, dy = target.Y - p.Y;
        if (dx != 0 || dy != 0) _units.Facing[i] = Facing.FromDelta((int)dx, (int)dy);
        if (dx * dx + dy * dy <= speed * speed) { _units.Position[i] = target; _units.PathPos[i]++; }
        else
        {
            long d = LeptonMath.Distance(p, target);
            // rounded, so speed 1 still advances along the major axis
            long sx = (dx * speed + (dx >= 0 ? d / 2 : -(d / 2))) / d, sy = (dy * speed + (dy >= 0 ? d / 2 : -(d / 2))) / d;
            _units.Position[i] = new LeptonCoord(p.X + (int)sx, p.Y + (int)sy);
        }
        return 1;
    }

    private int BurrowArrived(int i, bool deploy)
    {
        CellCoord cur = _units.Position[i].ToCell();
        ClearBurrowPath(i);
        _units.Destination[i] = _units.Position[i];   // later retries look around here, not at the old goal
        if (deploy)
        {
            if (BeginBreakThrough(i, cur)) return 1;
            if (IsFoamed(cur)) return SurfaceRetryTicks;   // M4-05: Quick-Set Foam overhead: stay down, still a Deploy, and look again
            // No place for a Hatch: surface as a unit instead (and keep retrying that).
            _units.Mission[i] = MissionKind.Surface; _units.Status[i] = MissionStatus.Init;
        }
        return BeginSurfacing(i, cur) ? 1 : SurfaceRetryTicks;
    }

    // ---- burrowed -> surfacing -> surface -------------------------------------------------------------

    private bool BeginSurfacing(int i, CellCoord from)
    {
        EntityId self = _units.Slots.IdOf(i);
        CellCoord cell = default;
        bool found = false;
        for (int n = 0; Spiral.Cell(from, n, out CellCoord c); n++)
            if (SurfaceCellOk(c, self, _units.Player[i])) { cell = c; found = true; break; }
        if (!found) return false;
        _units.Position[i] = LeptonCoord.CenterOf(cell);   // still underground: the hop to the nearest valid cell is unseen
        _units.Destination[i] = _units.Position[i];
        _tunnels.VehicleLayer[i] = ActorLayer.Surfacing;
        _units.Mission[i] = MissionKind.Surface; _units.Status[i] = MissionStatus.Waiting;
        ClearBurrowPath(i);
        _tunnels.Telegraphs.Add(new Telegraph
        {
            Cell = cell, Owner = _units.Player[i], TicksLeft = Setup.Rules.Tunnels.TelegraphTicks + 1,
            Kind = TelegraphKind.Unit, Subject = new ActorRef(ActorKind.Vehicle, _units.Slots.IdOf(i)),
        });
        return true;
    }

    private void FinishSurfacing(int i, CellCoord cell)
    {
        if (SurfaceCellOk(cell, _units.Slots.IdOf(i), _units.Player[i]))
        {
            _tunnels.VehicleLayer[i] = ActorLayer.Surface;
            _units.Position[i] = LeptonCoord.CenterOf(cell);
            Occupancy.Claim(this, ActorKind.Vehicle, _units, i);
            QueueGuard(i);
            return;
        }
        // The cell was taken, built next to or hardened during the telegraph: back underground, look again (new telegraph).
        _tunnels.VehicleLayer[i] = ActorLayer.Burrowed;
        _units.Mission[i] = MissionKind.Surface; _units.Status[i] = MissionStatus.Init; _units.WakeTick[i] = 0;
    }

    // ---- Bore break-through ---------------------------------------------------------------------------

    private bool BeginBreakThrough(int i, CellCoord from)
    {
        VehicleType vt = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]];
        BuildingType bt = RulesOf(_units.Player[i]).Buildings[vt.DeploysInto];
        CellCoord topLeft = default;
        bool found = false;
        for (int n = 0; Spiral.Cell(from, n, out CellCoord c); n++)
        {
            var tl = new CellCoord(c.X - (bt.FootprintW - 1) / 2, c.Y - (bt.FootprintH - 1) / 2);
            if (HatchSiteOk(tl, bt, _units.Player[i])) { topLeft = tl; found = true; break; }
        }
        if (!found) return false;
        byte owner = _units.Player[i];
        RemoveVehicleForDeploy(i);
        int health = (int)Math.Max(1L, (long)bt.MaxHealth * Setup.Rules.Tunnels.EmergingHealthPermille / 1000);
        EntityId id = PlaceBuildingNow(owner, vt.DeploysInto, topLeft, health);
        if (id.IsNone) return true;   // building store full: the Bore is lost, nothing to telegraph
        _tunnels.Telegraphs.Add(new Telegraph
        {
            Cell = topLeft, Owner = owner, TicksLeft = Setup.Rules.Tunnels.EntranceTelegraphTicks + 1,
            Kind = TelegraphKind.Entrance, Subject = new ActorRef(ActorKind.Building, id),
        });
        return true;
    }

    // ASSUMED (M3-03): a new entrance obeys the same rules as a surfacing unit: every footprint cell soft, unhardened, empty, not
    // under another unit telegraph, and the footprint more than SurfacingRing cells from any other structure.
    private bool HatchSiteOk(CellCoord topLeft, BuildingType bt, byte owner)
    {
        for (int y = topLeft.Y; y < topLeft.Y + bt.FootprintH; y++)
            for (int x = topLeft.X; x < topLeft.X + bt.FootprintW; x++)
            {
                var c = new CellCoord(x, y);
                if (!InMap(c) || !SurfaceCellOk(c, EntityId.None, owner)) return false;
            }
        return true;
    }

    // ---- vehicle -> building --------------------------------------------------------------------------

    /// <summary>Remove a live vehicle that turns into a building (not a death: no death weapon, no fx).</summary>
    private void RemoveVehicleForDeploy(int i)
    {
        EntityId id = _units.Slots.IdOf(i);
        OnActorRemoving(ActorKind.Vehicle, i);
        OnM3MobileRemoving(ActorKind.Vehicle, i);
        _units.Remove(id);
    }

    /// <summary>Place a building inside Step without the CanPlace checks (callers did their own) and with the given health.</summary>
    private EntityId PlaceBuildingNow(byte player, int type, CellCoord topLeft, int health)
    {
        BuildingType bt = RulesOf(player).Buildings[type];
        EntityId id = _buildings.Add(player, type, topLeft, health);
        if (id.IsNone) return id;
        for (int y = 0; y < bt.FootprintH; y++)
            for (int x = 0; x < bt.FootprintW; x++)
                _map.SetBuilding(new CellCoord(topLeft.X + x, topLeft.Y + y), id);
        _production.RepairGen[id.Index] = 0; _production.RepairFrac[id.Index] = 0;
        OnM3BuildingPlaced(id.Index);
        _emittersDirty = true;   // M3-INT
        return id;
    }

    /// <summary>Land-train deploy: the vehicle becomes DeploysInto on the spot (footprint centred on its cell). Refused (no change)
    /// when the footprint is off the map, on unbuildable land, holds a building or any other actor.</summary>
    internal bool TryDeployInPlace(int i)
    {
        VehicleType vt = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]];
        if (vt.DeploysInto < 0 || vt.DeploysInto >= RulesOf(_units.Player[i]).Buildings.Count || _tunnels.VehicleLayer[i] != ActorLayer.Surface) return false;
        BuildingType bt = RulesOf(_units.Player[i]).Buildings[vt.DeploysInto];
        CellCoord at = _units.Position[i].ToCell();
        EntityId self = _units.Slots.IdOf(i);
        CellCoord topLeft;
        if (vt.Locomotion == Locomotion.Naval)
        {
            // M7-10 (D-149): a ship sits on water, which is not buildable, so the footprint cannot be centred on it. It goes ashore instead:
            // the first top-left (rows, then columns) whose footprint is buildable and touches the ship's cell (within one cell of it).
            bool found = false;
            topLeft = default;
            for (int ty = at.Y - bt.FootprintH; ty <= at.Y + 1 && !found; ty++)
                for (int tx = at.X - bt.FootprintW; tx <= at.X + 1 && !found; tx++)
                    if (DeployFootprintFree(new CellCoord(tx, ty), bt, self)) { topLeft = new CellCoord(tx, ty); found = true; }
            if (!found) return false;
        }
        else
        {
            topLeft = new CellCoord(at.X - (bt.FootprintW - 1) / 2, at.Y - (bt.FootprintH - 1) / 2);
            if (!DeployFootprintFree(topLeft, bt, self)) return false;
        }
        byte owner = _units.Player[i];
        // ASSUMED (M3-03): the building keeps the vehicle's health fraction.
        int health = (int)Math.Max(1L, (long)bt.MaxHealth * _units.Health[i] / Math.Max(1, vt.MaxHealth));
        RemoveVehicleForDeploy(i);
        PlaceBuildingNow(owner, vt.DeploysInto, topLeft, health);
        return true;
    }

    /// <summary>True when every footprint cell is on the map, buildable, free of overlay and buildings, and holds no actor other than <paramref name="self"/>.</summary>
    private bool DeployFootprintFree(CellCoord topLeft, BuildingType bt, EntityId self)
    {
        for (int y = topLeft.Y; y < topLeft.Y + bt.FootprintH; y++)
            for (int x = topLeft.X; x < topLeft.X + bt.FootprintW; x++)
            {
                var c = new CellCoord(x, y);
                if (!InMap(c) || !Setup.Rules.Terrain.Buildable(_map.LandAt(c)) || _map.OverlayAt(c) != OverlayKind.None || !_map.BuildingAt(c).IsNone) return false;
                EntityId v = _map.VehicleAt(c);
                if (!v.IsNone && v != self) return false;
                for (int s = 0; s < InfantryStore.SubSlotCount; s++)
                    if (!_map.InfantryAt(c, s).IsNone) return false;
            }
        return true;
    }
}
