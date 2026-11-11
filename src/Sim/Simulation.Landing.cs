// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Numerics;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using Rts.Sim.World;
using Rts.Sim.Movement;

namespace Rts.Sim;

// M7-04 (docs/design/11_m7_plan.md §2.5, design 07 §6.3, D-139b): beach landing and the Skyhook air lift. Reuses the transport code
// (Simulation.Transport.cs: PassengerCount, FindUnloadCell, the Passenger layer); only the "when, where and how fast" is new.
//   CommandKind.Unload (Arg low byte = Vehicle, Cell = shore cell) on a transport with unload_ticks > 0 (landing craft) or air
//   locomotion (Skyhook) queues MissionKind.Unload. The craft sails to a water cell next to the shore cell (the Skyhook flies over it),
//   then puts one passenger ashore every unload_ticks onto the nearest free foot-passable cells (shore cell first, then rings 1-2).
//   State: NavalState.UnloadX/UnloadY (shore cell) and UnloadTicksLeft (ticks to the next passenger, 0 = not yet arrived).
// M7-04b: vehicle passengers (vehicle_slots, Simulation.VehiclePassengers.cs) leave after the soldiers, one per unload_ticks, and a ship
// puts them only on a Coast (terrain `landing`) cell; the soldiers' rule below is unchanged.
// ASSUMED (M7-04): a shore cell is accepted when foot can stand on it (Coast, Clear, shallow wading; not Cliff, Rock or deep water). — confirm (M7-04)
// ASSUMED (M7-04): a Skyhook with unload_ticks = 0 puts one passenger down per tick; when no free cell remains the unload ends and
// the rest stay aboard. — confirm (M7-04)
public sealed partial class Simulation
{
    private bool IsLandingTransport(VehicleType vt) => (vt.Passengers > 0 || vt.VehicleSlots > 0) && (vt.UnloadTicks > 0 || vt.Locomotion == Locomotion.Air);

    /// <summary>CommandKind.Unload on a landing transport with Cell = the shore cell. Refused (ignored) when the cell is not standable,
    /// nobody is aboard, or a ship has no sea cell next to the shore it can reach.</summary>
    private void ApplyLandingUnload(Command c, VehicleType vt)
    {
        int i = c.Subject.Index;
        if (_tunnels.VehicleLayer[i] != ActorLayer.Surface || _units.Health[i] <= 0 || !InMap(c.Cell)) return;
        if (!_map.IsPassableTerrain(c.Cell, Locomotion.Foot, Setup.Rules.Terrain) || PassengerCount(c.Subject) == 0) return;
        if (vt.Locomotion != Locomotion.Air && CellDistance(_units.Position[i].ToCell(), c.Cell) > 1 && !FindApproachCell(i, vt, c.Cell, out _)) return;
        MoveMission.DropRequest(this, _units, i);
        _naval.UnloadX[i] = c.Cell.X; _naval.UnloadY[i] = c.Cell.Y; _naval.UnloadTicksLeft[i] = 0;
        Queue(_units, i, MissionKind.Unload, c.Cell, default);
    }

