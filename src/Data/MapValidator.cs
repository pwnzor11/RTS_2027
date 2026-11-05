// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data.MapGen;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>Checks a map for skirmish use (design 08 §1/§5; shared by the generator, the editor and <c>map validate</c>).
/// Errors: <c>starts_count</c> (2–4 starts), <c>start_space</c> (clear buildable 5×5 square, flat, no resources, no features),
/// <c>start_distance</c> (≥ 12 cells Chebyshev), <c>connectivity_foot</c> / <c>connectivity_tracked</c> (every start reaches every
/// other), <c>feature_cell</c> (on the map, on buildable land, no overlap, ≥ 6 cells from a start).
/// Warnings: <c>few_fields</c> (&lt; 2 fields within 14 cells of path, Tracked) and <c>symmetry</c> (first cell that differs from
/// the declared symmetry).</summary>
public static class MapValidator
{
    /// <summary>Fewest and most start positions a skirmish map may have.</summary>
    public const int MinStarts = 2, MaxStarts = 4;
    /// <summary>Chebyshev radius of the required start square (5×5).</summary>
    public const int StartSquareRadius = 2;
    /// <summary>Least Chebyshev distance between two starts.</summary>
    public const int MinStartDistance = 12;
    /// <summary>Fields each start should reach within <see cref="FieldPathDistance"/> (tune).</summary>
    public const int MinFieldsPerStart = 2;
    /// <summary>Path distance (cells, Tracked) for <see cref="MinFieldsPerStart"/> (tune).</summary>
    public const int FieldPathDistance = 14;
    /// <summary>Least Chebyshev distance from a feature footprint to a start.</summary>
    public const int FeatureStartDistance = 6;

    /// <summary>Every issue found, errors and warnings, in a fixed order. <paramref name="terrain"/> null = the built-in conservative
    /// passability rule.</summary>
    public static IReadOnlyList<MapIssue> Validate(MapData map, MapMeta? meta, TerrainTable? terrain)
    {
        ArgumentNullException.ThrowIfNull(map);
        var issues = new List<MapIssue>();
        IReadOnlyList<CellCoord> starts = map.StartPositions;
        IReadOnlyList<MapFeature> features = meta?.Features ?? Array.Empty<MapFeature>();
        bool[]? blocked = MapAnalysis.FeatureMask(map, features);

        if (starts.Count < MinStarts || starts.Count > MaxStarts)
            issues.Add(Error("starts_count", starts.Count > 0 ? starts[0] : null, $"map has {starts.Count} start positions, needs {MinStarts}..{MaxStarts}"));

        for (int s = 0; s < starts.Count; s++)
        {
            string? why = StartSquareProblem(map, starts[s], terrain, blocked);
            if (why is not null) issues.Add(Error("start_space", starts[s], $"start {s + 1}: no clear 5x5 buildable square ({why})"));
        }

        for (int a = 0; a < starts.Count; a++)
            for (int b = a + 1; b < starts.Count; b++)
            {
                int d = Math.Max(Math.Abs(starts[a].X - starts[b].X), Math.Abs(starts[a].Y - starts[b].Y));
                if (d < MinStartDistance)
                    issues.Add(Error("start_distance", starts[b], $"starts {a + 1} and {b + 1} are {d} cells apart, need {MinStartDistance}"));
            }

        if (starts.Count >= 2)
        {
            Connectivity(map, starts, Locomotion.Foot, "connectivity_foot", terrain, blocked, issues);
            Connectivity(map, starts, Locomotion.Tracked, "connectivity_tracked", terrain, blocked, issues);
        }

        FeatureChecks(map, starts, features, terrain, issues);

        if (starts.Count > 0)
        {
            int[] fields = MapAnalysis.FieldIds(map, out int fieldCount);
            for (int s = 0; s < starts.Count; s++)
            {
                int[] dist = MapAnalysis.Distances(map, starts[s], Locomotion.Tracked, terrain, blocked);
                int near = MapAnalysis.FieldsWithin(dist, fields, fieldCount, FieldPathDistance);
                if (near < MinFieldsPerStart)
                    issues.Add(Warning("few_fields", starts[s], $"start {s + 1} reaches {near} resource fields within {FieldPathDistance} cells, want {MinFieldsPerStart}"));
            }
        }

        if (meta is not null && (meta.ConvoyEdge.Count > 0 || meta.Landing.Count > 0)) NavalChecks(map, meta, terrain, blocked, issues);

        if (meta is not null && meta.Symmetry != MapSymmetry.None) SymmetryCheck(map, meta.Symmetry, issues);
        return issues;
    }

