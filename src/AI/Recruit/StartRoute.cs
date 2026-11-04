// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.AI.Recruit;

/// <summary>
/// M4-12: the places an enemy base can be, for scouting and for waves that know no enemy building yet. On a map with 3-4 start
/// positions the route is every other start, nearest first (equally near ones in seeded order), then the map centre, then the corners
/// not already covered. With 2 starts (or none passed) it is the M3 route: the mirror of our start, the centre, the other two corners
/// (2-player games are unchanged).
/// </summary>
internal static class StartRoute
{
    private const int SameStopCells = 8;

    /// <summary>Builds the route for an AI that started at <see cref="AiContext.StartCell"/>.</summary>
    public static List<CellCoord> For(AiContext ctx, bool centreSecond = true)
    {
        int w = ctx.MapWidth, h = ctx.MapHeight, sx = ctx.StartCell.X, sy = ctx.StartCell.Y;
        var route = new List<CellCoord>();
        IReadOnlyList<CellCoord> starts = ctx.StartPositions;
        if (starts.Count >= 3)
        {
            int own = 0;
            for (int i = 1; i < starts.Count; i++)
                if (Cheb(starts[i], ctx.StartCell) < Cheb(starts[own], ctx.StartCell)) own = i;
            var others = new List<(int D, int Tie, int I)>();
            for (int i = 0; i < starts.Count; i++)
                if (i != own) others.Add((Dist2(starts[i], starts[own]), 0, i));   // squared distance: the diagonal corner is farther
            // equally near starts are ordered by the AI's seeded generator, not by map index (else every neighbour of start 0 scouted, found
            // and attacked start 0 first); numbers are drawn only when there is a tie, so 2-player games draw none
            bool tie = false;
            for (int a = 0; a < others.Count; a++)
                for (int b = a + 1; b < others.Count; b++) tie |= others[a].D == others[b].D;
            if (tie) for (int a = 0; a < others.Count; a++) others[a] = (others[a].D, ctx.Rng.Next(1024), others[a].I);
            others.Sort();   // (distance, seeded tie, map index): a total order, deterministic
            foreach (var (_, _, i) in others) Add(route, starts[i]);
        }
        else Add(route, new CellCoord(w - 1 - sx, h - 1 - sy));
        Add(route, new CellCoord(w / 2, h / 2));
        Add(route, new CellCoord(w - 1 - sx, sy));
        Add(route, new CellCoord(sx, h - 1 - sy));
        Add(route, new CellCoord(w - 1 - sx, h - 1 - sy));
        if (!centreSecond && route.Count > 2)
        {
            // scout order of M3 (mirror, side corner, centre, other corner); with 3-4 starts: every start, then the centre
            CellCoord centre = new(w / 2, h / 2);
            int ci = route.IndexOf(centre);
            if (ci >= 0)
            {
                route.RemoveAt(ci);
                route.Insert(starts.Count >= 3 ? Math.Min(route.Count, starts.Count - 1) : Math.Min(route.Count, 2), centre);
            }
        }
        return route;
    }

    private static void Add(List<CellCoord> route, CellCoord c)
    {
        foreach (CellCoord r in route) if (Cheb(r, c) < SameStopCells) return;
        route.Add(c);
    }

    private static int Dist2(CellCoord a, CellCoord b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
