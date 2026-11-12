// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// Deterministic structure placement for the Recruit AI: candidates are every top-left cell within SearchRadius of the
/// yard, ordered by distance (turrets: by distance to the map centre, i.e. the front), then by distance to the map centre, then (y, x) in an order mirrored for a yard past the centre. A candidate must pass
/// <see cref="PlayerView.CanPlace"/> (terrain, occupancy, region sealing and the build radius: the AI never re-implements
/// those rules). Three tiers of extra care are tried in turn: (0) a one-cell gap to every other building and the lanes kept
/// free, (1) lanes only, (2) anything legal. Lanes = the cells in front of every factory/barracks exit and refinery dock
/// (3 wide, 4 deep) so units and harvesters can always get out.
/// </summary>
internal sealed class PlacementSearch
{
    internal const int SearchRadius = 12;
    internal const int WideSearchRadius = 24;
    private const int LaneHalfWidth = 1, LaneDepth = 4;
    private const int OutletWidth = 2, OutletRows = 2;   // S18-AI-NEW-3: the lane's mouth widens toward the open side (the map centre)

    private readonly record struct Rect(int X0, int Y0, int X1, int Y1)
    {
        public bool Intersects(Rect o) => X0 <= o.X1 && o.X0 <= X1 && Y0 <= o.Y1 && o.Y0 <= Y1;
        public Rect Grow(int n) => new(X0 - n, Y0 - n, X1 + n, Y1 + n);
    }

    private readonly List<Rect> _blocks = new();
    private readonly List<Rect> _lanes = new();
    private readonly List<Rect> _mouths = new();   // D-157: the single dock / exit cells themselves; no tier may build on them
    private readonly List<(long Score, long Centre, int X, int Y)> _cands = new();

    /// <summary>Finds a legal top-left for <paramref name="type"/> near the yard, or false if none exists.</summary>
    public bool Find(RecruitKnowledge k, int type, bool towardCentre, out CellCoord cell)
    {
        SimRules rules = k.Rules;
        BuildingType bt = rules.Buildings[type];
        _blocks.Clear(); _lanes.Clear(); _mouths.Clear();
        foreach (ViewActor b in k.Buildings)
        {
            BuildingType t = rules.Buildings[b.TypeIndex];
            _blocks.Add(new Rect(b.Cell.X, b.Cell.Y, b.Cell.X + t.FootprintW - 1, b.Cell.Y + t.FootprintH - 1));
            if (HasLane(t)) AddLanes(t, b.Cell, _lanes, k.View.MapWidth);
            if (t.Produces != null) _mouths.Add(new Rect(b.Cell.X + t.Exit.X, b.Cell.Y + t.Exit.Y, b.Cell.X + t.Exit.X, b.Cell.Y + t.Exit.Y));
            if (t.Refinery) _mouths.Add(new Rect(b.Cell.X + t.Dock.X, b.Cell.Y + t.Dock.Y, b.Cell.X + t.Dock.X, b.Cell.Y + t.Dock.Y));
        }

        // M13-10: a base that has filled the window around the yard (a corner base, 30 supply structures) searches a wider window before giving up
        return Search(k, type, bt, towardCentre, SearchRadius, out cell) || Search(k, type, bt, towardCentre, WideSearchRadius, out cell);
    }

