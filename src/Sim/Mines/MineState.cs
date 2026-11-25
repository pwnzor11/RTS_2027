// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Mines;

/// <summary>
/// The "mines" state part (S44 M-01): the sea-mine table plus the per-minelayer bookkeeping. Appended after "props".
/// Mines: a fixed number of slots (<see cref="DefaultCapacity"/> usable; index 0 is the reserved "none" slot). A slot is live while
/// <see cref="Live"/> is set. A new mine takes the lowest free slot; when the table is full a lay is refused. A mine is owned by a
/// player (its team is never hurt) and is hidden from enemy teams unless a sonar or a sweeper covers it (see Simulation.Mines.cs).
/// Minelayers (per vehicle slot): <see cref="Laid"/> = mines laid since the last restock (stock = mine_capacity - Laid, so a new
/// layer starts full), <see cref="Cooldown"/> = ticks until the next mine may be laid, <see cref="Restock"/> = restock progress,
/// <see cref="OrderSet"/>/<see cref="OrderX"/>/<see cref="OrderY"/> = the cell a Deploy order told it to lay on.
/// Layout: version, mine slot count, vehicle slot count, every mine slot with any non-zero field (slot, live, owner, x, y, damage, radius, warhead) 0-terminated, then
/// minelayer rows (slot, laid, cooldown, restock, orderSet, orderX, orderY) for every vehicle slot with any non-default field, 0-terminated.
/// </summary>
public sealed class MineState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>Usable mine slots.</summary>
    public const int DefaultCapacity = 128;

    /// <summary>True while the mine slot holds a live mine.</summary>
    public readonly bool[] Live;
    /// <summary>Owning player per mine slot.</summary>
    public readonly byte[] Owner;
    /// <summary>Mine cell X per slot.</summary>
    public readonly int[] CellX;
    /// <summary>Mine cell Y per slot.</summary>
    public readonly int[] CellY;
    /// <summary>Base damage per slot.</summary>
    public readonly int[] Damage;
    /// <summary>Trigger radius in cells (Chebyshev) per slot.</summary>
    public readonly int[] Radius;
    /// <summary>Warhead index (in the owner's rules) per slot.</summary>
    public readonly int[] Warhead;

    /// <summary>Mines laid since the last restock, per vehicle slot.</summary>
    public readonly int[] Laid;
    /// <summary>Ticks until the next lay, per vehicle slot.</summary>
    public readonly int[] Cooldown;
    /// <summary>Restock progress in ticks, per vehicle slot.</summary>
    public readonly int[] Restock;
    /// <summary>True while a lay order waits, per vehicle slot.</summary>
    public readonly bool[] OrderSet;
    /// <summary>Lay order cell X per vehicle slot.</summary>
    public readonly int[] OrderX;
    /// <summary>Lay order cell Y per vehicle slot.</summary>
    public readonly int[] OrderY;

    /// <summary>Creates an empty part with <see cref="DefaultCapacity"/> mine slots and one row per vehicle slot.</summary>
    public MineState(SimSetup setup) : this(setup, DefaultCapacity) { }

    /// <summary>Creates an empty part with <paramref name="capacity"/> usable mine slots.</summary>
    public MineState(SimSetup setup, int capacity)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        int n = capacity + 1;
        Live = new bool[n]; Owner = new byte[n]; CellX = new int[n]; CellY = new int[n]; Damage = new int[n]; Radius = new int[n]; Warhead = new int[n];
        int v = setup.Limits.Vehicles;
        Laid = new int[v]; Cooldown = new int[v]; Restock = new int[v]; OrderSet = new bool[v]; OrderX = new int[v]; OrderY = new int[v];
    }

    /// <inheritdoc />
    public string Name => "mines";

    /// <summary>Usable mine slots.</summary>
    public int Capacity => Live.Length - 1;

    /// <summary>Number of live mines.</summary>
    public int Count
    {
        get { int n = 0; for (int i = 1; i < Live.Length; i++) if (Live[i]) n++; return n; }
    }

    /// <summary>Frees a mine slot (all fields reset so equal states hash equal).</summary>
    public void Clear(int i)
    {
        Live[i] = false; Owner[i] = 0; CellX[i] = 0; CellY[i] = 0; Damage[i] = 0; Radius[i] = 0; Warhead[i] = 0;
    }

    /// <summary>Resets a vehicle slot's minelayer data (spawn / removal).</summary>
    public void ClearVehicle(int i)
    {
        Laid[i] = 0; Cooldown[i] = 0; Restock[i] = 0; OrderSet[i] = false; OrderX[i] = 0; OrderY[i] = 0;
    }

    private bool HasVehicleData(int i) =>
        Laid[i] != 0 || Cooldown[i] != 0 || Restock[i] != 0 || OrderSet[i] || OrderX[i] != 0 || OrderY[i] != 0;

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(Live.Length);
        w.Write(Laid.Length);
        for (int i = 1; i < Live.Length; i++)
        {
            if (!Live[i] && Owner[i] == 0 && CellX[i] == 0 && CellY[i] == 0 && Damage[i] == 0 && Radius[i] == 0 && Warhead[i] == 0) continue;
            w.Write(i); w.Write(Live[i]); w.Write(Owner[i]); w.Write(CellX[i]); w.Write(CellY[i]); w.Write(Damage[i]); w.Write(Radius[i]); w.Write(Warhead[i]);
        }
        w.Write(0);
        for (int i = 1; i < Laid.Length; i++)
        {
            if (!HasVehicleData(i)) continue;
            w.Write(i); w.Write(Laid[i]); w.Write(Cooldown[i]); w.Write(Restock[i]); w.Write(OrderSet[i]); w.Write(OrderX[i]); w.Write(OrderY[i]);
        }
        w.Write(0);
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("mines version mismatch.");
        if (r.ReadInt32() != Live.Length || r.ReadInt32() != Laid.Length) throw new InvalidDataException("mines capacity mismatch.");
        for (int i = 1; i < Live.Length; i++) Clear(i);
        for (int i = 1; i < Laid.Length; i++) ClearVehicle(i);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            int s = StateIo.Slot(i, Live.Length, Name);
            Live[s] = r.ReadBoolean(); Owner[s] = r.ReadByte(); CellX[s] = r.ReadInt32(); CellY[s] = r.ReadInt32();
            Damage[s] = r.ReadInt32(); Radius[s] = r.ReadInt32(); Warhead[s] = r.ReadInt32();
        }
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            int s = StateIo.Slot(i, Laid.Length, Name);
            Laid[s] = r.ReadInt32(); Cooldown[s] = r.ReadInt32(); Restock[s] = r.ReadInt32(); OrderSet[s] = r.ReadBoolean();
            OrderX[s] = r.ReadInt32(); OrderY[s] = r.ReadInt32();
        }
    }
}
