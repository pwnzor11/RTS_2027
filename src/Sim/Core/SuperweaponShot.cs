// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>S45 C-05: one accepted superweapon shot (Special command that passed <c>CanFireSuperweapon</c>), as listed in <c>Simulation.SuperweaponShots</c>.</summary>
/// <param name="Player">The firing player.</param>
/// <param name="Building">The superweapon building.</param>
/// <param name="Cell">The target cell.</param>
public readonly record struct SuperweaponShot(byte Player, ActorRef Building, CellCoord Cell);
