// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Movement;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// Transports and engineers (design 05 §4). OWNER: M3-06. State: Tunnels.InfantryLayer (Passenger), Tunnels.InfantryCarrier.
// Command routing (ApplyEnter / ApplyDeploy) lives in Simulation.M3.cs.
// ASSUMED (M3-06): boarding is instant when the soldier is within 1 cell of the transport; otherwise the soldier walks there under the
// Capture mission (that mission also drives non-engineers to a transport) and boards on arrival.
// ASSUMED (M3-06): an engineer captures an enemy building only below CaptureBelowPermille of its health; a neutral building at any
// health; it repairs an allied/own building that is damaged. A command that cannot act is ignored and the soldier is not consumed.
// ASSUMED (M3-06): lost passengers vanish without a death weapon or a destruction effect.
public sealed partial class Simulation
{
    /// <summary>Enemy buildings at or above this health (per mille of max) cannot be captured (design 05 §4: 50 %, tune).</summary>
    internal const int CaptureBelowPermille = 500;

    // ---- queries ------------------------------------------------------------------------------

    private int PassengerCount(EntityId transport)
    {
        int n = 0;
        for (int i = 1; i < _infantry.Capacity; i++)
            if (_infantry.Slots.IsIndexAlive(i) && _tunnels.InfantryLayer[i] == ActorLayer.Passenger && _tunnels.InfantryCarrier[i] == transport) n++;
        return n;
    }

    /// <summary>Passengers currently inside the transport (vehicle id).</summary>
    public int PassengersIn(EntityId transport) => _units.Slots.IsAlive(transport) ? PassengerCount(transport) : 0;

