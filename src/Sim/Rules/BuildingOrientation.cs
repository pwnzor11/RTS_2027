// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Rules;

/// <summary>
/// M13-34: the single rule that decides how a building's footprint-relative exit or dock is turned on a given map (M4-16 half turn,
/// S22 M13-19 mirror maps). A pure function of public map geometry, shared by the sim (production exits, refinery docks) and the AI's
/// placement lanes, so the two can never drift. Integer only; no state.
/// </summary>
public static class BuildingOrientation
{
    /// <summary>
    /// A footprint-relative cell (a factory exit or a refinery dock, authored for a building facing south) as it applies to a building at
    /// <paramref name="topLeft"/>: unchanged in the north half of the map (or on the middle row), turned by 180 degrees inside the
    /// footprint in the south half; on a two-start mirror map the partner base is a reflection instead (see the sim comments).
    /// </summary>
    public static CellCoord Oriented(CellCoord rel, int footprintW, int footprintH, CellCoord topLeft, int mapW, int mapH, IReadOnlyList<CellCoord> starts)
    {
        int w = mapW, h = mapH;
        IReadOnlyList<CellCoord> sp = starts;
        if (sp.Count == 2 && !(sp[0].X + sp[1].X == w - 1 && sp[0].Y + sp[1].Y == h - 1))
        {
            if (sp[0].Y == sp[1].Y && sp[0].X + sp[1].X == w - 1 && topLeft.X * 2 + footprintW > w)
            {
                var westTl = new CellCoord(w - topLeft.X - footprintW, topLeft.Y);
                CellCoord west = HalfTurn(rel, footprintW, footprintH, westTl, h);
                return new CellCoord(footprintW - 1 - west.X, west.Y);
            }
            if (sp[0].X == sp[1].X && sp[0].Y + sp[1].Y == h - 1)
                return topLeft.Y * 2 + footprintH <= h ? rel : new CellCoord(rel.X, footprintH - 1 - rel.Y);
        }
        return HalfTurn(rel, footprintW, footprintH, topLeft, h);
    }

    private static CellCoord HalfTurn(CellCoord rel, int footprintW, int footprintH, CellCoord topLeft, int mapH)
    {
        if (topLeft.Y * 2 + footprintH <= mapH) return rel;
        return new CellCoord(footprintW - 1 - rel.X, footprintH - 1 - rel.Y);
    }
}
