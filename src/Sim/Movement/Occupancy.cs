// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim.Movement;

/// <summary>
/// Occupancy rules shared by movement code (M1-08). A vehicle holds exactly the map cell its Position is in
/// (<c>MapState.VehicleAt</c>), plus the one cell it has RESERVED while it drives into it (set when it starts moving
/// into the cell, the old cell is released when its Position crosses the boundary). A cell holds at most one
/// vehicle. Infantry hold a sub-slot (<c>InfantryAt</c>) of the cell they stand in, and claim a free sub-slot of the next cell
/// when they start moving into it, releasing the old slot when they cross the boundary. Holders are only ever released
/// by the holder itself (an id check), so a stale or duplicate claim can never free somebody else's cell.
/// </summary>
internal static class Occupancy
{
    /// <summary>Neighbour offsets in the fixed deterministic order N, NE, E, SE, S, SW, W, NW.</summary>
    public static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
    public static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

    public static Locomotion LocoOf(Simulation sim, ActorKind kind, MobileStore store, int i) =>
        kind == ActorKind.Vehicle ? sim.Setup.Rules.Vehicles[store.TypeIndex[i]].Locomotion : Locomotion.Foot;

    /// <summary>The cell the actor currently stands in: Position's cell for vehicles, the held sub-slot's cell for infantry.</summary>
    public static CellCoord CurCell(ActorKind kind, MobileStore store, int i) =>
        kind == ActorKind.Vehicle ? store.Position[i].ToCell() : ((InfantryStore)store).SlotCell[i];

    /// <summary>Inside the map, passable terrain (walls count) for the locomotion and no building — except a gate of
    /// <paramref name="passer"/>'s team (M3-04, D-076; <see cref="PathService.NoPasser"/> = every building blocks).</summary>
    public static bool TerrainOk(Simulation sim, CellCoord c, Locomotion loco, byte passer = PathService.NoPasser)
    {
        if (!sim.Setup.Map.Contains(c) || !sim.Map.IsPassableTerrain(c, loco, sim.Setup.Rules.Terrain)) return false;
        EntityId b = sim.Map.BuildingAt(c);
        return b.IsNone || (passer != PathService.NoPasser && sim.GateOpenFor(b, passer));   // M3-04: own-team gate
    }

    /// <summary>True if the infantry in a slot id is alive with positive health.</summary>
    public static bool LiveInfantry(Simulation sim, EntityId id) =>
        !id.IsNone && sim.Infantry.Slots.IsAlive(id) && sim.Infantry.Health[id.Index] > 0;

    /// <summary>A vehicle (other than <paramref name="self"/>) could be placed in the cell right now.</summary>
    public static bool FreeForVehicle(Simulation sim, CellCoord c, Locomotion loco, EntityId self, byte passer = PathService.NoPasser)
    {
        if (!TerrainOk(sim, c, loco, passer)) return false;   // M3-04: passer
        EntityId v = sim.Map.VehicleAt(c);
        if (!v.IsNone && v != self) return false;
        for (int s = 0; s < InfantryStore.SubSlotCount; s++)
            if (!sim.Map.InfantryAt(c, s).IsNone) return false;
        return true;
    }

    /// <summary>An infantry soldier could enter the cell right now (no vehicle, a free sub-slot).</summary>
    public static bool FreeForInfantry(Simulation sim, CellCoord c, byte passer = PathService.NoPasser)
    {
        if (!TerrainOk(sim, c, Locomotion.Foot, passer) || !sim.Map.VehicleAt(c).IsNone) return false;   // M3-04: passer
        return sim.Map.FreeInfantrySlot(c) >= 0;
    }

    /// <summary>A diagonal step may not cut a corner that is blocked terrain or a building (same rule as the path search).</summary>
    public static bool CornersOk(Simulation sim, CellCoord from, int dir, Locomotion loco, byte passer = PathService.NoPasser)
    {
        if ((dir & 1) == 0) return true;
        return TerrainOk(sim, new CellCoord(from.X + Dx[dir], from.Y), loco, passer)   // M3-04: passer
            && TerrainOk(sim, new CellCoord(from.X, from.Y + Dy[dir]), loco, passer);
    }

