// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Effects;

/// <summary>
/// The "effects" state part (M3-01 skeleton; filled by M3-06 auras, M3-05 stealth, M3-13 superweapon charge). Appended after
/// "ground". All zero by default; slot 0 of each array is the reserved "none" slot.
/// <list type="bullet">
/// <item><see cref="VehicleRofPermille"/> / <see cref="InfantryRofPermille"/>: aura rate-of-fire multiplier on reload time, per
/// mille; 0 = no aura (treat as 1000). Recomputed every 10 ticks by M3-06.</item>
/// <item><see cref="VehicleSpeedPermille"/> / <see cref="InfantrySpeedPermille"/>: aura speed multiplier per mille; 0 = none.</item>
/// <item><see cref="VehicleStill"/> / <see cref="InfantryStill"/>: stealth timer = ticks since the actor last moved or fired
/// (M3-05 counts up to <see cref="Rules.StealthRules.DelayTicks"/> and resets on move/fire; stealthed once it reaches it).</item>
/// <item><see cref="BuildingCharge"/>: superweapon charge ticks done per building slot (M3-13).</item>
/// </list>
/// Every mobile slot is cleared when an actor spawns or is removed, every building slot when a building is removed
/// (Simulation.M3.cs). Layout: version, capacities, then (slot, rof, speed, still) runs for vehicles and infantry and (slot,
/// charge) for buildings, each run 0-terminated and listing only non-zero slots.
/// </summary>
public sealed class EffectsState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>Aura RoF multiplier per vehicle slot (0 = none).</summary>
    public readonly int[] VehicleRofPermille;
    /// <summary>Aura speed multiplier per vehicle slot (0 = none).</summary>
    public readonly int[] VehicleSpeedPermille;
    /// <summary>Stealth timer per vehicle slot.</summary>
    public readonly int[] VehicleStill;
    /// <summary>Aura RoF multiplier per infantry slot (0 = none).</summary>
    public readonly int[] InfantryRofPermille;
    /// <summary>Aura speed multiplier per infantry slot (0 = none).</summary>
    public readonly int[] InfantrySpeedPermille;
    /// <summary>Stealth timer per infantry slot.</summary>
    public readonly int[] InfantryStill;
    /// <summary>Superweapon charge per building slot.</summary>
    public readonly int[] BuildingCharge;

    /// <summary>Creates an all-zero part sized by the setup's store limits.</summary>
    public EffectsState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int v = setup.Limits.Vehicles, n = setup.Limits.Infantry;
        VehicleRofPermille = new int[v]; VehicleSpeedPermille = new int[v]; VehicleStill = new int[v];
        InfantryRofPermille = new int[n]; InfantrySpeedPermille = new int[n]; InfantryStill = new int[n];
        BuildingCharge = new int[setup.Limits.Buildings];
    }

    /// <inheritdoc />
    public string Name => "effects";

    /// <summary>Aura RoF multiplier of a mobile slot as applied (1000 when no aura).</summary>
    public int RofPermille(ActorKind kind, int i)
    {
        int v = kind == ActorKind.Vehicle ? VehicleRofPermille[i] : kind == ActorKind.Infantry ? InfantryRofPermille[i] : 0;
        return v == 0 ? 1000 : v;
    }

    /// <summary>Aura speed multiplier of a mobile slot as applied (1000 when no aura).</summary>
    public int SpeedPermille(ActorKind kind, int i)
    {
        int v = kind == ActorKind.Vehicle ? VehicleSpeedPermille[i] : kind == ActorKind.Infantry ? InfantrySpeedPermille[i] : 0;
        return v == 0 ? 1000 : v;
    }

    /// <summary>Stealth timer of a mobile slot (0 for other kinds).</summary>
    public int StillTicks(ActorKind kind, int i) =>
        kind == ActorKind.Vehicle ? VehicleStill[i] : kind == ActorKind.Infantry ? InfantryStill[i] : 0;

    /// <summary>Reset a mobile slot (spawn / removal).</summary>
    public void ClearMobile(ActorKind kind, int i)
    {
        if (kind == ActorKind.Vehicle) { VehicleRofPermille[i] = 0; VehicleSpeedPermille[i] = 0; VehicleStill[i] = 0; }
        else if (kind == ActorKind.Infantry) { InfantryRofPermille[i] = 0; InfantrySpeedPermille[i] = 0; InfantryStill[i] = 0; }
    }

    /// <summary>Reset a building slot (removal).</summary>
    public void ClearBuilding(int i) => BuildingCharge[i] = 0;

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(VehicleStill.Length); w.Write(InfantryStill.Length); w.Write(BuildingCharge.Length);
        WriteMobiles(w, VehicleRofPermille, VehicleSpeedPermille, VehicleStill);
        WriteMobiles(w, InfantryRofPermille, InfantrySpeedPermille, InfantryStill);
        for (int i = 1; i < BuildingCharge.Length; i++) if (BuildingCharge[i] != 0) { w.Write(i); w.Write(BuildingCharge[i]); }
        w.Write(0);
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("effects version mismatch.");
        if (r.ReadInt32() != VehicleStill.Length || r.ReadInt32() != InfantryStill.Length || r.ReadInt32() != BuildingCharge.Length)
            throw new InvalidDataException("effects capacity mismatch.");
        ReadMobiles(r, VehicleRofPermille, VehicleSpeedPermille, VehicleStill);
        ReadMobiles(r, InfantryRofPermille, InfantrySpeedPermille, InfantryStill);
        Array.Clear(BuildingCharge);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32()) BuildingCharge[StateIo.Slot(i, BuildingCharge.Length, Name)] = r.ReadInt32();
    }

    private static void WriteMobiles(BinaryWriter w, int[] rof, int[] speed, int[] still)
    {
        for (int i = 1; i < still.Length; i++)
        {
            if (rof[i] == 0 && speed[i] == 0 && still[i] == 0) continue;
            w.Write(i); w.Write(rof[i]); w.Write(speed[i]); w.Write(still[i]);
        }
        w.Write(0);
    }

    private void ReadMobiles(BinaryReader r, int[] rof, int[] speed, int[] still)
    {
        Array.Clear(rof); Array.Clear(speed); Array.Clear(still);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            int s = StateIo.Slot(i, still.Length, Name);
            rof[s] = r.ReadInt32(); speed[s] = r.ReadInt32(); still[s] = r.ReadInt32();
        }
    }
}
