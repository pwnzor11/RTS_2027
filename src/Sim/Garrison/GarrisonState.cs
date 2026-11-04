// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Garrison;

/// <summary>
/// The "garrison" state part (M4-01 skeleton; filled by M4-03 garrisons and M4-15 neutral map features). Appended after "effects".
/// Who sits in a building is NOT stored here: occupants are infantry with <see cref="ActorLayer.Garrisoned"/> and
/// <see cref="Tunnels.TunnelState.InfantryCarrier"/> = the building (the "tunnels" part).
/// <list type="bullet">
/// <item><see cref="HomeOwner"/>: per building slot, the owner a neutral building reverts to when its garrison empties
/// (M4-15); <see cref="NoOwner"/> = the building was not flipped. Slot 0 is the reserved "none" slot.</item>
/// </list>
/// Layout: version, building capacity, then (slot, home owner) for every slot whose value is not <see cref="NoOwner"/>, ended by 0.
/// </summary>
public sealed class GarrisonState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>"Not flipped" value of <see cref="HomeOwner"/>.</summary>
    public const byte NoOwner = 255;

    /// <summary>Original owner per building slot while a neutral building is occupied (255 = none).</summary>
    public readonly byte[] HomeOwner;

    /// <summary>Creates an empty part sized by the setup's building limit.</summary>
    public GarrisonState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        HomeOwner = new byte[setup.Limits.Buildings];
        Array.Fill(HomeOwner, NoOwner);
    }

    /// <inheritdoc />
    public string Name => "garrison";

    /// <summary>Reset a building slot (placement / removal).</summary>
    public void ClearBuilding(int i) => HomeOwner[i] = NoOwner;

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(HomeOwner.Length);
        for (int i = 1; i < HomeOwner.Length; i++) if (HomeOwner[i] != NoOwner) { w.Write(i); w.Write(HomeOwner[i]); }
        w.Write(0);
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("garrison version mismatch.");
        if (r.ReadInt32() != HomeOwner.Length) throw new InvalidDataException("garrison capacity mismatch.");
        Array.Fill(HomeOwner, NoOwner);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32()) HomeOwner[StateIo.Slot(i, HomeOwner.Length, Name)] = r.ReadByte();
    }
}
