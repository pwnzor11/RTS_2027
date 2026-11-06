// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Movement;
using Rts.Sim.Naval;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// M7-09 (docs/design/11_m7_plan.md §2.3 and §2.5, design 07 §5 and §6.4, D-137, D-138): the carrier hangar. A carrier is a vehicle whose type
// has aircraft_slots > 0 (and sortie_ticks > 0). Its hangar is the set of its owner's aircraft whose NavalState.HangarOf points at the
// carrier's vehicle slot; the carrier itself stores nothing. An aircraft in the hangar is in one of three states (NavalState.AirState):
//   Docked    ActorLayer.Passenger (hidden, untargetable, cannot fire, rides at the carrier's position, no mission), repairs in place.
//   OnSortie  flying a normal Attack mission on the target of the launch order; SortieLeft counts down from sortie_ticks.
//   Returning MissionKind.Sortie: flies to the carrier's current cell and docks there.
// Orders: Attack on a carrier launches every docked aircraft that can hit the target (all at once); Enter on an aircraft with the own
// carrier as target sends it to dock (a free slot is needed); an aircraft completing in a production lane whose rally point lies within two
// cells of an own carrier with a free slot is built straight into the hangar.
// A sortie ends after sortie_ticks of flight or as soon as its Attack mission has ended (target dead or hidden), then the aircraft returns.
// If the carrier dies, docked aircraft are lost with it and airborne ones become ordinary aircraft (HangarOf cleared).
// D-156 (M7-16): ammunition. A type with ammo > 0 holds that many rounds (NavalState.AmmoLeft, full at spawn); each primary-weapon shot uses one
// and at 0 the aircraft cannot attack (WeaponOf says -1). A sortie also ends when the aircraft runs dry; docked aircraft rearm
// (RearmTicks for empty to full, one round per RearmTicks/Ammo) alongside the repair. A dry aircraft with no carrier (RunAircraftAmmo) flies to the
// nearest own carrier with a free slot and takes a hangar place, else hovers over the nearest own aircraft-producing building and rearms
// there, else stays where it is without ammunition.
// ASSUMED (D-156): a launch order sends any docked aircraft with at least one round; a partly rearmed aircraft may leave. — confirm (M7-16)
// ASSUMED (D-156): the hover-rearm fallback only runs for a dry aircraft that is idle or fighting; an explicit Move order is obeyed until it ends. — confirm (M7-16)
// ASSUMED (M7-09): a sortie ends by time, by running out of rounds, or by its target going away. — confirm (M7-09)
// ASSUMED (M7-09): sortie_ticks is the flight time of one sortie (the brief), not a launch interval; launching takes no time. — confirm (M7-09)
// ASSUMED (M7-09): any air type that carries no passengers fits a hangar (a Skyhook does not); one place per aircraft. — confirm (M7-09)
// ASSUMED (M7-09): a docked aircraft regains full health in sortie_ticks ticks (max health / sortie_ticks per tick, at least 1). — confirm (M7-09)
// ASSUMED (M7-09): an aircraft with a weapon that cannot hit the target stays docked on a launch order. — confirm (M7-09)
public sealed partial class Simulation
{
    private bool IsCarrierType(VehicleType vt) => vt.AircraftSlots > 0 && vt.SortieTicks > 0;

    /// <summary>True when slot <paramref name="h"/> holds a live carrier of <paramref name="player"/> on the surface.</summary>
    private bool CarrierUsable(int h, byte player) =>
        h > 0 && h < _units.Capacity && _units.Slots.IsIndexAlive(h) && _units.Player[h] == player && _units.Health[h] > 0
        && _tunnels.VehicleLayer[h] == ActorLayer.Surface && IsCarrierType(RulesOf(player).Vehicles[_units.TypeIndex[h]]);

