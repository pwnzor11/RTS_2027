// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Economy;

/// <summary>Step of a Harvest mission, kept in <see cref="EconomyState.HarvestState"/>.</summary>
public enum HarvestStep : byte { Seek = 0, Go = 1, Load = 2 }

/// <summary>Step of a Return mission (the dock dance), kept in <see cref="EconomyState.DockState"/>.</summary>
public enum DockStep : byte { Pick = 0, Go = 1, Wait = 2, Enter = 3, Unload = 4 }

/// <summary>
/// The "economy" state part (M2-07). Per-vehicle-slot harvester data (sized by Limits.Vehicles) and per-building-slot dock
/// occupancy (sized by Limits.Buildings). All of it is hashed and saved; only slots that differ from the cleared default are
/// written. <see cref="Owner"/> records which entity the slot's data belongs to, so data left by a dead vehicle is never
/// inherited by the next vehicle that reuses the slot (see <see cref="Touch"/>). Appended after "players" (append-only order).
/// </summary>
public sealed class EconomyState : ISimStatePart
{
    private const byte Version = 2;

    /// <summary>Highest density a cell can hold (design 02 section 2).</summary>
    public const int MaxDensity = 12;

    /// <summary>The entity the slot's data belongs to (generation checked by <see cref="Touch"/>).</summary>
    public readonly EntityId[] Owner;
    /// <summary>Units of resource carried.</summary>
    public readonly int[] Cargo;
    /// <summary>Credits the carried units are worth (ashfall and pitchblende pay differently).</summary>
    public readonly int[] CargoValue;
    /// <summary>The last cell the harvester loaded in; (0,0) = none yet. Harvest searches rings around it.</summary>
    public readonly CellCoord[] LastFieldCell;
    /// <summary>Harvest: the cell being driven to / loaded in.</summary>
    public readonly CellCoord[] Goal;
    /// <summary>Harvest: a cell that turned out unreachable, skipped by the next search.</summary>
    public readonly CellCoord[] BadCell;
    /// <summary>The refinery the harvester is returning to.</summary>
    public readonly EntityId[] DockTarget;
    /// <summary>Return step, a <see cref="DockStep"/>.</summary>
    public readonly byte[] DockState;
    /// <summary>Harvest step, a <see cref="HarvestStep"/>.</summary>
    public readonly byte[] HarvestState;
    /// <summary>Absolute tick of the next load / unload step.</summary>
    public readonly int[] Timer;
    /// <summary>Queue ticket at a dock (lower goes first); 0 = none.</summary>
    public readonly int[] Ticket;
    /// <summary>Per building slot: the harvester that holds the dock (docking or unloading), or none.</summary>
    public readonly EntityId[] DockOccupant;
    /// <summary>Next queue ticket to hand out (starts at 1).</summary>
    public int NextTicket = 1;

    public EconomyState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int v = setup.Limits.Vehicles, b = setup.Limits.Buildings;
        Owner = new EntityId[v];
        Cargo = new int[v];
        CargoValue = new int[v];
        LastFieldCell = new CellCoord[v];
        Goal = new CellCoord[v];
        BadCell = new CellCoord[v];
        DockTarget = new EntityId[v];
        DockState = new byte[v];
        HarvestState = new byte[v];
        Timer = new int[v];
        Ticket = new int[v];
        DockOccupant = new EntityId[b];
    }

    public string Name => "economy";

    /// <summary>Number of vehicle slots covered (slot 0 is the reserved "none" slot).</summary>
    public int VehicleCapacity => Owner.Length;

    /// <summary>Make sure slot <paramref name="i"/>'s data belongs to <paramref name="id"/>; a slot reused by a new vehicle is cleared.</summary>
    public void Touch(int i, EntityId id)
    {
        if (Owner[i] == id) return;
        Clear(i);
        Owner[i] = id;
    }

    /// <summary>Reset every harvester field of a vehicle slot.</summary>
    public void Clear(int i)
    {
        Owner[i] = default; Cargo[i] = 0; CargoValue[i] = 0; LastFieldCell[i] = default; Goal[i] = default; BadCell[i] = default;
        DockTarget[i] = default; DockState[i] = 0; HarvestState[i] = 0; Timer[i] = 0; Ticket[i] = 0;
    }

    /// <summary>True if the slot holds anything other than the cleared default.</summary>
    public bool IsDirty(int i) =>
        !Owner[i].IsNone || Cargo[i] != 0 || CargoValue[i] != 0 || LastFieldCell[i] != default || Goal[i] != default
        || BadCell[i] != default || !DockTarget[i].IsNone || DockState[i] != 0 || HarvestState[i] != 0 || Timer[i] != 0 || Ticket[i] != 0;

    /// <summary>Layout: version, capacities, next ticket, then (slot, fields) for each dirty vehicle slot ended by 0, then
    /// (slot, id) for each occupied dock ended by 0.</summary>
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(Owner.Length); w.Write(DockOccupant.Length);
        w.Write(NextTicket);
        for (int i = 1; i < Owner.Length; i++)
        {
            if (!IsDirty(i)) continue;
            w.Write(i);
            WriteId(w, Owner[i]);
            w.Write(Cargo[i]); w.Write(CargoValue[i]);
            WriteCell(w, LastFieldCell[i]); WriteCell(w, Goal[i]); WriteCell(w, BadCell[i]);
            WriteId(w, DockTarget[i]);
            w.Write(DockState[i]); w.Write(HarvestState[i]);
            w.Write(Timer[i]); w.Write(Ticket[i]);
        }
        w.Write(0);
        for (int b = 1; b < DockOccupant.Length; b++)
        {
            if (DockOccupant[b].IsNone) continue;
            w.Write(b);
            WriteId(w, DockOccupant[b]);
        }
        w.Write(0);
    }

    public void Read(BinaryReader r)
    {
        byte version = r.ReadByte();
        if (version != Version) throw new InvalidDataException("economy version mismatch.");
        if (r.ReadInt32() != Owner.Length || r.ReadInt32() != DockOccupant.Length) throw new InvalidDataException("economy capacity mismatch.");
        NextTicket = r.ReadInt32();
        for (int i = 1; i < Owner.Length; i++) Clear(i);
        for (int b = 1; b < DockOccupant.Length; b++) DockOccupant[b] = default;
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            if (i < 0 || i >= Owner.Length) throw new InvalidDataException("economy slot out of range.");
            Owner[i] = ReadId(r);
            Cargo[i] = r.ReadInt32(); CargoValue[i] = r.ReadInt32();
            LastFieldCell[i] = ReadCell(r); Goal[i] = ReadCell(r); BadCell[i] = ReadCell(r);
            DockTarget[i] = ReadId(r);
            DockState[i] = r.ReadByte(); HarvestState[i] = r.ReadByte();
            Timer[i] = r.ReadInt32(); Ticket[i] = r.ReadInt32();
        }
        for (int b = r.ReadInt32(); b != 0; b = r.ReadInt32())
        {
            if (b < 0 || b >= DockOccupant.Length) throw new InvalidDataException("economy dock slot out of range.");
            DockOccupant[b] = ReadId(r);
        }
    }

    private static void WriteId(BinaryWriter w, EntityId id) { w.Write(id.Index); w.Write(id.Generation); }
    private static EntityId ReadId(BinaryReader r) => new(r.ReadUInt16(), r.ReadUInt16());
    private static void WriteCell(BinaryWriter w, CellCoord c) { w.Write(c.X); w.Write(c.Y); }
    private static CellCoord ReadCell(BinaryReader r) => new(r.ReadInt32(), r.ReadInt32());
}
