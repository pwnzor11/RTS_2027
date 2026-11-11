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
/// <item><see cref="HangarOf"/>, <see cref="AirState"/>, <see cref="SortieLeft"/>: per vehicle slot of an aircraft, the carrier slot whose
/// hangar it holds a place in, where it is (<see cref="AirDocked"/>, <see cref="AirOnSortie"/>, <see cref="AirReturning"/>) and the ticks
/// of flight left on a sortie (M7-09). A carrier keeps no field of its own: its hangar is the aircraft that point at it.</item>
/// </list>
/// Vehicle slots are cleared when a vehicle spawns or is removed (Simulation.M3.cs hooks). Layout: version, cell count, vehicle capacity,
/// the non-zero region labels as a count and (cell, label) pairs in cell order, then (slot, unloadTicksLeft, unloadX, unloadY, submerged)
/// for every vehicle slot with any non-zero field, 0-terminated. Version 2 (written only while some aircraft holds a hangar place, so games
/// without carriers hash as before) appends (slot, hangarOf, airState, sortieLeft) for every slot with any non-zero hangar field, 0-terminated.
/// Version 3 (written only while some slot has ammo or rearm progress, D-156) writes the hangar list (possibly empty) and then appends
/// (slot, ammoLeft, rearmProgress) for every slot with either non-zero, 0-terminated. Version 4 (written only while a firing submarine is
/// revealed, D-205) writes the hangar and ammo lists (possibly empty) and appends (slot, revealLeft), 0-terminated.
/// </summary>
public sealed class NavalState : ISimStatePart
{
    private const byte Version = 1;
    private const byte VersionHangar = 2;
    private const byte VersionAmmo = 3;
    private const byte VersionReveal = 4;

    /// <summary><see cref="AirState"/>: the aircraft sits in its carrier's hangar (layer Passenger, rearming and repairing).</summary>
    public const byte AirDocked = 1;
    /// <summary><see cref="AirState"/>: the aircraft is flying a sortie from its carrier.</summary>
    public const byte AirOnSortie = 2;
    /// <summary><see cref="AirState"/>: the aircraft is flying back to its carrier to dock.</summary>
    public const byte AirReturning = 3;

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
    /// <summary>Ticks left in which a dived submarine that has just fired stays visible and targetable, per vehicle slot (D-205, FACTION-LE-05).</summary>
    public readonly int[] RevealLeft;
    /// <summary>Carrier vehicle slot an aircraft holds a hangar place in, per vehicle slot (0 = none).</summary>
    public readonly int[] HangarOf;
    /// <summary>Hangar state per aircraft slot (0 = none, else <see cref="AirDocked"/>, <see cref="AirOnSortie"/>, <see cref="AirReturning"/>).</summary>
    public readonly byte[] AirState;
    /// <summary>Ticks of flight left on the aircraft's current sortie, per vehicle slot.</summary>
    public readonly int[] SortieLeft;

    /// <summary>Rounds left in the primary weapon, per vehicle slot; only meaningful for a type with <c>Ammo &gt; 0</c> (D-156).</summary>
    public readonly int[] AmmoLeft;
    /// <summary>Rearm progress per vehicle slot: grows by the type's Ammo each tick while rearming; a round returns at RearmTicks (D-156).</summary>
    public readonly int[] RearmProgress;

    /// <summary>Counts loads of this part; the region cache recomputes when it changes (not saved).</summary>
    internal int LoadEpoch { get; private set; }

    /// <summary>Creates an all-zero part sized by the setup's map and store limits.</summary>
    public NavalState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        NavalRegion = new ushort[setup.Map.Width * setup.Map.Height];
        int v = setup.Limits.Vehicles;
        UnloadTicksLeft = new int[v]; UnloadX = new int[v]; UnloadY = new int[v]; Submerged = new bool[v];
        HangarOf = new int[v]; AirState = new byte[v]; SortieLeft = new int[v];
        AmmoLeft = new int[v]; RearmProgress = new int[v]; RevealLeft = new int[v];
    }

    /// <inheritdoc />
    public string Name => "naval";

    /// <summary>Reset a vehicle slot (spawn / removal).</summary>
    public void ClearVehicle(int i)
    {
        UnloadTicksLeft[i] = 0; UnloadX[i] = 0; UnloadY[i] = 0; Submerged[i] = false;
        HangarOf[i] = 0; AirState[i] = 0; SortieLeft[i] = 0;
        AmmoLeft[i] = 0; RearmProgress[i] = 0; RevealLeft[i] = 0;
    }

    private bool HasHangarData(int i) => HangarOf[i] != 0 || AirState[i] != 0 || SortieLeft[i] != 0;

    private bool HasAmmoData(int i) => AmmoLeft[i] != 0 || RearmProgress[i] != 0;

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        bool hangar = false;
        for (int i = 1; i < HangarOf.Length && !hangar; i++) hangar = HasHangarData(i);
        bool ammo = false;
        for (int i = 1; i < AmmoLeft.Length && !ammo; i++) ammo = HasAmmoData(i);
        bool reveal = false;
        for (int i = 1; i < RevealLeft.Length && !reveal; i++) reveal = RevealLeft[i] != 0;
        w.Write(reveal ? VersionReveal : ammo ? VersionAmmo : hangar ? VersionHangar : Version);
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
        if (!hangar && !ammo && !reveal) return;
        for (int i = 1; i < HangarOf.Length; i++)
        {
            if (!HasHangarData(i)) continue;
            w.Write(i); w.Write(HangarOf[i]); w.Write(AirState[i]); w.Write(SortieLeft[i]);
        }
        w.Write(0);
        if (!ammo && !reveal) return;
        for (int i = 1; i < AmmoLeft.Length; i++)
        {
            if (!HasAmmoData(i)) continue;
            w.Write(i); w.Write(AmmoLeft[i]); w.Write(RearmProgress[i]);
        }
        w.Write(0);
        if (!reveal) return;
        for (int i = 1; i < RevealLeft.Length; i++)
        {
            if (RevealLeft[i] == 0) continue;
            w.Write(i); w.Write(RevealLeft[i]);
        }
        w.Write(0);
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        byte version = r.ReadByte();
        if (version != Version && version != VersionHangar && version != VersionAmmo && version != VersionReveal) throw new InvalidDataException("naval version mismatch.");
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
        Array.Clear(HangarOf); Array.Clear(AirState); Array.Clear(SortieLeft); Array.Clear(AmmoLeft); Array.Clear(RearmProgress); Array.Clear(RevealLeft);
        if (version < VersionHangar) return;
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            int s = StateIo.Slot(i, HangarOf.Length, Name);
            int h = r.ReadInt32();
            if (h < 0 || h >= HangarOf.Length) throw new InvalidDataException("naval hangar slot out of range.");
            HangarOf[s] = h; AirState[s] = r.ReadByte(); SortieLeft[s] = r.ReadInt32();
        }
        if (version < VersionAmmo) return;
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            int s = StateIo.Slot(i, AmmoLeft.Length, Name);
            AmmoLeft[s] = r.ReadInt32(); RearmProgress[s] = r.ReadInt32();
        }
        if (version < VersionReveal) return;
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            int s = StateIo.Slot(i, RevealLeft.Length, Name);
            RevealLeft[s] = r.ReadInt32();
        }
    }
}
