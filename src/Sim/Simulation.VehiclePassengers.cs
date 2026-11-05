// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Movement;
using Rts.Sim.Numerics;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// M7-04b (design 07 §6.3): vehicle passengers. A transport with vehicle_slots > 0 carries that many own/allied ground vehicles beside its
// soldiers. State: Tunnels.VehicleLayer = Passenger and Tunnels.VehicleCarrier = the transport (no other fields); the passenger layer,
// SyncPassengers and OnTransportDying of Simulation.Transport.cs are reused, so supply (every alive slot counts) needs no change.
// ASSUMED (M7-04b): ships and aircraft cannot be carried; a packed rig (a vehicle) can; a vehicle that has vehicle_slots itself cannot
// (no nesting). — confirm
// ASSUMED (M7-04b): a vehicle leaves a ship only onto a cell the terrain table marks `landing` (the Coast) that is free and passable for
// its move class; a non-ship carrier (Skyhook, ground transport) may set it on any free passable cell next to it. — confirm
public sealed partial class Simulation
{
    private int VehiclesAboard(EntityId transport)
    {
        int n = 0;
        for (int v = 1; v < _units.Capacity; v++)
            if (_units.Slots.IsIndexAlive(v) && _tunnels.VehicleLayer[v] == ActorLayer.Passenger && _tunnels.VehicleCarrier[v] == transport) n++;
        return n;
    }

    /// <summary>Own or allied ground vehicle <paramref name="v"/> could board <paramref name="transport"/> now (ignoring distance).</summary>
    private bool CanBoardVehicle(int v, EntityId transport)
    {
        if (!_units.Slots.IsIndexAlive(v) || _units.Health[v] <= 0 || _tunnels.VehicleLayer[v] != ActorLayer.Surface) return false;
        if (!_units.Slots.IsAlive(transport) || transport.Index == v || _units.Health[transport.Index] <= 0
            || _tunnels.VehicleLayer[transport.Index] != ActorLayer.Surface) return false;
        if (!IsFriendly(_units.Player[v], _units.Player[transport.Index])) return false;
        VehicleType vt = RulesOf(_units.Player[v]).Vehicles[_units.TypeIndex[v]];
        if (vt.Locomotion == Locomotion.Air || vt.VehicleSlots > 0 || MoveClass.IsNaval(MoveClass.Of(vt))) return false;
        int slots = RulesOf(_units.Player[transport.Index]).Vehicles[_units.TypeIndex[transport.Index]].VehicleSlots;
        return slots > 0 && VehiclesAboard(transport) < slots;
    }

    /// <summary>Enter by a vehicle onto a transport: board at once within 1 cell, else drive there (Capture mission) and board on arrival.</summary>
    private void ApplyBoardVehicle(Command c, ActorRef unit)
    {
        int v = unit.Id.Index;
        if (!CanBoardVehicle(v, c.Target)) return;
        if (CellDistance(_units.Position[v].ToCell(), _units.Position[c.Target.Index].ToCell()) <= 1) BoardVehicleNow(v, c.Target);
        else Queue(_units, v, MissionKind.Capture, default, c.Target);
    }

    /// <summary>The Capture mission of a vehicle (boarding walk): one step. Returns ticks to sleep.</summary>
    internal int RunVehicleBoard(int v)
    {
        EntityId target = _units.Target[v];
        if (!_units.Slots.IsAlive(target)) return EndBoardWalk(v);
        CellCoord tc = _units.Position[target.Index].ToCell();
        if (CellDistance(_units.Position[v].ToCell(), tc) <= 1)
        {
            if (!BoardVehicleNow(v, target)) EndBoardWalk(v);
            return 1;
        }
        return MoveMission.StepToward(this, _units, ActorKind.Vehicle, v, tc);
    }

    private int EndBoardWalk(int v) { Queue(_units, v, MissionKind.Guard, default, default); return 1; }

    internal bool BoardVehicleNow(int v, EntityId transport)
    {
        if (!CanBoardVehicle(v, transport)) return false;
        Occupancy.Release(this, ActorKind.Vehicle, _units, v);
        MoveMission.DropRequest(this, _units, v);
        _tunnels.VehicleLayer[v] = ActorLayer.Passenger;
        _tunnels.VehicleCarrier[v] = transport;
        ParkVehiclePassenger(v, transport);
        return true;
    }

