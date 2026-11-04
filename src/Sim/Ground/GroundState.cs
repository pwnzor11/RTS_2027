// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Ground;

/// <summary>
/// The "ground" state part (M3-01 skeleton; filled by M3-04 hardening and M3-05 radiation/Blackout). Per map cell, row-major
/// (<c>y * Width + x</c>), all zero by default. Appended after "tunnels".
/// <list type="bullet">
/// <item><see cref="Radiation"/>: level 0..255 (design 05 §3).</item>
/// <item><see cref="Hardened"/>: bit p set = the cell is hardened by the team of player slot p (M3-04 sets the bit of every
/// member of the enclosing team). Any bit set = nothing may surface or break through there. Player slots 64 and up cannot
/// harden (<see cref="MaxHardeningPlayers"/>).</item>
/// <item><see cref="Blackout"/>: ticks left of a Blackout cloud (0 = none).</item>
/// <item><see cref="HardeningDirty"/>: a wall/gate/structure appeared or disappeared since the last enclosure recompute
/// (set by the building hooks in Simulation.M3.cs; M3-04 recomputes and clears it).</item>
/// </list>
/// Layout: version, width, height, dirty flag, then (cell + 1, radiation, hardened, blackout) for every non-zero cell,
/// ended by 0. Sparse, so an untouched map costs a few bytes in every hash.
/// </summary>
public sealed class GroundState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>Player slots that can own a hardening bit.</summary>
    public const int MaxHardeningPlayers = 64;

    /// <summary>Radiation level per cell.</summary>
    public readonly byte[] Radiation;
    /// <summary>Hardening bits per cell (bit = player slot).</summary>
    public readonly ulong[] Hardened;
    /// <summary>Blackout ticks left per cell.</summary>
    public readonly int[] Blackout;
    /// <summary>True when the enclosure must be recomputed.</summary>
    public bool HardeningDirty;

    /// <summary>Creates an all-zero part sized by the map.</summary>
    public GroundState(int width, int height)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width; Height = height;
        Radiation = new byte[width * height];
        Hardened = new ulong[width * height];
        Blackout = new int[width * height];
    }

    /// <inheritdoc />
    public string Name => "ground";

    /// <summary>Map width in cells.</summary>
    public int Width { get; }

    /// <summary>Map height in cells.</summary>
    public int Height { get; }

    /// <summary>Row-major index of an on-map cell.</summary>
    public int IndexOf(CellCoord c) => c.Y * Width + c.X;

    /// <summary>True if the cell is on the map.</summary>
    public bool Contains(CellCoord c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;

    /// <summary>Radiation level of a cell (0 off the map).</summary>
    public int RadiationAt(CellCoord c) => Contains(c) ? Radiation[IndexOf(c)] : 0;

    /// <summary>True if any team has hardened the cell.</summary>
    public bool IsHardened(CellCoord c) => Contains(c) && Hardened[IndexOf(c)] != 0;

    /// <summary>True if the team of <paramref name="player"/> hardened the cell.</summary>
    public bool IsHardenedBy(byte player, CellCoord c) =>
        player < MaxHardeningPlayers && Contains(c) && (Hardened[IndexOf(c)] & (1UL << player)) != 0;

    /// <summary>Blackout ticks left on a cell (0 off the map).</summary>
    public int BlackoutAt(CellCoord c) => Contains(c) ? Blackout[IndexOf(c)] : 0;

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(Width); w.Write(Height);
        w.Write(HardeningDirty);
        for (int i = 0; i < Radiation.Length; i++)
        {
            if (Radiation[i] == 0 && Hardened[i] == 0 && Blackout[i] == 0) continue;
            w.Write(i + 1); w.Write(Radiation[i]); w.Write(Hardened[i]); w.Write(Blackout[i]);
        }
        w.Write(0);
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("ground version mismatch.");
        if (r.ReadInt32() != Width || r.ReadInt32() != Height) throw new InvalidDataException("ground size mismatch.");
        HardeningDirty = r.ReadBoolean();
        Array.Clear(Radiation); Array.Clear(Hardened); Array.Clear(Blackout);
        for (int k = r.ReadInt32(); k != 0; k = r.ReadInt32())
        {
            int i = k - 1;
            if (i < 0 || i >= Radiation.Length) throw new InvalidDataException("ground cell out of range.");
            Radiation[i] = r.ReadByte(); Hardened[i] = r.ReadUInt64(); Blackout[i] = r.ReadInt32();
        }
    }
}
