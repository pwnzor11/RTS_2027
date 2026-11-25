// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Numerics;

namespace Rts.Game.Sprites;

/// <summary>
/// The optional <c>[anchors]</c> table of a vehicle manifest (BACKLOG V-05, written by <c>build_vehicle.py --anchors-only</c>): where shots
/// leave the barrel and where the dust comes up, one pixel offset per facing of the sheet's first state. Offsets are in sheet pixels from the
/// sheet's origin pixel (x right, y down); scale them like the origin (<see cref="SpriteManifest.ArtScale"/>, then the unit scale and zoom).
/// A missing table or key means "use the renderer's default offsets".
/// </summary>
public sealed class SpriteAnchors
{
    /// <summary>Anchors with neither key.</summary>
    public static readonly SpriteAnchors Empty = new(Array.Empty<(int, int)>(), Array.Empty<(int, int)>());

    private readonly (int Dx, int Dy)[] _muzzle;
    private readonly (int Dx, int Dy)[] _dust;

    internal SpriteAnchors((int, int)[] muzzle, (int, int)[] dust)
    {
        _muzzle = muzzle;
        _dust = dust;
    }

    /// <summary>True when the table gives a muzzle point per facing.</summary>
    public bool HasMuzzle => _muzzle.Length > 0;

    /// <summary>True when the table gives a dust point per facing.</summary>
    public bool HasDust => _dust.Length > 0;

    /// <summary>
    /// The muzzle point for a facing byte (0 = north, clockwise), or null when the table has none. On a body sheet the facing is the hull
    /// facing; on a <c>&lt;id&gt;_turret</c> sheet it is the turret facing and the point is relative to the turret origin.
    /// </summary>
    /// <param name="facing">Facing byte, 0..255.</param>
    public (int Dx, int Dy)? Muzzle(byte facing) => Pick(_muzzle, facing);

    /// <summary>The dust point (rear ground contact centre) for a hull facing byte, or null when the table has none.</summary>
    /// <param name="facing">Hull facing byte, 0..255.</param>
    public (int Dx, int Dy)? Dust(byte facing) => Pick(_dust, facing);

    /// <summary>Number of entries of the muzzle list (0 when absent).</summary>
    public int MuzzleCount => _muzzle.Length;

    /// <summary>Number of entries of the dust list (0 when absent).</summary>
    public int DustCount => _dust.Length;

    private static (int Dx, int Dy)? Pick((int Dx, int Dy)[] list, byte facing) =>
        list.Length == 0 ? null : list.Length == 1 ? list[0] : list[Facing.ToFacings(facing, list.Length)];
}
