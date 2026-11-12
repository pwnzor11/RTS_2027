// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>Which sim effect events shake the screen, and by how much (M10-14). Pure, view only.</summary>
public static class JuiceBlast
{
    /// <summary>A detonation counts as big from this splash radius in cells (the same line the audio uses for the large explosion).</summary>
    public const int LargeSplashCells = 3;

    /// <summary>Shake amplitude in pixels for one event, 0 when it is too small to shake the view. Capped by <see cref="JuiceShake.MaxAmplitudePx"/> at kick time.</summary>
    // ASSUMED (M10-14): tuned by eye; a 3x3 building death is a clear nudge, a superweapon the most.
    public static float Amplitude(FxEvent e) => e.Kind switch
    {
        FxKind.Superweapon => 5f + Math.Min(3, e.Size / 2),
        FxKind.BuildingDestroyed when e.Size >= 2 => 1.5f + e.Size,
        FxKind.Detonation when e.Size >= LargeSplashCells => 1f + 0.8f * e.Size,
        _ => 0f,
    };
}
