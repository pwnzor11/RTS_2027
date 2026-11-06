// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>
/// A camera hint from <c>Camera.Focus</c> (M8-05): the game layer may pan to <see cref="Target"/> until <see cref="UntilTick"/>. An
/// output only: the sim never reads it, it is not saved and not hashed. <see cref="Seq"/> 0 means no hint yet.
/// </summary>
/// <param name="Seq">Ever-increasing number; a new value means a new hint.</param>
/// <param name="Target">Zone name or actor tag.</param>
/// <param name="UntilTick">Sim tick at which the hint ends.</param>
public readonly record struct MissionCameraHint(int Seq, string Target, int UntilTick);
