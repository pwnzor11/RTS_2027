// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>
/// One superweapon building of the viewing player (M3-13), as listed in <see cref="PlayerHud.Superweapons"/>.
/// <see cref="Charge"/> counts up to <see cref="Total"/>; <see cref="Ready"/> when they are equal. <see cref="Paused"/> is true while
/// low power stops the charge. <see cref="Range"/> is the entrance range in cells (0 = no entrance needed).
/// </summary>
public readonly record struct SuperweaponView(EntityId Building, int TypeIndex, string Superweapon, int Charge, int Total,
    bool Ready, bool Paused, int Range)
{
    /// <summary>M3-21: blast radius in cells (Chebyshev), for the targeting circle. Rules constant of the sim (<c>SuperweaponBlastRadius</c>).</summary>
    public int BlastRadius { get; init; }

    /// <summary>M3-21: the player slot that owns the building (the HUD list is the viewer's own, the snapshot list can hold several players).</summary>
    public byte Owner { get; init; }
}
