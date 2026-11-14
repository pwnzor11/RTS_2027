// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data.MapGen;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Data.Stamps;

/// <summary>The generator's stamp pass (M12-13, <see cref="MapGenParams.Stamps"/>): places 2 to 4 stamps on the finished land, each with all its
/// symmetry images so the map stays fair, in neutral ground away from starts, ruins, cliffs, rock and resource fields. Uses only
/// <see cref="SimRandom"/> (its own stream, seeded from the attempt seed) and integer maths. The caller's checks then judge the result.</summary>
internal static class StampPass
{
    private const int StartKeep = 14;   // Chebyshev cells between a footprint and any start
    private const int RuinKeep = 3;     // cells between a footprint and a ruin
    private const int EdgeKeep = 3;
    private const int Gap = 2;          // cells between two stamps (and between a stamp and its images: 3)
    private const int TriesPerStamp = 40;

    private readonly record struct Rect(int X0, int Y0, int X1, int Y1);

    /// <summary>Returns the map with stamps applied (a new <see cref="MapData"/>; <paramref name="full"/> is not modified).</summary>
    public static MapData Run(MapData full, MapSymmetry symmetry, IReadOnlyList<CellCoord> orbit, IReadOnlyList<MapFeature> features, string biomeKey, ulong seed)
    {
        int n = full.Width;
        var rng = new SimRandom(unchecked(seed * 0x9E3779B97F4A7C15UL + 0x57A4D0C1UL), 77UL);
        var candidates = new List<Stamp>();
        foreach (Stamp s in StampLibrary.Default.All)
            if (s.Biomes.Count == 0 || s.Biomes.Contains(biomeKey)) candidates.Add(s);
        StampCanvas canvas = StampCanvas.CopyOf(full);
        if (candidates.Count == 0) return full;

        var keepOut = new List<Rect>();
        foreach (MapFeature f in features)
        {
            (int fw, int fh) = MapAnalysis.FootprintOf(f.Type);
            keepOut.Add(new Rect(f.Cell.X - RuinKeep, f.Cell.Y - RuinKeep, f.Cell.X + fw - 1 + RuinKeep, f.Cell.Y + fh - 1 + RuinKeep));
        }
        var placed = new List<Rect>();
        int want = rng.Next(2, 5), done = 0;
        for (int attempt = 0; attempt < want * TriesPerStamp && done < want; attempt++)
        {
            Stamp s = candidates[rng.Next(candidates.Count)];
            int rot = rng.Next(4); bool mirror = rng.Next(2) == 1;
            (int fw, int fh) = StampLibrary.FootprintSize(s, rot);
            int span = n - 2 * EdgeKeep - fw + 1, spanY = n - 2 * EdgeKeep - fh + 1;
            if (span < 1 || spanY < 1) continue;
            int x = EdgeKeep + rng.Next(span), y = EdgeKeep + rng.Next(spanY);
            var rect = new Rect(x, y, x + fw - 1, y + fh - 1);
            if (!Placeable(canvas, symmetry, orbit, keepOut, placed, s, rect, rot, mirror, out Rect[] images, out int[] changed)) continue;
            StampLibrary.Apply(s, canvas, x, y, rot, mirror);
            foreach (int i in changed) CopyToImages(canvas, symmetry, i);
            placed.Add(rect); placed.AddRange(images);
            done++;
        }
        return new MapData(full.Name, full.Theater, n, full.Height, full.Template.ToArray(), canvas.Land, canvas.HeightLevel, canvas.Overlay, canvas.Density,
            full.StartPositions, canvas.Decor);
    }

