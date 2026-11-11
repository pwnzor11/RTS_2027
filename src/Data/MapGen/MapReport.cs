// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

/// <summary>
/// The measured table of design 16 section 1.2 for one map (M12-06), plus the prop figures of design 17 (props per 100 cells, clustering,
/// share near starts, dead ground). All integer maths; paths are Tracked 4-way BFS over the built-in passability rule (props and ruins block).
/// </summary>
/// <param name="Name">Map name.</param>
/// <param name="Width">Cells across.</param>
/// <param name="Height">Cells down.</param>
/// <param name="Starts">Start positions.</param>
/// <param name="StartPath">Shortest path between start 1 and start 2, or <see cref="MapAnalysis.Unreachable"/>.</param>
/// <param name="Manhattan">Straight (Manhattan) distance between them.</param>
/// <param name="NearShortestCells">Cells on a path at most 10 % longer than the shortest.</param>
/// <param name="HalfwayWidth">Cells of that corridor exactly halfway along the shortest path.</param>
/// <param name="Fields">Resource fields on the map.</param>
/// <param name="FieldsNearStart">Average number of fields within 14 path cells of a start.</param>
/// <param name="Ruins">Neutral ruin features.</param>
/// <param name="DeadPermille">Cells (per mille) with nothing within 4 cells: no prop, resource, ruin, height change or non-base land.</param>
/// <param name="Props">Blocking prop cells.</param>
/// <param name="PropsPer100">Prop cells per 100 cells, times 100 (so 200 = 2.00).</param>
/// <param name="PropClusterShare">Per mille of props that sit in an 8-connected group of 4 or more.</param>
/// <param name="PropMedianCluster">Median group size, counted by prop.</param>
/// <param name="PropNearStartPermille">Per mille of props within 12 cells of a start (the area share is in <see cref="AreaNearStartPermille"/>).</param>
/// <param name="AreaNearStartPermille">Per mille of all cells within 12 cells of a start.</param>
/// <param name="BlockedTerrainPermille">Per mille of cells that are Water, Rock or Cliff.</param>
/// <param name="HarvestNearestTenths">Harvest travel distance (v6): path cells from a start to its nearest resource cell, averaged over the starts, in tenths.</param>
/// <param name="HarvestMeanTenths">Path cells from a start to the <see cref="HarvestCells"/> nearest resource cells (the first cells its harvesters mine), averaged over the cells and the starts, in tenths.</param>
public sealed record MapReport(string Name, int Width, int Height, int Starts, int StartPath, int Manhattan, int NearShortestCells, int HalfwayWidth,
    int Fields, int FieldsNearStart, int Ruins, int DeadPermille, int Props, int PropsPer100, int PropClusterShare, int PropMedianCluster,
    int PropNearStartPermille, int AreaNearStartPermille, int BlockedTerrainPermille, int HarvestNearestTenths = 0, int HarvestMeanTenths = 0)
{
    /// <summary>How many of the nearest resource cells <see cref="HarvestMeanTenths"/> averages.</summary>
    public const int HarvestCells = 60;

    /// <summary>Harvest travel distance of a map (the v6 income lever): per start, the path distance (Tracked 4-way, props and ruins block) from the start cell to
    /// its nearest resource cell and the mean over its <see cref="HarvestCells"/> nearest ones; both averaged over the starts, in tenths of a cell.</summary>
    public static (int NearestTenths, int MeanTenths) HarvestDistance(MapData map, MapMeta? meta)
    {
        ArgumentNullException.ThrowIfNull(map);
        bool[]? blocked = MapAnalysis.FeatureMask(map, meta?.Features);
        long nearSum = 0, meanSum = 0; int counted = 0;
        foreach (CellCoord s in map.StartPositions)
        {
            int[] d = MapAnalysis.Distances(map, s, Locomotion.Tracked, null, blocked);
            var list = new List<int>();
            for (int i = 0; i < d.Length; i++)
                if (OverlayKinds.IsResource(map.Overlay[i]) && d[i] != MapAnalysis.Unreachable) list.Add(d[i]);
            if (list.Count == 0) continue;
            list.Sort();
            int take = Math.Min(HarvestCells, list.Count);
            long sum = 0; for (int k = 0; k < take; k++) sum += list[k];
            nearSum += list[0] * 10L; meanSum += sum * 10 / take; counted++;
        }
        return counted == 0 ? (0, 0) : ((int)(nearSum / counted), (int)(meanSum / counted));
    }

    /// <summary>Measures a map. <paramref name="meta"/> may be null (no ruins counted, ruins do not block).</summary>
    public static MapReport Measure(MapData map, MapMeta? meta)
    {
        ArgumentNullException.ThrowIfNull(map);
        int w = map.Width, h = map.Height, n = w * h;
        bool[]? blocked = MapAnalysis.FeatureMask(map, meta?.Features);
        IReadOnlyList<CellCoord> starts = map.StartPositions;

        int path = MapAnalysis.Unreachable, manhattan = 0, near = 0, half = 0;
        if (starts.Count >= 2)
        {
            int[] a = MapAnalysis.Distances(map, starts[0], Locomotion.Tracked, null, blocked);
            int[] b = MapAnalysis.Distances(map, starts[1], Locomotion.Tracked, null, blocked);
            path = a[map.IndexOf(starts[1])];
            manhattan = Math.Abs(starts[0].X - starts[1].X) + Math.Abs(starts[0].Y - starts[1].Y);
            if (path != MapAnalysis.Unreachable)
            {
                long limit = path * 110L / 100;
                for (int i = 0; i < n; i++)
                {
                    if (a[i] == MapAnalysis.Unreachable || b[i] == MapAnalysis.Unreachable || a[i] + (long)b[i] > limit) continue;
                    near++;
                    if (a[i] == path / 2) half++;
                }
            }
        }

        int[] fieldIds = MapAnalysis.FieldIds(map, out int fieldCount);
        long fieldSum = 0;
        foreach (CellCoord s in starts)
            fieldSum += MapAnalysis.FieldsWithin(MapAnalysis.Distances(map, s, Locomotion.Tracked, null, blocked), fieldIds, fieldCount, MapValidator.FieldPathDistance);
        int fieldsNear = starts.Count == 0 ? 0 : (int)((fieldSum * 10 + starts.Count / 2) / starts.Count);   // tenths

        int ruins = 0;
        if (meta is not null) foreach (MapFeature f in meta.Features) if (f.Type.StartsWith("neu_ruin", StringComparison.Ordinal)) ruins++;

        // Dead ground: 9x9 windows with nothing in them.
        var sum = new int[(w + 1) * (h + 1)];
        Land(map, out LandType baseLand);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                bool something = map.Land[i] != baseLand || map.Overlay[i] != OverlayKind.None || map.HeightLevel[i] != 0 || (blocked is not null && blocked[i]);
                sum[(y + 1) * (w + 1) + x + 1] = sum[y * (w + 1) + x + 1] + sum[(y + 1) * (w + 1) + x] - sum[y * (w + 1) + x] + (something ? 1 : 0);
            }
        int dead = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int x0 = Math.Max(0, x - 4), y0 = Math.Max(0, y - 4), x1 = Math.Min(w, x + 5), y1 = Math.Min(h, y + 5);
                if (sum[y1 * (w + 1) + x1] - sum[y0 * (w + 1) + x1] - sum[y1 * (w + 1) + x0] + sum[y0 * (w + 1) + x0] == 0) dead++;
            }

        // Props: counts, clusters (8-connected), near starts.
        int props = 0, nearStartProps = 0, areaNear = 0, blockedTerrain = 0;
        var comp = new int[n];
        var sizes = new List<int>();
        var stack = new Stack<int>();
        for (int i = 0; i < n; i++)
        {
            int x = i % w, y = i / w;
            bool nearStart = false;
            foreach (CellCoord s in starts) if (Math.Max(Math.Abs(x - s.X), Math.Abs(y - s.Y)) <= 12) { nearStart = true; break; }
            if (nearStart) areaNear++;
            if (map.Land[i] is LandType.Water or LandType.Rock or LandType.Cliff) blockedTerrain++;
            if (!OverlayKinds.IsProp(map.Overlay[i])) continue;
            props++;
            if (nearStart) nearStartProps++;
            if (comp[i] != 0) continue;
            int size = 0;
            comp[i] = sizes.Count + 1; stack.Push(i);
            while (stack.Count > 0)
            {
                int c = stack.Pop(); size++;
                int cx = c % w, cy = c / w;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (comp[j] != 0 || !OverlayKinds.IsProp(map.Overlay[j])) continue;
                        comp[j] = sizes.Count + 1; stack.Push(j);
                    }
            }
            sizes.Add(size);
        }
        int inBig = 0;
        var perProp = new List<int>();
        foreach (int s in sizes) { if (s >= 4) inBig += s; for (int k = 0; k < s; k++) perProp.Add(s); }
        perProp.Sort();
        int median = perProp.Count == 0 ? 0 : perProp[perProp.Count / 2];

        (int hNear, int hMean) = HarvestDistance(map, meta);
        return new MapReport(map.Name, w, h, starts.Count, path, manhattan, near, half, fieldCount, fieldsNear, ruins,
            dead * 1000 / n, props, props * 10000 / n, props == 0 ? 0 : inBig * 1000 / props, median,
            props == 0 ? 0 : nearStartProps * 1000 / props, areaNear * 1000 / n, blockedTerrain * 1000 / n, hNear, hMean);
    }

    private static void Land(MapData map, out LandType baseLand)
    {
        var counts = new int[RuleCounts.LandTypes];
        for (int i = 0; i < map.Land.Count; i++) counts[(int)map.Land[i]]++;
        int best = 0;
        for (int l = 1; l < counts.Length; l++) if (counts[l] > counts[best]) best = l;
        baseLand = (LandType)best;
    }

    /// <summary>The report as aligned text lines (label, value).</summary>
    public IReadOnlyList<string> Lines()
    {
        static string F(int v, int div) => (v / (double)div).ToString("0.0#", CultureInfo.InvariantCulture);
        string path = StartPath == MapAnalysis.Unreachable ? "unreachable" : StartPath.ToString(CultureInfo.InvariantCulture);
        return new[]
        {
            $"map                          {Name} {Width}x{Height}, {Starts} starts",
            $"start-to-start path          {path} (straight {Manhattan})",
            $"cells within +10% of shortest {NearShortestCells}",
            $"width at the halfway line    {HalfwayWidth}",
            $"resource fields              {Fields}, {F(FieldsNearStart, 10)} within 14 path of a start",
            $"harvest distance            nearest {F(HarvestNearestTenths, 10)}, mean of the {HarvestCells} nearest cells {F(HarvestMeanTenths, 10)} (path cells from the start)",
            $"neutral ruins                {Ruins}",
            $"dead ground (nothing in 9x9) {F(DeadPermille, 10)} %",
            $"blocked terrain              {F(BlockedTerrainPermille, 10)} %",
            $"blocking props               {Props} ({F(PropsPer100, 100)} per 100 cells)",
            $"props in clusters of 4+      {F(PropClusterShare, 10)} % (median cluster {PropMedianCluster})",
            $"props within 12 of a start   {F(PropNearStartPermille, 10)} % (area share {F(AreaNearStartPermille, 10)} %)",
        };
    }
}
