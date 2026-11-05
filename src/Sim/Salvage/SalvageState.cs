// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Salvage;

/// <summary>
/// The "salvage" state part (M6-02 skeleton, docs/design/10_m6_plan.md §3.3; filled by M6-04): the wreck table. Appended after "rigs".
/// A fixed number of wreck slots (<see cref="DefaultCapacity"/> usable slots; array index 0 is the reserved "none" slot). A slot is
/// live while <see cref="TicksLeft"/> &gt; 0. Expiry and draining run in slot order; a new wreck takes the lowest free slot; when the
/// table is full the wreck with the fewest ticks left (the oldest) is replaced. Wrecks are visible to every player (D-102).
/// Layout: version, slot count, then (slot, cell x, cell y, value, valueStart, ticksLeft) for every slot with any non-zero field,
/// 0-terminated. Free slots must be cleared with <see cref="Clear"/> so equal states hash equal.
/// </summary>
public sealed class SalvageState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>Usable wreck slots (the plan's default cap).</summary>
    public const int DefaultCapacity = 256;

    /// <summary>Wreck cell X per slot.</summary>
    public readonly int[] CellX;
    /// <summary>Wreck cell Y per slot.</summary>
    public readonly int[] CellY;
    /// <summary>Credits still in the wreck per slot.</summary>
    public readonly int[] Value;
    /// <summary>Credits the wreck started with per slot (the decay is linear from this to 0 over its lifetime).</summary>
    public readonly int[] ValueStart;
    /// <summary>Ticks until the wreck expires per slot (0 = free slot).</summary>
    public readonly int[] TicksLeft;

    /// <summary>Creates an empty table of <see cref="DefaultCapacity"/> slots.</summary>
    public SalvageState(SimSetup setup) : this(setup, DefaultCapacity) { }

    /// <summary>Creates an empty table of <paramref name="capacity"/> usable slots.</summary>
    public SalvageState(SimSetup setup, int capacity)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        int n = capacity + 1;
        CellX = new int[n]; CellY = new int[n]; Value = new int[n]; ValueStart = new int[n]; TicksLeft = new int[n];
    }

    /// <inheritdoc />
    public string Name => "salvage";

    /// <summary>Number of usable slots.</summary>
    public int Capacity => TicksLeft.Length - 1;

    /// <summary>Number of live wrecks.</summary>
    public int Count
    {
        get { int n = 0; for (int i = 1; i < TicksLeft.Length; i++) if (TicksLeft[i] > 0) n++; return n; }
    }

    /// <summary>Resets a slot to free.</summary>
    public void Clear(int i) { CellX[i] = 0; CellY[i] = 0; Value[i] = 0; ValueStart[i] = 0; TicksLeft[i] = 0; }

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(TicksLeft.Length);
        for (int i = 1; i < TicksLeft.Length; i++)
        {
            if (CellX[i] == 0 && CellY[i] == 0 && Value[i] == 0 && ValueStart[i] == 0 && TicksLeft[i] == 0) continue;
            w.Write(i); w.Write(CellX[i]); w.Write(CellY[i]); w.Write(Value[i]); w.Write(ValueStart[i]); w.Write(TicksLeft[i]);
        }
        w.Write(0);
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("salvage version mismatch.");
        if (r.ReadInt32() != TicksLeft.Length) throw new InvalidDataException("salvage capacity mismatch.");
        for (int i = 1; i < TicksLeft.Length; i++) Clear(i);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            int s = StateIo.Slot(i, TicksLeft.Length, Name);
            CellX[s] = r.ReadInt32(); CellY[s] = r.ReadInt32(); Value[s] = r.ReadInt32(); ValueStart[s] = r.ReadInt32(); TicksLeft[s] = r.ReadInt32();
        }
    }
}