    /// <summary>Naval maps (M7-13), checked only when the map declares <c>convoy_edge</c> or <c>landing</c> cells, so every older map is
    /// unaffected. Sea = cells <see cref="Locomotion.Naval"/> may enter, 4-connected. Errors: <c>convoy_lake</c> (a convoy cell that is not
    /// sea, or whose sea touches no map border: an isolated lake), <c>convoy_no_coast</c> (a convoy sea with no Coast cell beside it),
    /// <c>landing_unreachable</c> (a landing marker that is not a Coast cell beside a convoy sea, or, when convoy cells are declared, a
    /// start that cannot walk to a sea-facing Coast cell).</summary>
    private static void NavalChecks(MapData map, MapMeta meta, TerrainTable? terrain, bool[]? blocked, List<MapIssue> issues)
    {
        int w = map.Width, h = map.Height;
        // Label the sea components (cell-index order) and note which touch the border.
        var comp = new int[w * h];
        Array.Fill(comp, -1);
        var touchesBorder = new List<bool>();
        var queue = new int[w * h];
        for (int s = 0; s < w * h; s++)
        {
            if (comp[s] >= 0 || !MapAnalysis.Passable(map.Land[s], Locomotion.Naval, terrain)) continue;
            int id = touchesBorder.Count, head = 0, tail = 0;
            bool border = false;
            comp[s] = id; queue[tail++] = s;
            while (head < tail)
            {
                int i = queue[head++];
                int x = i % w, y = i / w;
                if (x == 0 || y == 0 || x == w - 1 || y == h - 1) border = true;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx;
                    if (comp[j] < 0 && MapAnalysis.Passable(map.Land[j], Locomotion.Naval, terrain)) { comp[j] = id; queue[tail++] = j; }
                }
            }
            touchesBorder.Add(border);
        }

        var convoySea = new bool[touchesBorder.Count];   // components holding a valid convoy cell
        var firstCell = new CellCoord?[touchesBorder.Count];
        foreach (CellCoord c in meta.ConvoyEdge)
        {
            if (!map.Contains(c)) { issues.Add(Error("convoy_lake", c, $"convoy cell ({c.X},{c.Y}) is off the map")); continue; }
            int id = comp[map.IndexOf(c)];
            if (id < 0) issues.Add(Error("convoy_lake", c, $"convoy cell ({c.X},{c.Y}) is {map.Land[map.IndexOf(c)]}, not sea"));
            else if (!touchesBorder[id]) issues.Add(Error("convoy_lake", c, $"convoy cell ({c.X},{c.Y}) lies in an isolated lake that never reaches the map edge"));
            else { convoySea[id] = true; firstCell[id] ??= c; }
        }