    /// <summary>Hangar places taken on a carrier slot (docked, flying or returning), not counting slot <paramref name="except"/>.</summary>
    private int HangarUsed(int carrier, int except = 0)
    {
        int n = 0;
        for (int j = 1; j < _units.Capacity; j++)
            if (j != except && _naval.HangarOf[j] == carrier && _naval.AirState[j] != 0 && _units.Slots.IsIndexAlive(j)) n++;
        return n;
    }

    /// <summary>Aircraft sitting in the carrier's hangar right now.</summary>
    private int HangarDocked(int carrier)
    {
        int n = 0;
        for (int j = 1; j < _units.Capacity; j++)
            if (_naval.HangarOf[j] == carrier && _naval.AirState[j] == NavalState.AirDocked && _units.Slots.IsIndexAlive(j)) n++;
        return n;
    }

    /// <summary>PlayerView: (docked, slots) of the viewer's own carrier; false when the vehicle is not an own live carrier.</summary>
    internal bool OwnCarrierHangar(byte player, EntityId carrier, out int docked, out int slots)
    {
        docked = 0; slots = 0;
        if (!_units.Slots.IsAlive(carrier) || !CarrierUsable(carrier.Index, player)) return false;
        slots = RulesOf(player).Vehicles[_units.TypeIndex[carrier.Index]].AircraftSlots;
        docked = HangarDocked(carrier.Index);
        return true;
    }

    private bool FitsHangar(VehicleType vt) => vt.Locomotion == Locomotion.Air && vt.Passengers == 0;

    // ---- per tick -----------------------------------------------------------------------------

    /// <summary>Once per tick before the missions run: dock rides, repair, sortie timers, orphans of a lost carrier.</summary>
    private void RunCarriers()
    {
        for (int i = 1; i < _units.Capacity; i++)
        {
            int h = _naval.HangarOf[i];
            if (h == 0 && _naval.AirState[i] == 0) continue;
            if (!_units.Slots.IsIndexAlive(i)) continue;
            byte owner = _units.Player[i];
            if (h > 0 && h < _units.Capacity && _units.Slots.IsIndexAlive(h) && _units.Health[h] <= 0) continue;   // dying: RunCleanup's OnCarrierDying decides
            if (h == 0 || !CarrierUsable(h, owner)) { Orphan(i); continue; }
            switch (_naval.AirState[i])
            {
                case NavalState.AirDocked: RideAndRepair(i, h); break;
                case NavalState.AirOnSortie:
                    bool flying = _units.Mission[i] == MissionKind.Attack || _units.NextMission[i] == MissionKind.Attack;
                    bool dry = RulesOf(owner).Vehicles[_units.TypeIndex[i]].Ammo > 0 && _naval.AmmoLeft[i] <= 0;   // D-156
                    if (--_naval.SortieLeft[i] <= 0 || !flying || dry) Recall(i);
                    break;
            }
        }
    }

    private void RideAndRepair(int i, int carrier)
    {
        _tunnels.VehicleLayer[i] = ActorLayer.Passenger;
        _units.Position[i] = _units.Position[carrier];
        MoveMission.DropRequest(this, _units, i);
        _units.Mission[i] = MissionKind.None; _units.NextMission[i] = MissionKind.None;
        _units.NextCell[i] = default; _units.NextTarget[i] = default; _units.Target[i] = default;
        _units.PathActive[i] = false; _units.PathCells[i] = null; _units.PathPos[i] = 0;
        _combat.At(ActorKind.Vehicle, i).AttackTarget = default;
        byte owner = _units.Player[i];
        Rearm(i);
        int max = RulesOf(owner).Vehicles[_units.TypeIndex[i]].MaxHealth;
        if (_units.Health[i] <= 0 || _units.Health[i] >= max) return;
        int ticks = Math.Max(1, RulesOf(owner).Vehicles[_units.TypeIndex[carrier]].SortieTicks);
        _units.Health[i] = Math.Min(max, _units.Health[i] + Math.Max(1, max / ticks));
    }

