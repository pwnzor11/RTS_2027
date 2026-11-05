// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Naval;

/// <summary>
/// The "naval" state part (M7-02 skeleton, docs/design/11_m7_plan.md §2.4; filled by M7-03..05). Appended after "beachhead". Kept apart
/// from "ground" on purpose (same pattern as "rigs"). All zero by default; slot 0 of each per-vehicle array is the reserved "none" slot.
/// <list type="bullet">
/// <item><see cref="NavalRegion"/>: per map cell, the connected-region label for ships (0 = not water). M7-03 labels it by flood fill in
/// cell-index order. Only the naval class exists so far; a later class is a version bump.</item>
/// <item><see cref="UnloadTicksLeft"/>, <see cref="UnloadX"/>, <see cref="UnloadY"/>: per vehicle slot, the unload queue of a landing
/// craft: ticks until the next passenger leaves (0 = idle) and the shore cell it unloads onto (M7-04).</item>
/// <item><see cref="Submerged"/>: per vehicle slot, true while a submarine is dived (M7-05).</item>
/// </list>
/// Vehicle slots are cleared when a vehicle spawns or is removed (Simulation.M3.cs hooks). Layout: version, cell count, vehicle capacity,
/// the non-zero region labels as a count and (cell, label) pairs in cell order, then (slot, unloadTicksLeft, unloadX, unloadY, submerged)
/// for every vehicle slot with any non-zero field, 0-terminated.
/// </summary>
public sealed class NavalState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>Ship region label per map cell (0 = not water).</summary>
    public readonly ushort[] NavalRegion;
    /// <summary>Ticks until the next passenger leaves a landing craft, per vehicle slot (0 = idle).</summary>
    public readonly int[] UnloadTicksLeft;
    /// <summary>Unload target cell X per vehicle slot.</summary>
    public readonly int[] UnloadX;
    /// <summary>Unload target cell Y per vehicle slot.</summary>
    public readonly int[] UnloadY;
    /// <summary>True while a submarine is dived, per vehicle slot.</summary>
    public readonly bool[] Submerged;

    /// <summary>Counts loads of this part; the region cache recomputes when it changes (not saved).</summary>
    internal int LoadEpoch { get; private set; }

    /// <summary>Creates an all-zero part sized by the setup's map and store limits.</summary>
    public NavalState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        NavalRegion = new ushort[setup.Map.Width * setup.Map.Height];
        int v = setup.Limits.Vehicles;
        UnloadTicksLeft = new int[v]; UnloadX = new int[v]; UnloadY = new int[v]; Submerged = new bool[v];
    }

    /// <inheritdoc />
    public string Name => "naval";

    /// <summary>Reset a vehicle slot (spawn / removal).</summary>
    public void ClearVehicle(int i)
    {
        UnloadTicksLeft[i] = 0; UnloadX[i] = 0; UnloadY[i] = 0; Submerged[i] = false;
    }

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(NavalRegion.Length); w.Write(UnloadTicksLeft.Length);
        int n = 0;
        foreach (ushort x in NavalRegion) if (x != 0) n++;
        w.Write(n);
        for (int i = 0; i < NavalRegion.Length; i++) if (NavalRegion[i] != 0) { w.Write(i); w.Write(NavalRegion[i]); }
        for (int i = 1; i < UnloadTicksLeft.Length; i++)
        {
            if (UnloadTicksLeft[i] == 0 && UnloadX[i] == 0 && UnloadY[i] == 0 && !Submerged[i]) continue;
            w.Write(i); w.Write(UnloadTicksLeft[i]); w.Write(UnloadX[i]); w.Write(UnloadY[i]); w.Write(Submerged[i]);
        }
        w.Write(0);
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("naval version mismatch.");
        LoadEpoch++;
        if (r.ReadInt32() != NavalRegion.Length || r.ReadInt32() != UnloadTicksLeft.Length) throw new InvalidDataException("naval capacity mismatch.");
        Array.Clear(NavalRegion);
        int n = StateIo.Count(r, Name);
        for (int k = 0; k < n; k++)
        {
            int i = r.ReadInt32();
            if (i < 0 || i >= NavalRegion.Length) throw new InvalidDataException("naval cell out of range.");
            NavalRegion[i] = r.ReadUInt16();
        }
        Array.Clear(UnloadTicksLeft); Array.Clear(UnloadX); Array.Clear(UnloadY); Array.Clear(Submerged);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            int s = StateIo.Slot(i, UnloadTicksLeft.Length, Name);
            UnloadTicksLeft[s] = r.ReadInt32(); UnloadX[s] = r.ReadInt32(); UnloadY[s] = r.ReadInt32(); Submerged[s] = r.ReadBoolean();
        }
    }
}
