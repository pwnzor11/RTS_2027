// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Sprites;

/// <summary>
/// One named animation state of a sprite: a block of frames in the sheet laid out facing-major. The frame for
/// facing <c>f</c> and animation step <c>k</c> is <c>First + f * Frames + k</c>, row-major in the sheet.
/// </summary>
/// <param name="Name">State name, unique within the sprite (for example "idle", "damaged", "fire").</param>
/// <param name="Facings">Number of facings in the block: 1, 8, 16 or 32.</param>
/// <param name="Frames">Animation frames per facing, at least 1.</param>
/// <param name="First">Index of the block's first frame in the sheet, row-major from 0.</param>
/// <param name="TicksPerFrame">Simulation ticks each animation frame is shown for, at least 1.</param>
/// <param name="Loop">True (the default) when the animation wraps; false holds the last frame (one-shot effects).</param>
public sealed record SpriteState(string Name, int Facings, int Frames, int First, int TicksPerFrame, bool Loop = true)
{
    /// <summary>Number of sheet cells this state occupies.</summary>
    public int CellCount => Facings * Frames;
}
