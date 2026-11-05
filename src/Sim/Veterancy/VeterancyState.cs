// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Veterancy;

/// <summary>
/// The "veterancy" state part (VET-1, docs/design/14_veterancy.md §4, D-127). Appended after "salvage". Per vehicle slot and per infantry
/// slot: the accumulated <c>kill value</c> (credits of enemy victims killed, scaled by the kill permille) and the tick of the last combat
/// (the actor fired, or was hit). The rank is derived from the kill value and the rules table and is never stored. Slot 0 of each array
/// is the reserved "none" slot. A slot is cleared when an actor spawns into it or is removed (Simulation.M3.cs hooks), so a reused slot
/// starts at 0. Layout: version, vehicle capacity, infantry capacity, then (slot, killValue, lastCombatTick) for every vehicle slot with
/// a non-zero field, 0-terminated, then the same for infantry.
/// </summary>
public sealed class VeterancyState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>Kill value per vehicle slot.</summary>
    public readonly int[] VehicleKillValue;
    /// <summary>Tick of the last shot fired or hit taken per vehicle slot (0 = never).</summary>
    public readonly int[] VehicleLastCombat;
    /// <summary>Kill value per infantry slot.</summary>
    public readonly int[] InfantryKillValue;
    /// <summary>Tick of the last shot fired or hit taken per infantry slot (0 = never).</summary>
    public readonly int[] InfantryLastCombat;

    /// <summary>Creates an all-zero part sized by the setup's store limits.</summary>
    public VeterancyState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int v = setup.Limits.Vehicles, n = setup.Limits.Infantry;
        VehicleKillValue = new int[v]; VehicleLastCombat = new int[v];
        InfantryKillValue = new int[n]; InfantryLastCombat = new int[n];
    }

    /// <inheritdoc />
    public string Name => "veterancy";

    /// <summary>Kill value of a mobile slot (0 for other kinds).</summary>
    public int KillValue(ActorKind kind, int i) =>
        kind == ActorKind.Vehicle ? VehicleKillValue[i] : kind == ActorKind.Infantry ? InfantryKillValue[i] : 0;

    /// <summary>Last combat tick of a mobile slot (0 for other kinds).</summary>
    public int LastCombat(ActorKind kind, int i) =>
        kind == ActorKind.Vehicle ? VehicleLastCombat[i] : kind == ActorKind.Infantry ? InfantryLastCombat[i] : 0;

    /// <summary>Reset a mobile slot (spawn / removal).</summary>
    public void ClearMobile(ActorKind kind, int i)
    {
        if (kind == ActorKind.Vehicle) { VehicleKillValue[i] = 0; VehicleLastCombat[i] = 0; }
        else if (kind == ActorKind.Infantry) { InfantryKillValue[i] = 0; InfantryLastCombat[i] = 0; }
    }

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(VehicleKillValue.Length); w.Write(InfantryKillValue.Length);
        WriteRun(w, VehicleKillValue, VehicleLastCombat);
        WriteRun(w, InfantryKillValue, InfantryLastCombat);
    }

    private static void WriteRun(BinaryWriter w, int[] kills, int[] last)
    {
        for (int i = 1; i < kills.Length; i++)
        {
            if (kills[i] == 0 && last[i] == 0) continue;
            w.Write(i); w.Write(kills[i]); w.Write(last[i]);
        }
        w.Write(0);
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("veterancy version mismatch.");
        if (r.ReadInt32() != VehicleKillValue.Length || r.ReadInt32() != InfantryKillValue.Length)
            throw new InvalidDataException("veterancy capacity mismatch.");
        ReadRun(r, VehicleKillValue, VehicleLastCombat);
        ReadRun(r, InfantryKillValue, InfantryLastCombat);
    }

    private void ReadRun(BinaryReader r, int[] kills, int[] last)
    {
        Array.Clear(kills); Array.Clear(last);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            int s = StateIo.Slot(i, kills.Length, Name);
            kills[s] = r.ReadInt32(); last[s] = r.ReadInt32();
        }
    }
}
