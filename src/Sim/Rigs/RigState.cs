// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Rigs;

/// <summary>
/// The "rigs" state part (M6-02 skeleton, docs/design/10_m6_plan.md §3.3; filled by M6-03). Appended after "directive". All zero by
/// default; slot 0 of each array is the reserved "none" slot. A rig is a Left Behind building that packs into a vehicle and back.
/// <list type="bullet">
/// <item><see cref="PackTicksLeft"/>: per building slot, 0 = idle, &gt; 0 = packing in progress.</item>
/// <item><see cref="PackedFrom"/>: per vehicle slot, building type index + 1 of the rig this vehicle was packed from (0 = not a packed rig).</item>
/// <item><see cref="HpPermille"/>: per vehicle slot, health carried over between the building and the vehicle form (permille of max).</item>
/// <item><see cref="UnpackTicksLeft"/>: per vehicle slot, 0 = idle, &gt; 0 = unpacking in progress.</item>
/// <item><see cref="UnpackX"/> / <see cref="UnpackY"/>: per vehicle slot, top-left cell of the footprint being unpacked onto (valid while unpacking).</item>
/// <item><see cref="PausedQueue"/>: per vehicle slot, handle of the production queue paused while the rig is packed (0 = none; D-102 Q-A option 1).</item>
/// </list>
/// Building slots are cleared when a building is placed or removed, vehicle slots when a vehicle spawns or is removed
/// (Simulation.M3.cs hooks). Layout: version, building capacity, vehicle capacity, then (slot, packTicksLeft) for every non-zero building
/// slot, 0-terminated, then (slot, packedFrom, hpPermille, unpackTicksLeft, unpackX, unpackY, pausedQueue) for every vehicle slot with
/// any non-zero field, 0-terminated.
/// </summary>
public sealed class RigState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>Pack ticks left per building slot (0 = idle).</summary>
    public readonly int[] PackTicksLeft;
    /// <summary>Source building type index + 1 per vehicle slot (0 = not a packed rig).</summary>
    public readonly int[] PackedFrom;
    /// <summary>Carried-over health per vehicle slot, permille of max.</summary>
    public readonly int[] HpPermille;
    /// <summary>Unpack ticks left per vehicle slot (0 = idle).</summary>
    public readonly int[] UnpackTicksLeft;
    /// <summary>Unpack target cell X per vehicle slot.</summary>
    public readonly int[] UnpackX;
    /// <summary>Unpack target cell Y per vehicle slot.</summary>
    public readonly int[] UnpackY;
    /// <summary>Paused production queue handle per vehicle slot (0 = none).</summary>
    public readonly int[] PausedQueue;

    /// <summary>Creates an all-zero part sized by the setup's store limits.</summary>
    public RigState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int v = setup.Limits.Vehicles;
        PackTicksLeft = new int[setup.Limits.Buildings];
        PackedFrom = new int[v]; HpPermille = new int[v]; UnpackTicksLeft = new int[v];
        UnpackX = new int[v]; UnpackY = new int[v]; PausedQueue = new int[v];
    }

    /// <inheritdoc />
    public string Name => "rigs";

    /// <summary>True when vehicle slot <paramref name="i"/> is a packed rig.</summary>
    public bool IsPackedRig(int i) => PackedFrom[i] != 0;

    /// <summary>Reset a vehicle slot (spawn / removal).</summary>
    public void ClearVehicle(int i)
    {
        PackedFrom[i] = 0; HpPermille[i] = 0; UnpackTicksLeft[i] = 0; UnpackX[i] = 0; UnpackY[i] = 0; PausedQueue[i] = 0;
    }

    /// <summary>Reset a building slot (placement / removal).</summary>
    public void ClearBuilding(int i) => PackTicksLeft[i] = 0;

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(PackTicksLeft.Length); w.Write(PackedFrom.Length);
        for (int i = 1; i < PackTicksLeft.Length; i++) if (PackTicksLeft[i] != 0) { w.Write(i); w.Write(PackTicksLeft[i]); }
        w.Write(0);
        for (int i = 1; i < PackedFrom.Length; i++)
        {
            if (PackedFrom[i] == 0 && HpPermille[i] == 0 && UnpackTicksLeft[i] == 0 && UnpackX[i] == 0 && UnpackY[i] == 0 && PausedQueue[i] == 0)
                continue;
            w.Write(i); w.Write(PackedFrom[i]); w.Write(HpPermille[i]); w.Write(UnpackTicksLeft[i]);
            w.Write(UnpackX[i]); w.Write(UnpackY[i]); w.Write(PausedQueue[i]);
        }
        w.Write(0);
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("rigs version mismatch.");
        if (r.ReadInt32() != PackTicksLeft.Length || r.ReadInt32() != PackedFrom.Length) throw new InvalidDataException("rigs capacity mismatch.");
        Array.Clear(PackTicksLeft);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32()) PackTicksLeft[StateIo.Slot(i, PackTicksLeft.Length, Name)] = r.ReadInt32();
        Array.Clear(PackedFrom); Array.Clear(HpPermille); Array.Clear(UnpackTicksLeft);
        Array.Clear(UnpackX); Array.Clear(UnpackY); Array.Clear(PausedQueue);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            int s = StateIo.Slot(i, PackedFrom.Length, Name);
            PackedFrom[s] = r.ReadInt32(); HpPermille[s] = r.ReadInt32(); UnpackTicksLeft[s] = r.ReadInt32();
            UnpackX[s] = r.ReadInt32(); UnpackY[s] = r.ReadInt32(); PausedQueue[s] = r.ReadInt32();
        }
    }
}