        // Coast cells beside a convoy sea.
        var beach = new bool[w * h];
        var seaWithCoast = new bool[touchesBorder.Count];
        for (int i = 0; i < w * h; i++)
        {
            if (map.Land[i] != LandType.Coast) continue;
            int x = i % w, y = i / w;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int id = comp[ny * w + nx];
                if (id >= 0 && convoySea[id]) { beach[i] = true; seaWithCoast[id] = true; }
            }
        }
        for (int id = 0; id < convoySea.Length; id++)
            if (convoySea[id] && !seaWithCoast[id])
                issues.Add(Error("convoy_no_coast", firstCell[id], "the sea at this convoy cell reaches no Coast cell: ships have nowhere to land troops"));

        foreach (CellCoord c in meta.Landing)
            if (!map.Contains(c) || !beach[map.IndexOf(c)])
                issues.Add(Error("landing_unreachable", c, $"landing cell ({c.X},{c.Y}) is not a Coast cell beside a sea the convoy edge reaches"));

        if (meta.ConvoyEdge.Count == 0) return;
        for (int s = 0; s < map.StartPositions.Count; s++)
        {
            int[] dist = MapAnalysis.Distances(map, map.StartPositions[s], Locomotion.Foot, terrain, blocked);
            bool reach = false;
            for (int i = 0; i < w * h && !reach; i++) reach = beach[i] && dist[i] != MapAnalysis.Unreachable;
            if (!reach) issues.Add(Error("landing_unreachable", map.StartPositions[s], $"start {s + 1} cannot walk to a Coast cell that ships can reach"));
        }
    }

    private static string? StartSquareProblem(MapData map, CellCoord s, TerrainTable? terrain, bool[]? blocked)
    {
        for (int dy = -StartSquareRadius; dy <= StartSquareRadius; dy++)
            for (int dx = -StartSquareRadius; dx <= StartSquareRadius; dx++)
            {
                var c = new CellCoord(s.X + dx, s.Y + dy);
                if (!map.Contains(c)) return "runs off the map";
                int i = map.IndexOf(c);
                if (!MapAnalysis.Buildable(map.Land[i], terrain)) return $"{map.Land[i]} at ({c.X},{c.Y})";
                if (map.Overlay[i] != OverlayKind.None) return $"{map.Overlay[i]} at ({c.X},{c.Y})";
                if (map.HeightLevel[i] != map.HeightLevel[map.IndexOf(s)]) return $"height step at ({c.X},{c.Y})";
                if (blocked is not null && blocked[i]) return $"feature at ({c.X},{c.Y})";
            }
        return null;
    }

    private static void Connectivity(MapData map, IReadOnlyList<CellCoord> starts, Locomotion loco, string code, TerrainTable? terrain,
        bool[]? blocked, List<MapIssue> issues)
    {
        int[] dist = MapAnalysis.Distances(map, starts[0], loco, terrain, blocked);
        for (int s = 1; s < starts.Count; s++)
            if (dist[map.IndexOf(starts[s])] == MapAnalysis.Unreachable)
                issues.Add(Error(code, starts[s], $"start {s + 1} cannot be reached from start 1 by {loco} units"));
    }

    private static void FeatureChecks(MapData map, IReadOnlyList<CellCoord> starts, IReadOnlyList<MapFeature> features, TerrainTable? terrain,
        List<MapIssue> issues)
    {
        if (features.Count == 0) return;
        var owner = new int[map.Width * map.Height];
        Array.Fill(owner, -1);
        for (int f = 0; f < features.Count; f++)
        {
            MapFeature feat = features[f];
            (int w, int h) = MapAnalysis.FootprintOf(feat.Type);
            string? why = null;
            for (int dy = 0; dy < h && why is null; dy++)
                for (int dx = 0; dx < w && why is null; dx++)
                {
                    var c = new CellCoord(feat.Cell.X + dx, feat.Cell.Y + dy);
                    if (!map.Contains(c)) { why = "footprint runs off the map"; break; }
                    int i = map.IndexOf(c);
                    if (!MapAnalysis.Buildable(map.Land[i], terrain)) why = $"{map.Land[i]} at ({c.X},{c.Y})";
                    else if (map.Overlay[i] != OverlayKind.None) why = $"{map.Overlay[i]} at ({c.X},{c.Y})";
                    else if (owner[i] >= 0) why = $"overlaps feature {owner[i] + 1}";
                    else owner[i] = f;
                }
            if (why is null)
                for (int s = 0; s < starts.Count; s++)
                {
                    int ddx = Math.Max(0, Math.Max(feat.Cell.X - starts[s].X, starts[s].X - (feat.Cell.X + w - 1)));
                    int ddy = Math.Max(0, Math.Max(feat.Cell.Y - starts[s].Y, starts[s].Y - (feat.Cell.Y + h - 1)));
                    if (Math.Max(ddx, ddy) < FeatureStartDistance) { why = $"within {FeatureStartDistance} cells of start {s + 1}"; break; }
                }
            if (why is not null) issues.Add(Error("feature_cell", feat.Cell, $"feature {f + 1} ({feat.Type}): {why}"));
        }
    }

    private static void SymmetryCheck(MapData map, MapSymmetry symmetry, List<MapIssue> issues)
    {
        if (symmetry is MapSymmetry.Rot4 or MapSymmetry.MirrorDiag && map.Width != map.Height)
        {
            issues.Add(Warning("symmetry", null, $"{symmetry} needs a square map, map is {map.Width}x{map.Height}"));
            return;
        }
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                int i = y * map.Width + x;
                foreach ((int ix, int iy) in MapSymmetries.Images(symmetry, map.Width, map.Height, x, y))
                {
                    int j = iy * map.Width + ix;
                    if (map.Land[i] != map.Land[j] || map.HeightLevel[i] != map.HeightLevel[j] || map.Overlay[i] != map.Overlay[j]
                        || map.Density[i] != map.Density[j])
                    {
                        issues.Add(Warning("symmetry", new CellCoord(x, y),
                            $"cell ({x},{y}) differs from its {MapFile.SymmetryKeys[(int)symmetry]} image ({ix},{iy})"));
                        return;
                    }
                }
            }
    }

    private static MapIssue Error(string code, CellCoord? cell, string message) => new(MapIssueSeverity.Error, code, cell, message);
    private static MapIssue Warning(string code, CellCoord? cell, string message) => new(MapIssueSeverity.Warning, code, cell, message);
}
