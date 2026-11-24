// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Superweapon;

/// <summary>The cells of one own entrance building: its top-left cell and footprint.</summary>
/// <param name="X">Top-left cell column.</param>
/// <param name="Y">Top-left cell row.</param>
/// <param name="W">Footprint width in cells.</param>
/// <param name="H">Footprint height in cells.</param>
public readonly record struct EntranceFootprint(int X, int Y, int W, int H);

/// <summary>
/// The legal area of an entrance-range superweapon (A43 GAMEUI-03), read from the snapshot: a cell is in range when it lies within
/// <c>range</c> cells of an own entrance footprint, the same distance the sim uses (Chebyshev distance from the footprint). Pure, so it
/// is unit-tested. The sim stays the judge of a click; this only draws the area and names the refusal.
/// </summary>
public static class SuperweaponRange
{
    /// <summary>Cells from <paramref name="cell"/> to the nearest cell of <paramref name="origin"/> (0 when inside it).</summary>
    public static int Distance(EntranceFootprint origin, CellCoord cell)
    {
        int dx = Math.Max(Math.Max(origin.X - cell.X, cell.X - (origin.X + origin.W - 1)), 0);
        int dy = Math.Max(Math.Max(origin.Y - cell.Y, cell.Y - (origin.Y + origin.H - 1)), 0);
        return Math.Max(dx, dy);
    }

    /// <summary>True when <paramref name="cell"/> is in range of any origin. A range of 0 or less means anywhere (no entrance needed).</summary>
    public static bool Covers(IReadOnlyList<EntranceFootprint> origins, int range, CellCoord cell)
    {
        if (range <= 0) return true;
        foreach (EntranceFootprint o in origins)
            if (Distance(o, cell) <= range) return true;
        return false;
    }

    /// <summary>The footprints of the viewing player's own live entrance buildings (ghosts and other players' buildings left out).</summary>
    /// <param name="buildings">The snapshot's buildings.</param>
    /// <param name="player">The viewing player.</param>
    /// <param name="isEntranceType">True for a building type index that is an entrance.</param>
    public static IReadOnlyList<EntranceFootprint> OriginsOf(IEnumerable<BuildingView> buildings, byte player, Func<int, bool> isEntranceType)
    {
        var list = new List<EntranceFootprint>();
        foreach (BuildingView b in buildings)
            if (!b.Ghost && b.Player == player && b.Health > 0 && isEntranceType(b.TypeIndex))
                list.Add(new EntranceFootprint(b.Cell.X, b.Cell.Y, b.FootprintW, b.FootprintH));
        return list;
    }
}
