// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Sim.Core;

namespace Rts.Game.Ui;

/// <summary>
/// The Static Broadcast jam on the minimap (D-077): cells the snapshot marks <see cref="GroundLayers.Jammed"/> for this viewer
/// are covered with opaque flickering static, hiding the terrain and every dot under it. View only: the sim is unaffected and the
/// main map is not jammed. Pure so it is unit-tested; <see cref="Minimap"/> uploads the result as one texel per cell.
/// </summary>
public static class MinimapJam
{
    /// <summary>Static changes every this many ticks (10 Hz at 20 Hz ticks).</summary>
    public const int FlickerTicks = 2;

    /// <summary>True when the snapshot has ground data for the whole map and the cell is jammed.</summary>
    public static bool IsJammed(SimSnapshot snapshot, int x, int y)
    {
        if (snapshot.Ground.Count != snapshot.MapWidth * snapshot.MapHeight) return false;
        if ((uint)x >= (uint)snapshot.MapWidth || (uint)y >= (uint)snapshot.MapHeight) return false;
        return (snapshot.Ground[y * snapshot.MapWidth + x] & GroundLayers.Jammed) != 0;
    }

    /// <summary>The static at a cell: an opaque grey between 38 and 133, a pure function of cell and tick.</summary>
    public static Color Noise(int x, int y, int tick)
    {
        uint h = (uint)x * 0x9E3779B1u ^ (uint)y * 0x85EBCA6Bu ^ (uint)(tick / FlickerTicks) * 0xC2B2AE35u;
        h ^= h >> 15;
        h *= 0x2C1B3C6Du;
        h ^= h >> 12;
        int grey = 38 + (int)(h % 96u);
        return new Color(grey, grey, grey + 6, 255);
    }

    /// <summary>
    /// Fills <paramref name="pixels"/> (row-major, one per map cell) with static on jammed cells and <see cref="Color.Transparent"/>
    /// elsewhere. Returns true if any cell is jammed, so the caller can skip drawing when none is.
    /// </summary>
    public static bool Fill(Color[] pixels, SimSnapshot snapshot)
    {
        bool any = false;
        int w = snapshot.MapWidth;
        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % w, y = i / w;
            if (IsJammed(snapshot, x, y))
            {
                pixels[i] = Noise(x, y, snapshot.Tick);
                any = true;
            }
            else pixels[i] = Color.Transparent;
        }

        return any;
    }
}
