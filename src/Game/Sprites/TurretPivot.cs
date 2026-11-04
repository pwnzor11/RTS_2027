// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Sprites;

/// <summary>
/// Where a turret sits on its hull, in cells, in the hull's own frame at hull facing 0: <see cref="X"/> is east, <see cref="Y"/>
/// is forward (north on the map). <see cref="Z"/> is the height above the ground; the turret sheet's origin already sits on the
/// ground point under the pivot, so the drawing code does not use it.
/// </summary>
/// <param name="X">East offset in cells at hull facing 0.</param>
/// <param name="Y">Forward offset in cells at hull facing 0.</param>
/// <param name="Z">Height above the ground in cells (informational).</param>
public readonly record struct TurretPivot(double X, double Y, double Z);
