// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>Pure counting for muzzle flashes (M10-14): how many shells left a barrel on the latest tick.</summary>
public static class JuiceMuzzle
{
    /// <summary>Most flashes started per tick, so a big battle does not flood the screen. ASSUMED.</summary>
    public const int MaxPerTick = 6;

    /// <summary>
    /// New projectiles this tick: the count grew, plus the ones that landed in the same tick (one detonation each), as the audio counts shells.
    /// The new ones are the last entries of the list.
    /// </summary>
    public static int NewShots(int previousCount, int currentCount, int landed) =>
        Math.Clamp(currentCount - previousCount + Math.Max(0, landed), 0, Math.Min(MaxPerTick, currentCount));
}
