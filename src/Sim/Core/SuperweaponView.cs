// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>
/// One superweapon building of the viewing player (M3-13), as listed in <see cref="PlayerHud.Superweapons"/>.
/// <see cref="Charge"/> counts up to <see cref="Total"/>; <see cref="Ready"/> when they are equal. <see cref="Paused"/> is true while
/// low power stops the charge. <see cref="Range"/> is the entrance range in cells (0 = no entrance needed).
/// </summary>
public readonly record struct SuperweaponView(EntityId Building, int TypeIndex, string Superweapon, int Charge, int Total,
    bool Ready, bool Paused, int Range);
