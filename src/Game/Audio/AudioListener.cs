// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Audio;

/// <summary>
/// Where the player is looking, for positional sound. The screen that owns the camera sets <see cref="Position"/> each frame
/// (see <see cref="SetFromWorldPixels"/>); until it does, positional sounds play at full volume, centred.
/// </summary>
public static class AudioListener
{
    /// <summary>The camera centre in leptons, or null when no screen has set it.</summary>
    public static LeptonCoord? Position { get; set; }

    /// <summary>Sets the position from a world-pixel point, given the pixels per cell the renderer uses.</summary>
    public static void SetFromWorldPixels(float x, float y, float cellPx) =>
        Position = new LeptonCoord((int)(x / cellPx * SimConstants.LeptonsPerCell), (int)(y / cellPx * SimConstants.LeptonsPerCell));
}