    /// <summary>D-205 (SIM-RULES-06): Deploy on a landing transport. A Skyhook (air locomotion) is always airborne and the order is refused;
    /// a landing craft starts the timed unload (<c>unload_ticks</c> per passenger) onto the first standable cell next to it (N..NW order);
    /// refused when it has no such neighbour.</summary>
    private void ApplyLandingDeploy(Command c, VehicleType vt)
    {
        if (vt.Locomotion == Locomotion.Air) return;
        CellCoord at = _units.Position[c.Subject.Index].ToCell();
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var cc = new CellCoord(at.X + dx, at.Y + dy);
                if (!InMap(cc) || !_map.IsPassableTerrain(cc, Locomotion.Foot, Setup.Rules.Terrain)) continue;
                ApplyLandingUnload(new Command(c.ExecTick, c.Player, c.Sequence, CommandKind.Unload, c.Subject, default, cc, (int)ActorKind.Vehicle), vt);
                return;
            }
    }

    /// <summary>The sea (movement-class passable) cell around <paramref name="shore"/> nearest the craft, in the craft's own region.</summary>
    private bool FindApproachCell(int i, VehicleType vt, CellCoord shore, out CellCoord found)
    {
        Locomotion cls = MoveClass.Of(vt);
        CellCoord cur = _units.Position[i].ToCell();
        ushort[]? labels = MoveClass.IsNaval(cls) ? RegionLabels(cls) : null;
        int here = labels != null ? labels[cur.Y * Setup.Map.Width + cur.X] : 0;
        long bestD = long.MaxValue;
        found = default;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var cc = new CellCoord(shore.X + dx, shore.Y + dy);
                if (!InMap(cc) || !_map.IsPassableTerrain(cc, cls, Setup.Rules.Terrain) || !_map.BuildingAt(cc).IsNone) continue;
                if (labels != null) { int l = labels[cc.Y * Setup.Map.Width + cc.X]; if (l == 0 || here == 0 || l != here) continue; }
                long ex = cc.X - cur.X, ey = cc.Y - cur.Y, d = ex * ex + ey * ey;
                if (d >= bestD) continue;
                bestD = d; found = cc;
            }
        return bestD != long.MaxValue;
    }

    /// <summary>One step of MissionKind.Unload for vehicle slot <paramref name="i"/>: approach, then one passenger per unload_ticks.
    /// Returns ticks to sleep.</summary>
    internal int RunUnloadMission(int i)
    {
        VehicleType vt = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]];
        EntityId self = _units.Slots.IdOf(i);
        if (_units.Health[i] <= 0 || !IsLandingTransport(vt) || _tunnels.VehicleLayer[i] != ActorLayer.Surface || PassengerCount(self) == 0)
            return EndUnload(i);
        var shore = new CellCoord(_naval.UnloadX[i], _naval.UnloadY[i]);
        CellCoord cur = _units.Position[i].ToCell();
        if (vt.Locomotion == Locomotion.Air)
        {
            if (_units.Position[i] != LeptonCoord.CenterOf(shore)) return MoveMission.StepToward(this, _units, ActorKind.Vehicle, i, shore);
        }
        else if (CellDistance(cur, shore) > 1)
        {
            if (!FindApproachCell(i, vt, shore, out CellCoord goal)) return EndUnload(i);
            return MoveMission.StepToward(this, _units, ActorKind.Vehicle, i, goal);
        }
        int per = Math.Max(1, vt.UnloadTicks);
        if (_naval.UnloadTicksLeft[i] <= 0) { _naval.UnloadTicksLeft[i] = per; return 1; }   // arrived: the first passenger leaves after one interval
        if (--_naval.UnloadTicksLeft[i] > 0) return 1;
        bool landed = false;
        for (int p = 1; p < _infantry.Capacity && !landed; p++)
        {
            if (!_infantry.Slots.IsIndexAlive(p) || _tunnels.InfantryLayer[p] != ActorLayer.Passenger || _tunnels.InfantryCarrier[p] != self) continue;
            if (!FindUnloadCell(shore, out CellCoord cell, out int slot, 0)) return EndUnload(i);   // no room: the rest stay aboard
            PutAshore(p, cell, slot);
            landed = true;
        }
        if (!landed && !LandNextVehicle(self, shore)) return EndUnload(i);   // M7-04b: soldiers first, then vehicles; none can land
        if (PassengerCount(self) == 0) return EndUnload(i);
        _naval.UnloadTicksLeft[i] = per;
        return 1;
    }

    private void PutAshore(int infantry, CellCoord cell, int slot)
    {
        _tunnels.InfantryLayer[infantry] = ActorLayer.Surface;
        _tunnels.InfantryCarrier[infantry] = default;
        _infantry.SubSlot[infantry] = (byte)slot;
        _infantry.SlotCell[infantry] = cell;
        _infantry.Position[infantry] = InfantryStore.SlotPosition(cell, slot);
        _map.SetInfantry(cell, slot, _infantry.Slots.IdOf(infantry));
        Queue(_infantry, infantry, MissionKind.Guard, default, default);
    }

    private int EndUnload(int i)
    {
        _naval.UnloadTicksLeft[i] = 0; _naval.UnloadX[i] = 0; _naval.UnloadY[i] = 0;
        Queue(_units, i, MissionKind.Guard, default, default);
        return 1;
    }
}
