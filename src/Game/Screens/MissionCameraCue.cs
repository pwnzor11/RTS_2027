// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Scripting;

namespace Rts.Game.Screens;

/// <summary>
/// Turns the mission's camera hint (<c>Camera.Focus</c>, M8-05) into one camera move: a new hint centres the view once, if it is still
/// inside its time window. After that the player moves the camera freely; the same hint never pans twice. Pure model.
/// </summary>
public sealed class MissionCameraCue
{
    private int _honouredSeq;

    /// <summary>
    /// The cell to centre on for a hint that has not been honoured yet, or null. A hint seen after its window has passed is marked honoured
    /// and skipped. A hint with no position (a dead target actor) is skipped too.
    /// </summary>
    public CellCoord? Take(MissionCameraHint hint, int tick)
    {
        if (hint.Seq <= _honouredSeq) return null;
        _honouredSeq = hint.Seq;
        if (tick >= hint.UntilTick || hint.Cell.X < 0 || hint.Cell.Y < 0) return null;
        return hint.Cell;
    }
}