    private static int CellDistance(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    // ---- boarding -----------------------------------------------------------------------------

    /// <summary>Enter with Target = own/allied vehicle: board it if it has room (VehicleType.Passengers).</summary>
    private void ApplyBoard(Command c, ActorRef unit)
    {
        if (unit.Kind != ActorKind.Infantry || !CanBoard(unit.Id.Index, c.Target)) return;
        int i = unit.Id.Index;
        if (CellDistance(_infantry.SlotCell[i], _units.Position[c.Target.Index].ToCell()) <= 1) BoardNow(i, c.Target);
        else Queue(_infantry, i, MissionKind.Capture, default, c.Target);
    }

    private bool CanBoard(int infantry, EntityId transport)
    {
        if (!_infantry.Slots.IsIndexAlive(infantry) || _infantry.Health[infantry] <= 0 || _tunnels.InfantryLayer[infantry] != ActorLayer.Surface) return false;
        if (!_units.Slots.IsAlive(transport) || _units.Health[transport.Index] <= 0 || _tunnels.VehicleLayer[transport.Index] != ActorLayer.Surface) return false;
        if (!IsFriendly(_infantry.Player[infantry], _units.Player[transport.Index])) return false;
        int cap = RulesOf(_units.Player[transport.Index]).Vehicles[_units.TypeIndex[transport.Index]].Passengers;
        return cap > 0 && PassengerCount(transport) < cap;
    }

    internal bool BoardNow(int infantry, EntityId transport)
    {
        if (!CanBoard(infantry, transport)) return false;
        Occupancy.Release(this, ActorKind.Infantry, _infantry, infantry);
        MoveMission.DropRequest(this, _infantry, infantry);
        _tunnels.InfantryLayer[infantry] = ActorLayer.Passenger;
        _tunnels.InfantryCarrier[infantry] = transport;
        StopPassenger(infantry);
        _infantry.Position[infantry] = _units.Position[transport.Index];
        return true;
    }

    private void StopPassenger(int i)
    {
        _infantry.Mission[i] = MissionKind.None; _infantry.NextMission[i] = MissionKind.None;
        _infantry.NextCell[i] = default; _infantry.NextTarget[i] = default; _infantry.Target[i] = default;
        _infantry.PathActive[i] = false; _infantry.PathCells[i] = null; _infantry.PathPos[i] = 0;
        _combat.At(ActorKind.Infantry, i).AttackTarget = default;
    }

    /// <summary>Every tick: passengers ride at their transport's position, hold no cell, run no mission. A passenger whose transport is
    /// gone (should not happen: <see cref="OnTransportDying"/> removes them) is dropped on the surface where it is.</summary>
    private void SyncPassengers()
    {
        for (int i = 1; i < _infantry.Capacity; i++)
        {
            if (!_infantry.Slots.IsIndexAlive(i) || _tunnels.InfantryLayer[i] != ActorLayer.Passenger) continue;
            EntityId carrier = _tunnels.InfantryCarrier[i];
            if (!_units.Slots.IsAlive(carrier)) { _tunnels.InfantryLayer[i] = ActorLayer.Surface; _tunnels.InfantryCarrier[i] = default; continue; }
            Occupancy.Release(this, ActorKind.Infantry, _infantry, i);   // a stray order may have reserved a cell
            StopPassenger(i);
            _infantry.Position[i] = _units.Position[carrier.Index];
        }
    }

    // ---- unloading ----------------------------------------------------------------------------

    /// <summary>Deploy on a transport (Passengers &gt; 0): unload everyone.</summary>
    private void ApplyUnload(Command c)
    {
        int v = c.Subject.Index;
        if (_tunnels.VehicleLayer[v] != ActorLayer.Surface || _units.Health[v] <= 0) return;
        CellCoord at = _units.Position[v].ToCell();
        for (int i = 1; i < _infantry.Capacity; i++)
        {
            if (!_infantry.Slots.IsIndexAlive(i) || _tunnels.InfantryLayer[i] != ActorLayer.Passenger || _tunnels.InfantryCarrier[i] != c.Subject) continue;
            if (!FindUnloadCell(at, out CellCoord cell, out int slot)) return;   // no room: the rest stay aboard
            _tunnels.InfantryLayer[i] = ActorLayer.Surface;
            _tunnels.InfantryCarrier[i] = default;
            _infantry.SubSlot[i] = (byte)slot;
            _infantry.SlotCell[i] = cell;
            _infantry.Position[i] = InfantryStore.SlotPosition(cell, slot);
            _map.SetInfantry(cell, slot, _infantry.Slots.IdOf(i));
            Queue(_infantry, i, MissionKind.Guard, default, default);
        }
    }

    /// <summary>First free cell around the transport: ring 1 then ring 2, N..NW order; a cell with no soldier is preferred over one that
    /// only has a free sub-slot.</summary>
    private bool FindUnloadCell(CellCoord at, out CellCoord cell, out int slot)
    {
        for (int pass = 0; pass < 2; pass++)
            for (int r = 1; r <= 2; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        var cc = new CellCoord(at.X + dx, at.Y + dy);
                        if (!InMap(cc) || !Occupancy.FreeForInfantry(this, cc)) continue;
                        int free = _map.FreeInfantrySlot(cc);
                        if (pass == 0 && free != 0) continue;   // pass 0: completely empty cells only
                        cell = cc; slot = free;
                        return true;
                    }
        cell = default; slot = 0;
        return false;
    }

    /// <summary>A vehicle is dying: its passengers are lost.</summary>
    private void OnTransportDying(int vehicleIndex)
    {
        EntityId transport = _units.Slots.IdOf(vehicleIndex);
        for (int i = 1; i < _infantry.Capacity; i++)
        {
            if (!_infantry.Slots.IsIndexAlive(i) || _tunnels.InfantryLayer[i] != ActorLayer.Passenger || _tunnels.InfantryCarrier[i] != transport) continue;
            RemoveMobileQuietly(ActorKind.Infantry, i);
        }
    }

    /// <summary>Free a vehicle/infantry slot outside RunCleanup's death path (no death weapon, no destruction effect).</summary>
    private void RemoveMobileQuietly(ActorKind kind, int i)
    {
        EntityId id = IdAt(kind, i);
        OnActorRemoving(kind, i);
        OnM3MobileRemoving(kind, i);
        StoreOf(kind)!.Remove(id);
    }

    // ---- engineers ----------------------------------------------------------------------------

    private bool IsEngineer(int infantry) => RulesOf(_infantry.Player[infantry]).Infantry[_infantry.TypeIndex[infantry]].Engineer;

    /// <summary>What an engineer of <paramref name="player"/> could do to the building: capture, repair or nothing.</summary>
    internal enum EngineerAction : byte { None, Capture, Repair }

    internal EngineerAction EngineerActionOn(byte player, int building)
    {
        if (!_buildings.Slots.IsIndexAlive(building) || _buildings.Health[building] <= 0) return EngineerAction.None;
        int max = BuildingTypeAt(building).MaxHealth;
        byte owner = _buildings.Player[building];
        if (IsFriendly(player, owner)) return _buildings.Health[building] < max ? EngineerAction.Repair : EngineerAction.None;
        if (RelationOf(player, owner) == Relation.Neutral) return EngineerAction.Capture;
        return (long)_buildings.Health[building] * 1000 < (long)max * CaptureBelowPermille ? EngineerAction.Capture : EngineerAction.None;
    }

    /// <summary>Enter with Target = a building that is not an own entrance: engineer capture (enemy, below half health) or repair
    /// (friendly). Ignore unless the unit is an Engineer.</summary>
    private void ApplyEngineer(Command c, ActorRef unit)
    {
        if (unit.Kind != ActorKind.Infantry || !_infantry.Slots.IsIndexAlive(unit.Id.Index) || !IsEngineer(unit.Id.Index)) return;
        if (_tunnels.InfantryLayer[unit.Id.Index] != ActorLayer.Surface) return;
        if (!_buildings.Slots.IsAlive(c.Target) || EngineerActionOn(c.Player, c.Target.Index) == EngineerAction.None) return;
        Queue(_infantry, unit.Id.Index, MissionKind.Capture, default, c.Target);
    }

    /// <summary>The engineer at slot <paramref name="i"/> enters the building now: capture or repair, and the soldier is consumed.
    /// False (nothing happens) when the action no longer applies.</summary>
    internal bool EngineerEnter(int i, EntityId building)
    {
        if (!_buildings.Slots.IsAlive(building)) return false;
        byte player = _infantry.Player[i];
        int b = building.Index;
        EngineerAction action = EngineerActionOn(player, b);
        if (action == EngineerAction.None) return false;
        if (action == EngineerAction.Repair) _buildings.Health[b] = BuildingTypeAt(b).MaxHealth;
        else CaptureBuilding(b, player);
        RemoveMobileQuietly(ActorKind.Infantry, i);
        return true;
    }

    private void CaptureBuilding(int b, byte newOwner)
    {
        _buildings.Player[b] = newOwner;
        _emittersDirty = true;   // M3-INT
        _combat.At(ActorKind.Building, b) = new Combat.CombatEntry { Generation = _buildings.Slots.GenerationOf(b) };
        _production.RepairGen[b] = 0; _production.RepairFrac[b] = 0;
        for (int p = 0; p < _players.Count; p++) _vision.ForgetBuilding((byte)p, b);   // no stale ghost of the old owner's building
        _ground.HardeningDirty = true;   // team membership of a seal piece changed
        if (BuildingTypeAt(b).Gate) _map.Touch();   // integration (M3-04): who may pass the gate changed, so cached paths are stale
    }
}
