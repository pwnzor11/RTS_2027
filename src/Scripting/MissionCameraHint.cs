// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Scripting;

/// <summary>
/// A camera hint from <c>Camera.Focus</c> (M8-05): the game layer may pan to <see cref="Cell"/> once, while the tick is before
/// <see cref="UntilTick"/>. An output only: the sim never reads it, it is not saved and not hashed. The latest call wins.
/// <see cref="Seq"/> 0 means no hint yet. <see cref="Cell"/> is (-1, -1) when the target actor is not alive (nothing to point at).
/// </summary>
/// <param name="Seq">Ever-increasing number (per host); a new value means a new hint.</param>
/// <param name="Target">Zone name or actor tag, as the script named it.</param>
/// <param name="Cell">The cell to centre on: the zone's middle cell, or the actor's cell.</param>
/// <param name="UntilTick">Sim tick at which the hint ends.</param>
public readonly record struct MissionCameraHint(int Seq, string Target, CellCoord Cell, int UntilTick);
