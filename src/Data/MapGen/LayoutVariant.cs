// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

/// <summary>
/// Per-seed variety of layout 2 (M12-07 rework): which skeleton a 2-player seed uses (mirror-diagonal bases, or a half-turn skeleton with an S-shaped
/// river) and which of the eight square symmetries turns the finished canonical map. Everything is a pure function of the params seed, so a map file
/// (which keeps its seed) can be checked in its canonical frame again. The turn keeps the declared symmetry: a half-turn map stays half-turn
/// symmetric under any of the eight, a four-player map keeps its Klein group, and a mirror-diagonal 2-player map only takes the identity or the half turn.
/// </summary>
internal static class LayoutVariant
{
    /// <summary>The skeleton and the turn for a seed (<paramref name="allowRot2"/> false: always the mirror-diagonal skeleton): <c>Rot2</c> selects the half-turn skeleton (2 players, Medium and up), <c>T</c> is 0..7 (bit 0 flips x, bit 1 flips y, bit 2 transposes first).</summary>
    public static (bool Rot2, int T) For(ulong seed, int players, int n, bool allowRot2 = true)
    {
        var r = new SimRandom(unchecked(seed * 0x9E3779B97F4A7C15UL + 12345UL));
        bool rot2 = players == 2 && n >= 96 && allowRot2;   // v5: every Medium and Large 2-player seed is half-turn (a mirror skeleton measured 67-70 % wins for start 1 in two biomes); Ash Plains needs two different crossings, which a half turn would copy
        int t = rot2 || players > 2 ? r.Next(8) : (r.Next(2) == 0 ? 0 : 3);
        return (rot2, t);
    }

    /// <summary>Canonical cell to its place after turn <paramref name="t"/>.</summary>
    public static (int X, int Y) Fwd(int t, int n, int x, int y)
    {
        if ((t & 4) != 0) (x, y) = (y, x);
        if ((t & 1) != 0) x = n - 1 - x;
        if ((t & 2) != 0) y = n - 1 - y;
        return (x, y);
    }

    /// <summary>The cell of the canonical map that turn <paramref name="t"/> puts at (x, y).</summary>
    public static (int X, int Y) Inv(int t, int n, int x, int y)
    {
        if ((t & 2) != 0) y = n - 1 - y;
        if ((t & 1) != 0) x = n - 1 - x;
        if ((t & 4) != 0) (x, y) = (y, x);
        return (x, y);
    }

    /// <summary>Turns a map with turn <paramref name="t"/>; the starts keep their order.</summary>
    public static MapData Turn(MapData m, int t, Func<int, int, (int X, int Y)> mapCell, IReadOnlyList<CellCoord> starts)
    {
        int n = m.Width, total = n * n;
        var tpl = new ushort[total]; var land = new LandType[total]; var height = new byte[total]; var overlay = new OverlayKind[total]; var dens = new byte[total];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                (int fx, int fy) = mapCell(x, y);
                int s = y * n + x, d = fy * n + fx;
                tpl[d] = m.Template[s]; land[d] = m.Land[s]; height[d] = m.HeightLevel[s]; overlay[d] = m.Overlay[s]; dens[d] = m.Density[s];
            }
        return new MapData(m.Name, m.Theater, n, n, tpl, land, height, overlay, dens, starts);
    }

    /// <summary>Turns a map and its starts forward by <paramref name="t"/>.</summary>
    public static MapData Forward(MapData m, int t, IReadOnlyList<CellCoord> starts)
    {
        int n = m.Width;
        return Turn(m, t, (x, y) => Fwd(t, n, x, y), MapCells(starts, n, (x, y) => Fwd(t, n, x, y)));
    }

    /// <summary>Undoes turn <paramref name="t"/> (the canonical frame of the checks).</summary>
    public static MapData Backward(MapData m, int t)
    {
        int n = m.Width;
        return Turn(m, t, (x, y) => Inv(t, n, x, y), MapCells(m.StartPositions, n, (x, y) => Inv(t, n, x, y)));
    }

    /// <summary>Maps a cell list through <paramref name="f"/>.</summary>
    public static CellCoord[] MapCells(IReadOnlyList<CellCoord> cells, int n, Func<int, int, (int X, int Y)> f)
    {
        var r = new CellCoord[cells.Count];
        for (int i = 0; i < r.Length; i++) { (int x, int y) = f(cells[i].X, cells[i].Y); r[i] = new CellCoord(x, y); }
        return r;
    }

    /// <summary>Maps feature footprints (square, so the corner is the minimum of the two mapped corners).</summary>
    public static MapFeature[] MapFeatures(IReadOnlyList<MapFeature> features, int n, Func<int, int, (int X, int Y)> f)
    {
        var r = new MapFeature[features.Count];
        for (int i = 0; i < r.Length; i++)
        {
            (int fw, int fh) = MapAnalysis.FootprintOf(features[i].Type);
            (int ax, int ay) = f(features[i].Cell.X, features[i].Cell.Y);
            (int bx, int by) = f(features[i].Cell.X + fw - 1, features[i].Cell.Y + fh - 1);
            r[i] = new MapFeature(features[i].Type, new CellCoord(Math.Min(ax, bx), Math.Min(ay, by)));
        }
        return r;
    }
}