    public static int Chebyshev(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>The actor can sidestep into the neighbour cell in direction <paramref name="dir"/>.</summary>
    public static bool NeighbourFree(Simulation sim, ActorKind kind, MobileStore store, int i, CellCoord from, int dir)
    {
        var c = new CellCoord(from.X + Dx[dir], from.Y + Dy[dir]);
        Locomotion loco = LocoOf(sim, kind, store, i);
        byte passer = store.Player[i];   // M3-04: own-team gates are open to this actor
        if (!CornersOk(sim, from, dir, loco, passer)) return false;
        return kind == ActorKind.Vehicle ? FreeForVehicle(sim, c, loco, store.Slots.IdOf(i), passer) : FreeForInfantry(sim, c, passer);
    }

    /// <summary>First free adjacent cell in the order N..NW starting at <paramref name="startDir"/>.</summary>
    public static bool FindScatterCell(Simulation sim, ActorKind kind, MobileStore store, int i, int startDir, out CellCoord cell)
    {
        CellCoord from = CurCell(kind, store, i);
        for (int k = 0; k < 8; k++)
        {
            int d = (startDir + k) & 7;
            if (!NeighbourFree(sim, kind, store, i, from, d)) continue;
            cell = new CellCoord(from.X + Dx[d], from.Y + Dy[d]);
            return true;
        }
        cell = default;
        return false;
    }

    /// <summary>Claim the cell(s) of a freshly spawned actor. A taken cell/slot is left to its holder.</summary>
    public static void Claim(Simulation sim, ActorKind kind, MobileStore store, int i)
    {
        CellCoord cell = store.Position[i].ToCell();
        EntityId me = store.Slots.IdOf(i);
        if (Missions.AirMission.IsAir(sim, kind, store, i)) return;   // M3-06: air units hold no cell
        if (kind == ActorKind.Vehicle)
        {
            if (sim.Map.VehicleAt(cell).IsNone) sim.Map.SetVehicle(cell, me);
            return;
        }
        var inf = (InfantryStore)store;
        inf.SlotCell[i] = cell;
        int slot = inf.SubSlot[i];
        if (!sim.Map.InfantryAt(cell, slot).IsNone)
        {
            slot = sim.Map.FreeInfantrySlot(cell);
            if (slot < 0) return;
            inf.SubSlot[i] = (byte)slot;
            inf.Position[i] = InfantryStore.SlotPosition(cell, slot);
        }
        sim.Map.SetInfantry(cell, slot, me);
    }

    /// <summary>Release every claim of the actor (standing cell/slot and reservation).</summary>
    public static void Release(Simulation sim, ActorKind kind, MobileStore store, int i)
    {
        EntityId me = store.Slots.IdOf(i);
        CellCoord cell = CurCell(kind, store, i);
        if (kind == ActorKind.Vehicle)
        {
            if (sim.Map.VehicleAt(cell) == me) sim.Map.SetVehicle(cell, EntityId.None);
            if (store.NextCellReserved[i] && sim.Map.VehicleAt(store.ReservedCell[i]) == me)
                sim.Map.SetVehicle(store.ReservedCell[i], EntityId.None);
        }
        else
        {
            var inf = (InfantryStore)store;
            int slot = inf.SubSlot[i];
            if (sim.Map.InfantryAt(cell, slot) == me) sim.Map.SetInfantry(cell, slot, EntityId.None);
            if (store.NextCellReserved[i])
            {
                CellCoord rc = store.ReservedCell[i];
                int rs = store.ReservedSlot[i];
                if (sim.Map.InfantryAt(rc, rs) == me) sim.Map.SetInfantry(rc, rs, EntityId.None);
            }
        }
        store.NextCellReserved[i] = false;
    }

    /// <summary>Give back an unfinished reservation (Stop) but keep the cell the actor stands in.</summary>
    public static void DropReservation(Simulation sim, ActorKind kind, MobileStore store, int i)
    {
        if (!store.NextCellReserved[i]) return;
        EntityId me = store.Slots.IdOf(i);
        CellCoord pos = CurCell(kind, store, i), rc = store.ReservedCell[i];
        if (kind == ActorKind.Vehicle)
        {
            if (rc != pos && sim.Map.VehicleAt(rc) == me) sim.Map.SetVehicle(rc, EntityId.None);
        }
        else if (rc != pos)
        {
            int rs = store.ReservedSlot[i];
            if (sim.Map.InfantryAt(rc, rs) == me) sim.Map.SetInfantry(rc, rs, EntityId.None);
        }
        store.NextCellReserved[i] = false;
    }
}
