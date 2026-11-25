// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// The maths of the water shimmer (design 18 section 3, water row; M16-V-04): a slow diagonal band of sparkles that sweeps across the water,
/// a caustic-like glitter that sits on top of the existing glints and foam of <see cref="WaterOverlay"/>. A cell's brightness depends only on
/// its place and the tick; which cells are lit skips water the viewer cannot see now. View only.
/// </summary>
public static class WaterShimmer
{
    /// <summary>Width of the band cycle in band units (a cell is <c>2x + 3y</c> units along the sweep).</summary>
    public const int BandPeriod = 80;

    /// <summary>Half width of the lit band in band units; a cell is fully lit on the centre line and dark at this distance.</summary>
    public const int BandHalfWidth = 7;

    /// <summary>Ticks the band takes to move one unit (so it crosses a 40-cell map in about 10 s).</summary>
    public const int TicksPerStep = 3;

    /// <summary>Opacity of a sparkle on the band's centre line, 0..255. ASSUMED.</summary>
    public const byte MaxAlpha = 70;

    /// <summary>Sparkles in a lit cell.</summary>
    public const int SparklesPerCell = 3;

    /// <summary>Ticks a sparkle stays in one spot before it hops.</summary>
    public const int HopTicks = 30;

    private const ulong Seed = 0x5348_494DUL;

    /// <summary>A lit water cell.</summary>
    /// <param name="X">Cell x.</param>
    /// <param name="Y">Cell y.</param>
    /// <param name="Alpha">Peak sparkle opacity in this cell, 1..<see cref="MaxAlpha"/>.</param>
    public readonly record struct LitCell(int X, int Y, byte Alpha);

    /// <summary>Opacity (0..<see cref="MaxAlpha"/>) of the band at cell (<paramref name="x"/>, <paramref name="y"/>) at <paramref name="tick"/>; 0 = off the band.</summary>
    public static byte BandAlpha(int x, int y, int tick)
    {
        int u = Mod(2 * x + 3 * y, BandPeriod);
        int centre = Mod(tick / TicksPerStep, BandPeriod);
        int d = Math.Abs(u - centre);
        d = Math.Min(d, BandPeriod - d);
        return d >= BandHalfWidth ? (byte)0 : (byte)(MaxAlpha * (BandHalfWidth - d) / BandHalfWidth);
    }

    /// <summary>
    /// Appends the lit water cells of the inclusive cell range to <paramref name="lit"/>. A cell counts when it is water or shallow water, on
    /// the band now, and the viewer sees it now (<paramref name="vision"/> is 2); with no vision data (omniscient view) every water cell qualifies.
    /// </summary>
    public static void Select(IReadOnlyList<LandType> land, int width, int height, IReadOnlyList<byte>? vision, int x0, int y0, int x1, int y1, int tick, List<LitCell> lit)
    {
        bool fog = vision is not null && vision.Count == width * height;
        for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
            {
                int k = y * width + x;
                if (!WaterShore.IsWater(land[k])) continue;
                if (fog && vision![k] != 2) continue;
                byte a = BandAlpha(x, y, tick);
                if (a > 0) lit.Add(new LitCell(x, y, a));
            }
        }
    }

    /// <summary>Sparkle <paramref name="index"/> of a cell: a short dash in the cell's own zoom-1 pixels, hopping to a new place every <see cref="HopTicks"/>.</summary>
    public static Rectangle Sparkle(int cellX, int cellY, int index, int tick)
    {
        int hop = tick / HopTicks;
        uint h = DecorHash.Mix(Seed, cellX * 7 + index, cellY, (uint)hop + 1000u * (uint)index);
        int w = 4 + (int)(h % 5);
        int x = (int)((h >> 4) % (uint)(Camera.CellPx - w));
        int y = 3 + (int)((h >> 14) % (uint)(Camera.CellPx - 7));
        return new Rectangle(x, y, w, 2);
    }

    /// <summary>The sparkle colour: the theater's light water shade pushed 45 % toward white, so it stays in the palette.</summary>
    public static TerrainPalette.Rgb ColourFor(TerrainPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        TerrainPalette.Rgb light = palette.ShadesFor(LandType.Water).Light;
        return TerrainBake.Mix(light, new TerrainPalette.Rgb(255, 255, 255), 450);
    }

    private static int Mod(int v, int m)
    {
        int r = v % m;
        return r < 0 ? r + m : r;
    }
}