    private static bool Placeable(StampCanvas canvas, MapSymmetry symmetry, IReadOnlyList<CellCoord> orbit, List<Rect> keepOut, List<Rect> placed,
        Stamp s, Rect rect, int rot, bool mirror, out Rect[] images, out int[] changed)
    {
        images = Array.Empty<Rect>(); changed = Array.Empty<int>();
        foreach (CellCoord st in orbit)
            if (Dist(rect, new Rect(st.X, st.Y, st.X, st.Y)) < StartKeep) return false;
        foreach (Rect k in keepOut) if (Dist(rect, k) < 0) return false;
        foreach (Rect p in placed) if (Dist(rect, p) < Gap) return false;

        // The images of the footprint: bounding boxes of the four corners' images. They must stay clear of the footprint itself.
        int n = canvas.Width, order = MapSymmetries.Order(symmetry);
        var corners = new[] { (rect.X0, rect.Y0), (rect.X1, rect.Y0), (rect.X0, rect.Y1), (rect.X1, rect.Y1) };
        var imgs = new Rect[order - 1];
        for (int k = 1; k < order; k++)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach ((int cx, int cy) in corners)
            {
                (int ix, int iy) = MapSymmetries.Images(symmetry, n, n, cx, cy)[k];
                x0 = Math.Min(x0, ix); y0 = Math.Min(y0, iy); x1 = Math.Max(x1, ix); y1 = Math.Max(y1, iy);
            }
            imgs[k - 1] = new Rect(x0, y0, x1, y1);
            if (Dist(rect, imgs[k - 1]) < 3) return false;
            foreach (Rect p in placed) if (Dist(imgs[k - 1], p) < Gap) return false;
        }

        // Trial on a scratch copy: nothing refused, and only plain ground (and its images) is overwritten.
        var trial = new StampCanvas(n, canvas.Height, (LandType[])canvas.Land.Clone(), (byte[])canvas.HeightLevel.Clone(), (OverlayKind[])canvas.Overlay.Clone(),
            (byte[])canvas.Density.Clone(), canvas.Decor is null ? null : (byte[])canvas.Decor.Clone(), canvas.Starts);
        StampResult r = StampLibrary.Apply(s, trial, rect.X0, rect.Y0, rot, mirror);
        if (r.Refused.Count > 0) return false;
        foreach (int i in r.Changed)
        {
            if (!Plain(canvas, i)) return false;
            (int, int)[] im = MapSymmetries.Images(symmetry, n, n, i % n, i / n);
            for (int k = 1; k < im.Length; k++)
            {
                int j = im[k].Item2 * n + im[k].Item1;
                if (!Plain(canvas, j) || canvas.NearStart(im[k].Item1, im[k].Item2)) return false;
            }
        }
        images = imgs; changed = r.Changed.ToArray();
        return true;
    }

    /// <summary>Ground a stamp may replace: not cliff, rock or a wall, and not a resource field.</summary>
    private static bool Plain(StampCanvas c, int i) =>
        c.Land[i] is not (LandType.Cliff or LandType.Rock) && c.Overlay[i] != OverlayKind.Wall && !OverlayKinds.IsResource(c.Overlay[i]);

    /// <summary>Copies every layer of cell <paramref name="i"/> to its symmetry images.</summary>
    private static void CopyToImages(StampCanvas c, MapSymmetry symmetry, int i)
    {
        int n = c.Width;
        (int, int)[] im = MapSymmetries.Images(symmetry, n, n, i % n, i / n);
        for (int k = 1; k < im.Length; k++)
        {
            int j = im[k].Item2 * n + im[k].Item1;
            c.Land[j] = c.Land[i]; c.HeightLevel[j] = c.HeightLevel[i]; c.Overlay[j] = c.Overlay[i]; c.Density[j] = c.Density[i];
            if (c.Decor is not null) c.Decor[j] = c.Decor[i];
        }
    }

    /// <summary>Chebyshev gap between two rectangles: the number of free cells between them (negative when they overlap).</summary>
    private static int Dist(Rect a, Rect b) => Math.Max(Math.Max(b.X0 - a.X1, a.X0 - b.X1), Math.Max(b.Y0 - a.Y1, a.Y0 - b.Y1)) - 1;
}
