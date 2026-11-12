// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Render;

/// <summary>
/// Pure animation curves for water (M10-13): a glint that fades in and out at a new spot each cycle, and the pulsing width of the shore
/// foam. All of it depends only on the cell and the tick (a hash, no RNG state), so every client and every frame agrees and nothing is stored.
/// </summary>
public static class WaterWaves
{
    /// <summary>Ticks per glint cycle (3 s at 20 ticks a second).</summary>
    public const int GlintPeriod = 60;

    /// <summary>Peak opacity of a glint (0..255).</summary>
    public const byte GlintMaxAlpha = 46;

    /// <summary>Ticks per foam pulse.</summary>
    public const int FoamPeriod = 50;

    /// <summary>Thinnest foam strip, in zoom-1 pixels.</summary>
    public const int FoamMinPx = 2;

    /// <summary>Widest foam strip, in zoom-1 pixels.</summary>
    public const int FoamMaxPx = 7;

    /// <summary>Glints per water cell.</summary>
    public const int GlintsPerCell = 2;

    /// <summary>A small stable hash of a cell and a salt.</summary>
    public static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)x * 73856093u ^ (uint)y * 19349663u ^ (uint)salt * 83492791u;
        h ^= h >> 13;
        h *= 0x5bd1e995u;
        h ^= h >> 15;
        return h;
    }

    /// <summary>Rising then falling 0..1000 over <paramref name="period"/> ticks, starting at <paramref name="offset"/>.</summary>
    public static int Triangle(int tick, int offset, int period)
    {
        int p = (tick + offset) % period;
        int half = period / 2;
        int up = p < half ? p : period - p;
        return up * 1000 / half;
    }

    /// <summary>
    /// Glint number <paramref name="index"/> of a water cell at <paramref name="tick"/>: its opacity (0 = not visible now) and its
    /// rectangle in the cell's zoom-1 pixels (64 x 64). Each cycle puts the glint somewhere new and drifts it slowly sideways.
    /// </summary>
    public static byte Glint(int cellX, int cellY, int index, int tick, out Rectangle rect)
    {
        int offset = (int)(Hash(cellX, cellY, index) % GlintPeriod);
        int cycle = (tick + offset) / GlintPeriod;
        uint h = Hash(cellX, cellY, 1000 + index * 7919 + cycle);
        int w = 14 + (int)(h % 18);
        int x = (int)((h >> 5) % (uint)(Camera.CellPx - w)) + ((tick + offset) % GlintPeriod) / 12;
        int y = 4 + (int)((h >> 12) % (uint)(Camera.CellPx - 10));
        x = Math.Min(x, Camera.CellPx - w);
        rect = new Rectangle(x, y, w, 2);
        return (byte)(GlintMaxAlpha * Triangle(tick, offset, GlintPeriod) / 1000);
    }

    /// <summary>Foam strip width for a shore cell at <paramref name="tick"/>, <see cref="FoamMinPx"/>..<see cref="FoamMaxPx"/>.</summary>
    public static int FoamWidthPx(int cellX, int cellY, int tick)
    {
        int offset = (int)(Hash(cellX / 2, cellY / 2, 77) % FoamPeriod);   // neighbours pulse together, so a shore reads as one wave
        return FoamMinPx + (FoamMaxPx - FoamMinPx) * Triangle(tick, offset, FoamPeriod) / 1000;
    }

    /// <summary>Foam opacity: strongest when the wave is at its widest.</summary>
    public static byte FoamAlpha(int cellX, int cellY, int tick)
    {
        int offset = (int)(Hash(cellX / 2, cellY / 2, 77) % FoamPeriod);
        return (byte)(50 + 70 * Triangle(tick, offset, FoamPeriod) / 1000);
    }
}