    /// <summary>One tick of rearming: Ammo progress per tick, one round back every RearmTicks of progress (empty to full takes RearmTicks).</summary>
    private void Rearm(int i)
    {
        VehicleType vt = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]];
        if (vt.Ammo <= 0) return;
        if (_naval.AmmoLeft[i] >= vt.Ammo) { _naval.AmmoLeft[i] = vt.Ammo; _naval.RearmProgress[i] = 0; return; }
        _naval.RearmProgress[i] += vt.Ammo;
        while (_naval.RearmProgress[i] >= vt.RearmTicks && _naval.AmmoLeft[i] < vt.Ammo) { _naval.RearmProgress[i] -= vt.RearmTicks; _naval.AmmoLeft[i]++; }
        if (_naval.AmmoLeft[i] >= vt.Ammo) _naval.RearmProgress[i] = 0;
    }

    /// <summary>Once per tick for aircraft with ammo that hold no hangar place: rearm while hovering over an own aircraft-producing building,
    /// and send a dry one to a free carrier slot or to such a building.</summary>
    private void RunAircraftAmmo()
    {
        for (int i = 1; i < _units.Capacity; i++)
        {
            if (!_units.Slots.IsIndexAlive(i) || _naval.HangarOf[i] != 0 || _naval.AirState[i] != 0 || _units.Health[i] <= 0) continue;
            byte owner = _units.Player[i];
            VehicleType vt = RulesOf(owner).Vehicles[_units.TypeIndex[i]];
            if (vt.Ammo <= 0 || vt.Locomotion != Locomotion.Air || _tunnels.VehicleLayer[i] != ActorLayer.Surface) continue;
            CellCoord cell = _units.Position[i].ToCell();
            if (OverAircraftBuilding(i, owner, cell)) { Rearm(i); continue; }
            if (_naval.AmmoLeft[i] > 0) continue;
            MissionKind m = _units.Mission[i], n = _units.NextMission[i];
            if (m == MissionKind.Move || n == MissionKind.Move || m == MissionKind.Sortie || n == MissionKind.Sortie) continue;
            int carrier = NearestFreeCarrier(owner, cell);
            if (carrier > 0)
            {
                MoveMission.DropRequest(this, _units, i);
                _naval.HangarOf[i] = carrier; _naval.AirState[i] = NavalState.AirReturning; _naval.SortieLeft[i] = 0;
                _combat.At(ActorKind.Vehicle, i).AttackTarget = default;
                Queue(_units, i, MissionKind.Sortie, default, default);
                continue;
            }
            int b = NearestAircraftBuilding(owner, cell);
            if (b <= 0) continue;
            BuildingType bt = RulesOf(owner).Buildings[_buildings.TypeIndex[b]];
            CellCoord tl = _buildings.Cell[b];
            _combat.At(ActorKind.Vehicle, i).AttackTarget = default;
            Queue(_units, i, MissionKind.Move, new CellCoord(tl.X + bt.FootprintW / 2, tl.Y + bt.FootprintH / 2), default);
        }
    }

    private bool IsAircraftBuilding(int b, byte owner) =>
        _buildings.Slots.IsIndexAlive(b) && _buildings.Player[b] == owner && _buildings.Health[b] > 0
        && RulesOf(owner).Buildings[_buildings.TypeIndex[b]].Produces == ProductionCategory.Aircraft;

    private bool OverAircraftBuilding(int i, byte owner, CellCoord cell)
    {
        for (int b = 1; b < _buildings.Capacity; b++)
        {
            if (!IsAircraftBuilding(b, owner)) continue;
            BuildingType bt = RulesOf(owner).Buildings[_buildings.TypeIndex[b]];
            CellCoord tl = _buildings.Cell[b];
            if (cell.X >= tl.X && cell.X < tl.X + bt.FootprintW && cell.Y >= tl.Y && cell.Y < tl.Y + bt.FootprintH) return true;
        }
        return false;
    }

    private int NearestAircraftBuilding(byte owner, CellCoord from)
    {
        int best = 0, bestD = int.MaxValue;
        for (int b = 1; b < _buildings.Capacity; b++)
        {
            if (!IsAircraftBuilding(b, owner)) continue;
            int d = CellDistance(from, _buildings.Cell[b]);
            if (d < bestD) { bestD = d; best = b; }
        }
        return best;
    }

    private int NearestFreeCarrier(byte owner, CellCoord from)
    {
        int best = 0, bestD = int.MaxValue;
        for (int h = 1; h < _units.Capacity; h++)
        {
            if (!CarrierUsable(h, owner) || HangarUsed(h) >= RulesOf(owner).Vehicles[_units.TypeIndex[h]].AircraftSlots) continue;
            int d = CellDistance(from, _units.Position[h].ToCell());
            if (d < bestD) { bestD = d; best = h; }
        }
        return best;
    }

    /// <summary>End a sortie: forget the target and fly home.</summary>
    private void Recall(int i)
    {
        _naval.AirState[i] = NavalState.AirReturning; _naval.SortieLeft[i] = 0;
        _combat.At(ActorKind.Vehicle, i).AttackTarget = default;
        Queue(_units, i, MissionKind.Sortie, default, default);
    }

    /// <summary>The aircraft's carrier is gone: it flies on as an ordinary aircraft.</summary>
    private void Orphan(int i)
    {
        if (_tunnels.VehicleLayer[i] == ActorLayer.Passenger) _tunnels.VehicleLayer[i] = ActorLayer.Surface;
        bool returning = _naval.AirState[i] == NavalState.AirReturning;
        _naval.HangarOf[i] = 0; _naval.AirState[i] = 0; _naval.SortieLeft[i] = 0;
        if (returning || _units.Mission[i] == MissionKind.Sortie) Queue(_units, i, MissionKind.Guard, default, default);
    }

    /// <summary>MissionKind.Sortie of an aircraft (Returning): fly to the carrier's cell and dock. Returns ticks to sleep.</summary>
    internal int RunSortieMission(int i)
    {
        int h = _naval.HangarOf[i];
        if (_naval.AirState[i] != NavalState.AirReturning || !CarrierUsable(h, _units.Player[i]) || _units.Health[i] <= 0)
        {
            if (_naval.AirState[i] != 0 && !CarrierUsable(h, _units.Player[i])) Orphan(i);
            Queue(_units, i, MissionKind.Guard, default, default);
            return 1;
        }
        CellCoord carrierCell = _units.Position[h].ToCell();
        if (CellDistance(_units.Position[i].ToCell(), carrierCell) > 0)
            return MoveMission.StepToward(this, _units, ActorKind.Vehicle, i, carrierCell);
        _naval.AirState[i] = NavalState.AirDocked; _naval.SortieLeft[i] = 0;
        _units.Mission[i] = MissionKind.None;
        RideAndRepair(i, h);
        return 1;
    }

    // ---- orders -------------------------------------------------------------------------------

    /// <summary>Attack with a carrier as subject: every docked aircraft that can hit the target takes off against it. True if any did.</summary>
    private bool TryCarrierLaunch(byte player, int carrier, ActorRef target)
    {
        if (!CarrierUsable(carrier, player) || !IsValidTarget(player, target)) return false;
        bool any = false;
        for (int j = 1; j < _units.Capacity; j++)
        {
            if (_naval.HangarOf[j] != carrier || _naval.AirState[j] != NavalState.AirDocked || !_units.Slots.IsIndexAlive(j)) continue;
            int weapon = WeaponOf(ActorKind.Vehicle, j);
            if (weapon < 0 || !WeaponCanTarget(RulesOf(player).Weapons[weapon], target.Kind, target.Id.Index)) continue;
            _tunnels.VehicleLayer[j] = ActorLayer.Surface;
            _naval.AirState[j] = NavalState.AirOnSortie;
            _naval.SortieLeft[j] = RulesOf(player).Vehicles[_units.TypeIndex[carrier]].SortieTicks;
            _combat.At(ActorKind.Vehicle, j).AttackTarget = target;
            Queue(_units, j, MissionKind.Attack, default, target.Id);
            any = true;
        }
        return any;
    }

    /// <summary>Enter by an own aircraft onto an own carrier: take a hangar place and fly there. False when the pair is not
    /// aircraft + own carrier (the command then takes its other routes).</summary>
    private bool TryApplyDock(Command c)
    {
        if ((ActorKind)(c.Arg & 0xFF) != ActorKind.Vehicle || !_units.Slots.IsAlive(c.Target)) return false;
        int j = c.Subject.Index, h = c.Target.Index;
        if (_units.Player[h] != c.Player || !IsCarrierType(RulesOf(c.Player).Vehicles[_units.TypeIndex[h]])) return false;
        VehicleType vt = RulesOf(c.Player).Vehicles[_units.TypeIndex[j]];
        if (!FitsHangar(vt) || _units.Health[j] <= 0 || !CarrierUsable(h, c.Player)) return true;
        if (_naval.HangarOf[j] == h && _naval.AirState[j] != 0)   // already has a place there: Enter recalls a flying one
        {
            if (_naval.AirState[j] == NavalState.AirOnSortie) Recall(j);
            return true;
        }
        if (HangarUsed(h, j) >= RulesOf(c.Player).Vehicles[_units.TypeIndex[h]].AircraftSlots) return true;   // hangar full
        MoveMission.DropRequest(this, _units, j);
        _naval.HangarOf[j] = h; _naval.AirState[j] = NavalState.AirReturning; _naval.SortieLeft[j] = 0;
        _combat.At(ActorKind.Vehicle, j).AttackTarget = default;
        Queue(_units, j, MissionKind.Sortie, default, default);
        return true;
    }

    // ---- production ---------------------------------------------------------------------------

    /// <summary>A finished aircraft goes straight into the hangar of the lowest-slot own carrier with a free place whose cell lies within
    /// two cells of the lane's rally point. False when there is no rally, no such carrier, or the type does not fit.</summary>
    private bool TryBuildIntoHangar(byte player, int type, CellCoord? rally)
    {
        if (rally is not CellCoord rc || !FitsHangar(RulesOf(player).Vehicles[type])) return false;
        for (int h = 1; h < _units.Capacity; h++)
        {
            if (!CarrierUsable(h, player)) continue;
            CellCoord cell = _units.Position[h].ToCell();
            if (CellDistance(cell, rc) > 2 || HangarUsed(h) >= RulesOf(player).Vehicles[_units.TypeIndex[h]].AircraftSlots) continue;
            EntityId id = SpawnVehicle(player, type, cell);
            if (id.IsNone) return false;
            OnStatsBuilt(player, ActorKind.Vehicle);
            _naval.HangarOf[id.Index] = h; _naval.AirState[id.Index] = NavalState.AirDocked;
            RideAndRepair(id.Index, h);
            return true;
        }
        return false;
    }

    // ---- death --------------------------------------------------------------------------------

    /// <summary>A carrier is about to be removed (health &lt;= 0 in RunCleanup): docked aircraft are lost with it, the others are freed.</summary>
    private void OnCarrierDying(int carrier)
    {
        for (int j = 1; j < _units.Capacity; j++)
        {
            if (_naval.HangarOf[j] != carrier || !_units.Slots.IsIndexAlive(j)) continue;
            if (_naval.AirState[j] == NavalState.AirDocked)
            {
                OnStatsLost(_units.Player[j], ActorKind.Vehicle);
                RemoveMobileQuietly(ActorKind.Vehicle, j);
            }
            else Orphan(j);
        }
    }
}
