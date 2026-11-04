// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Audio;

/// <summary>Volume and pan of a sound heard from a place, relative to the listener (the camera centre).</summary>
public static class Spatializer
{
    /// <summary>Widest pan used, so a sound at the edge is not heard in one ear only.</summary>
    public const float MaxPan = 0.85f;

    /// <summary>
    /// Gain 0..1 falls off with distance (full inside a quarter of <paramref name="hearingCells"/>, then linearly to silence at the
    /// radius); pan -1..1 follows the horizontal offset (<paramref name="panCells"/> pans fully). Game-side float maths: this never
    /// runs in the sim.
    /// </summary>
    public static (float Gain, float Pan) Compute(LeptonCoord listener, LeptonCoord source, float hearingCells, float panCells)
    {
        float dx = (source.X - listener.X) / (float)SimConstants.LeptonsPerCell;
        float dy = (source.Y - listener.Y) / (float)SimConstants.LeptonsPerCell;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float near = hearingCells * 0.25f;
        float gain = dist <= near ? 1f : Math.Clamp(1f - (dist - near) / (hearingCells - near), 0f, 1f);
        float pan = Math.Clamp(dx / panCells, -1f, 1f) * MaxPan;
        return (gain, pan);
    }
}
