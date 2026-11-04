// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// Deterministic structure placement for the Recruit AI: candidates are every top-left cell within SearchRadius of the
/// yard, ordered by distance (turrets: by distance to the map centre, i.e. the front), then (y, x). A candidate must pass
/// <see cref="PlayerView.CanPlace"/> (terrain, occupancy, region sealing and the build radius: the AI never re-implements
/// those rules). Three tiers of extra care are tried in turn: (0) a one-cell gap to every other building and the lanes kept
/// free, (1) lanes only, (2) anything legal. Lanes = the cells in front of every factory/barracks exit and refinery dock
/// (3 wide, 4 deep) so units and harvesters can always get out.
/// </summary>
internal sealed class PlacementSearch
{
    private const int SearchRadius = 12;
    private const int LaneHalfWidth = 1, LaneDepth = 4;

    private readonly record struct Rect(int X0, int Y0, int X1, int Y1)
    {
        public bool Intersects(Rect o) => X0 <= o.X1 && o.X0 <= X1 && Y0 <= o.Y1 && o.Y0 <= Y1;
        public Rect Grow(int n) => new(X0 - n, Y0 - n, X1 + n, Y1 + n);
    }

    private readonly List<Rect> _blocks = new();
    private readonly List<Rect> _lanes = new();
    private readonly List<(long Score, int X, int Y)> _cands = new();

    /// <summary>Finds a legal top-left for <paramref name="type"/> near the yard, or false if none exists.</summary>
    public bool Find(RecruitKnowledge k, int type, bool towardCentre, out CellCoord cell)
    {
        PlayerView view = k.View;
        SimRules rules = k.Rules;
        BuildingType bt = rules.Buildings[type];
        _blocks.Clear(); _lanes.Clear();
        foreach (ViewActor b in k.Buildings)
        {
            BuildingType t = rules.Buildings[b.TypeIndex];
            _blocks.Add(new Rect(b.Cell.X, b.Cell.Y, b.Cell.X + t.FootprintW - 1, b.Cell.Y + t.FootprintH - 1));
            if (HasLane(t)) AddLanes(t, b.Cell, _lanes, view.MapHeight);
        }

        int w = bt.FootprintW, h = bt.FootprintH;
        long cx2 = k.Yard.X * 2L + 3, cy2 = k.Yard.Y * 2L + 3;   // yard centre in half cells (3x3 yard)
        if (towardCentre) { cx2 = view.MapWidth; cy2 = view.MapHeight; }
        _cands.Clear();
        for (int y = k.Yard.Y - SearchRadius; y <= k.Yard.Y + SearchRadius; y++)
            for (int x = k.Yard.X - SearchRadius; x <= k.Yard.X + SearchRadius; x++)
            {
                if (x < 0 || y < 0 || x + w > view.MapWidth || y + h > view.MapHeight) continue;
                long dx = x * 2L + w - cx2, dy = y * 2L + h - cy2;
                _cands.Add((dx * dx + dy * dy, x, y));
            }
        bool fx = k.Yard.X * 2 + 3 > view.MapWidth, fy = k.Yard.Y * 2 + 3 > view.MapHeight;
        int mw = view.MapWidth, mh = view.MapHeight;
        _cands.Sort((a, b) =>
        {
            if (a.Score != b.Score) return a.Score.CompareTo(b.Score);
            int ay = fy ? mh - h - a.Y : a.Y, by = fy ? mh - h - b.Y : b.Y;
            if (ay != by) return ay.CompareTo(by);
            int ax = fx ? mw - w - a.X : a.X, bx = fx ? mw - w - b.X : b.X;
            return ax.CompareTo(bx);
        });

        for (int tier = 0; tier <= 2; tier++)
            foreach (var (_, x, y) in _cands)
            {
                var foot = new Rect(x, y, x + w - 1, y + h - 1);
                if (tier <= 1 && !LanesOk(view, bt, foot, x, y)) continue;
                if (tier == 0 && !GapOk(foot)) continue;
                if (!view.CanPlace(type, new CellCoord(x, y))) continue;
                cell = new CellCoord(x, y);
                return true;
            }
        cell = default;
        return false;
    }

    private static bool HasLane(BuildingType t) => t.Produces != null || t.Refinery;

    private static void AddLanes(BuildingType t, CellCoord topLeft, List<Rect> into, int mapH)
    {
        bool up = topLeft.Y * 2 + t.FootprintH > mapH;
        CellCoord e = up ? new CellCoord(t.FootprintW - 1 - t.Exit.X, t.FootprintH - 1 - t.Exit.Y) : t.Exit;
        CellCoord d = up ? new CellCoord(t.FootprintW - 1 - t.Dock.X, t.FootprintH - 1 - t.Dock.Y) : t.Dock;
        if (t.Produces != null) into.Add(LaneAt(topLeft.X + e.X, topLeft.Y + e.Y, up));
        if (t.Refinery) into.Add(LaneAt(topLeft.X + d.X, topLeft.Y + d.Y, up));
    }

    private static Rect LaneAt(int x, int y, bool up) => up ? new(x - LaneHalfWidth, y - LaneDepth + 1, x + LaneHalfWidth, y) : new(x - LaneHalfWidth, y, x + LaneHalfWidth, y + LaneDepth - 1);

    private bool GapOk(Rect foot)
    {
        Rect grown = foot.Grow(1);
        foreach (Rect b in _blocks)
            if (grown.Intersects(b)) return false;
        return true;
    }

    private bool LanesOk(PlayerView view, BuildingType bt, Rect foot, int x, int y)
    {
        foreach (Rect lane in _lanes)
            if (foot.Intersects(lane)) return false;
        if (!HasLane(bt)) return true;
        var mine = new List<Rect>(2);
        AddLanes(bt, new CellCoord(x, y), mine, view.MapHeight);
        foreach (Rect lane in mine)
        {
            int ex = (lane.X0 + lane.X1) / 2, ey = y * 2 + bt.FootprintH > view.MapHeight ? lane.Y1 : lane.Y0;
            if (!view.InMap(new CellCoord(ex, ey)) || !view.IsPassable(new CellCoord(ex, ey), Locomotion.Tracked)) return false;
            foreach (Rect b in _blocks)
                if (lane.Intersects(b)) return false;
        }
        return true;
    }
}
