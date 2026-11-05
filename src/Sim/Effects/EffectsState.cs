// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Effects;

/// <summary>
/// The "effects" state part (M3-01 skeleton; filled by M3-06 auras, M3-05 stealth, M3-13 superweapon charge). Appended after
/// "ground". All zero by default (M6-02: layout version 2 adds withdraw flags and storms, see below); slot 0 of each array is the reserved "none" slot.
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
/// <para>Version 2 (M6-02, docs/design/10_m6_plan.md §3.3) appends, after the version-1 body: (slot, flag) runs for
/// <see cref="VehicleWithdraw"/> and <see cref="InfantryWithdraw"/> (raider leaving after a plunder), then the storm table
/// (<see cref="MaxStorms"/> fixed slots, written as (slot + 1, centre x, centre y, radius, ticks left, owner) for every slot with any
/// non-zero field, 0-terminated). A storm is live while <see cref="StormTicks"/> &gt; 0. Version 1 data still reads: the new fields come
/// back empty.</para>
/// <para>Version 3 (M7-12, Salvo Array) is written only while a salvo is in flight (so every other game saves and hashes as version 2):
/// after the version-2 body, the salvo table (<see cref="MaxSalvos"/> fixed slots, written as (slot + 1, x, y, owner, building type + 1,
/// ticks elapsed) for every live slot, 0-terminated). Versions 1 and 2 read with no salvos.</para>
/// </summary>
public sealed class EffectsState : ISimStatePart
{
    private const byte Version = 3;

    /// <summary>Fixed salvo capacity (Salvo Array, M7-12).</summary>
    public const int MaxSalvos = 4;

    /// <summary>Fixed storm capacity (The Second Drift, M6-07).</summary>
    public const int MaxStorms = 8;

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
    /// <summary>1 while a raider vehicle is withdrawing, per vehicle slot (v2; M6-05).</summary>
    public readonly byte[] VehicleWithdraw;
    /// <summary>1 while a raider soldier is withdrawing, per infantry slot (v2; M6-05).</summary>
    public readonly byte[] InfantryWithdraw;
    /// <summary>Storm centre cell X per storm slot (v2; M6-07).</summary>
    public readonly int[] StormX = new int[MaxStorms];
    /// <summary>Storm centre cell Y per storm slot.</summary>
    public readonly int[] StormY = new int[MaxStorms];
    /// <summary>Storm radius in cells per storm slot.</summary>
    public readonly int[] StormRadius = new int[MaxStorms];
    /// <summary>Ticks left per storm slot (0 = the slot is free).</summary>
    public readonly int[] StormTicks = new int[MaxStorms];
    /// <summary>Owning player per storm slot.</summary>
    public readonly byte[] StormOwner = new byte[MaxStorms];

    /// <summary>Target cell X per salvo slot (v3; M7-12).</summary>
    public readonly int[] SalvoX = new int[MaxSalvos];
    /// <summary>Target cell Y per salvo slot.</summary>
    public readonly int[] SalvoY = new int[MaxSalvos];
    /// <summary>Owning player per salvo slot.</summary>
    public readonly byte[] SalvoOwner = new byte[MaxSalvos];
    /// <summary>Building type index + 1 of the firing superweapon per salvo slot (0 = the slot is free).</summary>
    public readonly int[] SalvoType = new int[MaxSalvos];
    /// <summary>Ticks since the salvo was ordered, per slot.</summary>
    public readonly int[] SalvoElapsed = new int[MaxSalvos];

    /// <summary>True while any salvo is in flight.</summary>
    public bool AnySalvo { get { for (int s = 0; s < MaxSalvos; s++) if (SalvoUsed(s)) return true; return false; } }

    private bool SalvoUsed(int s) => SalvoType[s] != 0 || SalvoX[s] != 0 || SalvoY[s] != 0 || SalvoOwner[s] != 0 || SalvoElapsed[s] != 0;