    private bool Search(RecruitKnowledge k, int type, BuildingType bt, bool towardCentre, int radius, out CellCoord cell)
    {
        PlayerView view = k.View;
        int w = bt.FootprintW, h = bt.FootprintH;
        long cx2 = k.Yard.X * 2L + 3, cy2 = k.Yard.Y * 2L + 3;   // yard centre in half cells (3x3 yard)
        if (towardCentre) { cx2 = view.MapWidth; cy2 = view.MapHeight; }
        // M13-10 / AI-08: ties are broken toward the map centre, then in an order that is flipped for a yard on the far side of the
        // centre (x when east of it, y when south of it), so under a mirror or half turn of the map the other start's first pick
        // is the image of this start's pick instead of a translation of it.
        int flipX = k.Yard.X * 2L + 3 > view.MapWidth ? -1 : 1, flipY = k.Yard.Y * 2L + 3 > view.MapHeight ? -1 : 1;
        _cands.Clear();
        for (int y = k.Yard.Y - radius; y <= k.Yard.Y + radius; y++)
            for (int x = k.Yard.X - radius; x <= k.Yard.X + radius; x++)
            {
                if (x < 0 || y < 0 || x + w > view.MapWidth || y + h > view.MapHeight) continue;
                long dx = x * 2L + w - cx2, dy = y * 2L + h - cy2;
                long mx = x * 2L + w - view.MapWidth, my = y * 2L + h - view.MapHeight;
                _cands.Add((dx * dx + dy * dy, mx * mx + my * my, x, y));
            }
        _cands.Sort((a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) : a.Centre != b.Centre ? a.Centre.CompareTo(b.Centre)
            : a.Y != b.Y ? flipY * a.Y.CompareTo(b.Y) : flipX * a.X.CompareTo(b.X));

        for (int tier = 0; tier <= 2; tier++)
            foreach (var (_, _, x, y) in _cands)
            {
                var foot = new Rect(x, y, x + w - 1, y + h - 1);
                if (OnMouth(foot)) continue;   // D-157: a structure on a refinery dock cell or factory exit starves the economy for good
                if (tier <= 1 && !LanesOk(view, bt, foot, x, y)) continue;
                if (tier == 0 && !GapOk(foot)) continue;
                if (!view.CanPlace(type, new CellCoord(x, y))) continue;
                cell = new CellCoord(x, y);
                return true;
            }
        cell = default;
        return false;
    }

    private bool OnMouth(Rect foot)
    {
        foreach (Rect m in _mouths)
            if (foot.Intersects(m)) return true;
        return false;
    }

    private static bool HasLane(BuildingType t) => t.Produces != null || t.Refinery;

    private static void AddLanes(BuildingType t, CellCoord topLeft, List<Rect> into, int mapWidth)
    {
        if (t.Produces != null) AddLane(topLeft.X + t.Exit.X, topLeft.Y + t.Exit.Y, mapWidth, into);
        if (t.Refinery) AddLane(topLeft.X + t.Dock.X, topLeft.Y + t.Dock.Y, mapWidth, into);
    }

    /// <summary>The lane straight out of an exit or dock (3 wide, 4 deep: the exit itself is fixed by the sim), plus an outlet beside its far end
    /// on the side facing the map centre, so units and harvesters can turn toward the open side. S18-AI-NEW-3: the side follows the base, so a
    /// mirrored or half-turned start keeps the image of the same outlet (an exit exactly on the centre line takes the east side).</summary>
    private static void AddLane(int x, int y, int mapWidth, List<Rect> into)
    {
        into.Add(new Rect(x - LaneHalfWidth, y, x + LaneHalfWidth, y + LaneDepth - 1));
        bool west = x * 2L + 1 < mapWidth;   // the exit is west of the centre line: the open side is east
        int far = LaneDepth - OutletRows;
        into.Add(west ? new Rect(x + LaneHalfWidth + 1, y + far, x + LaneHalfWidth + OutletWidth, y + LaneDepth - 1)
                      : new Rect(x - LaneHalfWidth - OutletWidth, y + far, x - LaneHalfWidth - 1, y + LaneDepth - 1));
    }

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
        var mine = new List<Rect>(4);
        AddLanes(bt, new CellCoord(x, y), mine, view.MapWidth);
        for (int li = 0; li < mine.Count; li += 2)   // each lane is its main rectangle followed by its outlet
        {
            Rect lane = mine[li];
            int ex = (lane.X0 + lane.X1) / 2, ey = lane.Y0;
            if (!view.InMap(new CellCoord(ex, ey)) || !view.IsPassable(new CellCoord(ex, ey), Locomotion.Tracked)) return false;
            foreach (Rect b in _blocks)
                if (lane.Intersects(b)) return false;
        }
        return true;
    }
}
