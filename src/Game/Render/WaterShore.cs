// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// Pure shore detection for the water animation (M10-13): which sides of a water cell touch land. Computed once per map, never per frame.
/// </summary>
public static class WaterShore
{
    /// <summary>Side bit: land to the north.</summary>
    public const byte North = 1;

    /// <summary>Side bit: land to the east.</summary>
    public const byte East = 2;

    /// <summary>Side bit: land to the south.</summary>
    public const byte South = 4;

    /// <summary>Side bit: land to the west.</summary>
    public const byte West = 8;

    /// <summary>Whether a land type is open water (deep or shallow).</summary>
    public static bool IsWater(LandType land) => land is LandType.Water or LandType.Shallow;

    /// <summary>
    /// The shore sides of one cell: a bitmask of <see cref="North"/>, <see cref="East"/>, <see cref="South"/>, <see cref="West"/> for each
    /// neighbour that is land (anything but water). 0 for a land cell, an inland water cell, or a cell outside the map. The map edge is not land.
    /// </summary>
    public static byte EdgeMask(IReadOnlyList<LandType> land, int width, int height, int x, int y)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height || !IsWater(land[y * width + x])) return 0;
        byte mask = 0;
        if (IsLand(land, width, height, x, y - 1)) mask |= North;
        if (IsLand(land, width, height, x + 1, y)) mask |= East;
        if (IsLand(land, width, height, x, y + 1)) mask |= South;
        if (IsLand(land, width, height, x - 1, y)) mask |= West;
        return mask;
    }

    /// <summary>The shore mask of every cell, row-major.</summary>
    public static byte[] Build(IReadOnlyList<LandType> land, int width, int height)
    {
        var masks = new byte[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                masks[y * width + x] = EdgeMask(land, width, height, x, y);
        return masks;
    }

    private static bool IsLand(IReadOnlyList<LandType> land, int width, int height, int x, int y) =>
        (uint)x < (uint)width && (uint)y < (uint)height && !IsWater(land[y * width + x]);
}