    /// <summary>Creates an all-zero part sized by the setup's store limits.</summary>
    public EffectsState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int v = setup.Limits.Vehicles, n = setup.Limits.Infantry;
        VehicleRofPermille = new int[v]; VehicleSpeedPermille = new int[v]; VehicleStill = new int[v];
        InfantryRofPermille = new int[n]; InfantrySpeedPermille = new int[n]; InfantryStill = new int[n];
        BuildingCharge = new int[setup.Limits.Buildings];
        VehicleWithdraw = new byte[v]; InfantryWithdraw = new byte[n];
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
        if (kind == ActorKind.Vehicle) { VehicleRofPermille[i] = 0; VehicleSpeedPermille[i] = 0; VehicleStill[i] = 0; VehicleWithdraw[i] = 0; }
        else if (kind == ActorKind.Infantry) { InfantryRofPermille[i] = 0; InfantrySpeedPermille[i] = 0; InfantryStill[i] = 0; InfantryWithdraw[i] = 0; }
    }

    /// <summary>Reset a building slot (removal).</summary>
    public void ClearBuilding(int i) => BuildingCharge[i] = 0;

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        bool salvo = AnySalvo;
        w.Write(salvo ? Version : (byte)2);
        w.Write(VehicleStill.Length); w.Write(InfantryStill.Length); w.Write(BuildingCharge.Length);
        WriteMobiles(w, VehicleRofPermille, VehicleSpeedPermille, VehicleStill);
        WriteMobiles(w, InfantryRofPermille, InfantrySpeedPermille, InfantryStill);
        for (int i = 1; i < BuildingCharge.Length; i++) if (BuildingCharge[i] != 0) { w.Write(i); w.Write(BuildingCharge[i]); }
        w.Write(0);
        // version 2 (M6-02)
        WriteFlags(w, VehicleWithdraw);
        WriteFlags(w, InfantryWithdraw);
        for (int s = 0; s < MaxStorms; s++)
        {
            if (StormX[s] == 0 && StormY[s] == 0 && StormRadius[s] == 0 && StormTicks[s] == 0 && StormOwner[s] == 0) continue;
            w.Write(s + 1); w.Write(StormX[s]); w.Write(StormY[s]); w.Write(StormRadius[s]); w.Write(StormTicks[s]); w.Write(StormOwner[s]);
        }
        w.Write(0);
        if (!salvo) return;
        for (int s = 0; s < MaxSalvos; s++)
        {
            if (!SalvoUsed(s)) continue;
            w.Write(s + 1); w.Write(SalvoX[s]); w.Write(SalvoY[s]); w.Write(SalvoOwner[s]); w.Write(SalvoType[s]); w.Write(SalvoElapsed[s]);
        }
        w.Write(0);
    }

    private static void WriteFlags(BinaryWriter w, byte[] flags)
    {
        for (int i = 1; i < flags.Length; i++) if (flags[i] != 0) { w.Write(i); w.Write(flags[i]); }
        w.Write(0);
    }

    private void ReadFlags(BinaryReader r, byte[] flags)
    {
        Array.Clear(flags);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32()) flags[StateIo.Slot(i, flags.Length, Name)] = r.ReadByte();
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        byte version = r.ReadByte();
        if (version < 1 || version > Version) throw new InvalidDataException("effects version mismatch.");
        if (r.ReadInt32() != VehicleStill.Length || r.ReadInt32() != InfantryStill.Length || r.ReadInt32() != BuildingCharge.Length)
            throw new InvalidDataException("effects capacity mismatch.");
        ReadMobiles(r, VehicleRofPermille, VehicleSpeedPermille, VehicleStill);
        ReadMobiles(r, InfantryRofPermille, InfantrySpeedPermille, InfantryStill);
        Array.Clear(BuildingCharge);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32()) BuildingCharge[StateIo.Slot(i, BuildingCharge.Length, Name)] = r.ReadInt32();
        Array.Clear(VehicleWithdraw); Array.Clear(InfantryWithdraw);
        Array.Clear(SalvoX); Array.Clear(SalvoY); Array.Clear(SalvoOwner); Array.Clear(SalvoType); Array.Clear(SalvoElapsed);
        Array.Clear(StormX); Array.Clear(StormY); Array.Clear(StormRadius); Array.Clear(StormTicks); Array.Clear(StormOwner);
        if (version == 1) return;   // M6-02: version-1 data has no raiders and no storms
        ReadFlags(r, VehicleWithdraw);
        ReadFlags(r, InfantryWithdraw);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            if (i > MaxStorms) throw new InvalidDataException("effects storm slot out of range.");
            int s = i - 1;
            StormX[s] = r.ReadInt32(); StormY[s] = r.ReadInt32(); StormRadius[s] = r.ReadInt32(); StormTicks[s] = r.ReadInt32();
            StormOwner[s] = r.ReadByte();
        }
        if (version < 3) return;
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            if (i > MaxSalvos) throw new InvalidDataException("effects salvo slot out of range.");
            int s = i - 1;
            SalvoX[s] = r.ReadInt32(); SalvoY[s] = r.ReadInt32(); SalvoOwner[s] = r.ReadByte(); SalvoType[s] = r.ReadInt32(); SalvoElapsed[s] = r.ReadInt32();
        }
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