    private void ParkVehiclePassenger(int v, EntityId transport)
    {
        _units.Mission[v] = MissionKind.Sleep; _units.NextMission[v] = MissionKind.None;   // Sleep, not None: an idle harvester must not restart
        _units.NextCell[v] = default; _units.NextTarget[v] = default; _units.Target[v] = default;
        _units.PathActive[v] = false; _units.PathCells[v] = null; _units.PathPos[v] = 0;
        _units.BlockedTicks[v] = 0; _units.StuckCount[v] = 0;
        _combat.At(ActorKind.Vehicle, v).AttackTarget = default;
        _units.Position[v] = _units.Position[transport.Index];
        _units.Destination[v] = _units.Position[v];
    }

    /// <summary>Every tick (from SyncPassengers): vehicle passengers ride at their transport's position and hold no cell.</summary>
    private void SyncVehiclePassengers()
    {
        for (int v = 1; v < _units.Capacity; v++)
        {
            if (!_units.Slots.IsIndexAlive(v) || _tunnels.VehicleLayer[v] != ActorLayer.Passenger) continue;
            EntityId carrier = _tunnels.VehicleCarrier[v];
            if (!_units.Slots.IsAlive(carrier)) { RemoveMobileQuietly(ActorKind.Vehicle, v); continue; }   // cannot happen: OnTransportDying removes them
            Occupancy.Release(this, ActorKind.Vehicle, _units, v);   // a stray order may have reserved a cell
            ParkVehiclePassenger(v, carrier);
        }
    }

    /// <summary>The carrier is dying: its vehicle passengers (and their soldiers) are lost without a death weapon.</summary>
    private void LoseVehiclePassengers(EntityId transport)
    {
        for (int v = 1; v < _units.Capacity; v++)
        {
            if (!_units.Slots.IsIndexAlive(v) || _tunnels.VehicleLayer[v] != ActorLayer.Passenger || _tunnels.VehicleCarrier[v] != transport) continue;
            RemoveMobileQuietly(ActorKind.Vehicle, v);
        }
    }

    /// <summary>First free cell for vehicle <paramref name="v"/> in ring minRing..2 around <paramref name="at"/> (N..NW order). A ship's
    /// cargo needs a `landing` cell.</summary>
    private bool FindVehicleLandCell(int carrier, int v, CellCoord at, int minRing, out CellCoord cell)
    {
        VehicleType cvt = RulesOf(_units.Player[carrier]).Vehicles[_units.TypeIndex[carrier]];
        bool ship = MoveClass.IsNaval(MoveClass.Of(cvt));
        Locomotion cls = MoveClass.Of(RulesOf(_units.Player[v]).Vehicles[_units.TypeIndex[v]]);
        EntityId self = _units.Slots.IdOf(v);
        for (int r = minRing; r <= 2; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var cc = new CellCoord(at.X + dx, at.Y + dy);
                    if (!InMap(cc) || !Occupancy.FreeForVehicle(this, cc, cls, self)) continue;
                    if (ship && !Setup.Rules.Terrain.Landing(_map.LandAt(cc))) continue;
                    cell = cc;
                    return true;
                }
        cell = default;
        return false;
    }

    private void LandVehicle(int v, CellCoord cell)
    {
        _tunnels.VehicleLayer[v] = ActorLayer.Surface;
        _tunnels.VehicleCarrier[v] = default;
        _units.Position[v] = LeptonCoord.CenterOf(cell);
        _units.Destination[v] = _units.Position[v];
        Occupancy.Claim(this, ActorKind.Vehicle, _units, v);
        Queue(_units, v, MissionKind.Guard, default, default);
    }

    /// <summary>Put the first vehicle passenger that has a landing cell ashore (cells around <paramref name="shore"/>, ring 0 up).
    /// False when none can land; those stay aboard.</summary>
    private bool LandNextVehicle(EntityId carrier, CellCoord shore)
    {
        for (int v = 1; v < _units.Capacity; v++)
        {
            if (!_units.Slots.IsIndexAlive(v) || _tunnels.VehicleLayer[v] != ActorLayer.Passenger || _tunnels.VehicleCarrier[v] != carrier) continue;
            if (!FindVehicleLandCell(carrier.Index, v, shore, 0, out CellCoord cell)) continue;
            LandVehicle(v, cell);
            return true;
        }
        return false;
    }

    /// <summary>Instant unload (Deploy on a plain transport): every vehicle that has a cell in ring 1..2 lands; the rest stay aboard.</summary>
    private void UnloadVehiclePassengers(EntityId carrier, CellCoord at)
    {
        for (int v = 1; v < _units.Capacity; v++)
        {
            if (!_units.Slots.IsIndexAlive(v) || _tunnels.VehicleLayer[v] != ActorLayer.Passenger || _tunnels.VehicleCarrier[v] != carrier) continue;
            if (FindVehicleLandCell(carrier.Index, v, at, 1, out CellCoord cell)) LandVehicle(v, cell);
        }
    }
}
