// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Data;
using Rts.Data.Stamps;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Tools;

/// <summary>`Tools map stamp --base &lt;map&gt; --plan &lt;plan.toml&gt; --out &lt;map&gt;` (M12-14): applies a reviewable list of stamps, single props and
/// ground patches to an existing map file, deterministically (same base and plan give a byte-identical file). Plan file (TOML):
/// <c>symmetry</c> (optional key such as rot2 or mirror_y: every op is copied to its images; default none), <c>[[keep]] rect = [x0,y0,x1,y1]</c>
/// (cells no op may change), <c>[[stamp]] id, x, y, rot, mirror, sym</c>, <c>[[prop]] overlay, x, y, density, sym</c> and
/// <c>[[patch]] land, rect, sym</c>. An op that touches a start square, a resource cell, a keep rect, or whose image does, is an error, so a plan
/// that loads is safe by construction; the result is also run through the map validator and any error fails the command.</summary>
public static class MapStampCommand
{
    private const int StartKeep = 4;   // start square (5x5) plus a margin of one

    /// <summary>Runs the command; returns 0 ok, 1 error, 2 usage.</summary>
    public static int Run(string[] args)
    {
        string? basePath = null, planPath = null, outPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (i + 1 >= args.Length) { Console.Error.WriteLine("usage: map stamp --base <map> --plan <plan.toml> --out <map>"); return 2; }
            switch (args[i])
            {
                case "--base": basePath = args[++i]; break;
                case "--plan": planPath = args[++i]; break;
                case "--out": outPath = args[++i]; break;
                default: Console.Error.WriteLine("usage: map stamp --base <map> --plan <plan.toml> --out <map>"); return 2;
            }
        }
        if (basePath is null || planPath is null || outPath is null) { Console.Error.WriteLine("usage: map stamp --base <map> --plan <plan.toml> --out <map>"); return 2; }
        try
        {
            (MapData map, MapMeta meta) = MapFile.LoadWithMeta(basePath);
            TomlTable plan = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(planPath)) ?? new TomlTable();
            MapSymmetry sym = MapSymmetry.None;
            if (plan.TryGetValue("symmetry", out object? sv))
            {
                int k = Array.IndexOf(MapFile.SymmetryKeys, sv as string);
                if (k < 0) throw new InvalidOperationException("plan symmetry must be one of " + string.Join(", ", MapFile.SymmetryKeys));
                sym = (MapSymmetry)k;
            }
            var canvas = new StampCanvas(map.Width, map.Height, map.Land.ToArray(), map.HeightLevel.ToArray(), map.Overlay.ToArray(), map.Density.ToArray(),
                map.Decor?.ToArray(), map.StartPositions) { StartKeepRadius = StartKeep };
            var keep = new List<int[]>();
            foreach (TomlTable t in Tables(plan, "keep")) keep.Add(Rect(t, "keep"));
            var ctx = new Ctx(canvas, sym, keep);
            int ops = 0;
            foreach (TomlTable t in Tables(plan, "stamp")) { DoStamp(ctx, t); ops++; }
            foreach (TomlTable t in Tables(plan, "patch")) { DoPatch(ctx, t); ops++; }
            foreach (TomlTable t in Tables(plan, "prop")) { DoProp(ctx, t); ops++; }

            var result = new MapData(map.Name, map.Theater, map.Width, map.Height, map.Template.ToArray(), canvas.Land, canvas.HeightLevel, canvas.Overlay,
                canvas.Density, map.StartPositions, canvas.Decor);
            bool hasMeta = meta.Biome.Length > 0 || meta.Generator is not null || meta.Features.Count > 0 || meta.ConvoyEdge.Count > 0;
            MapFile.Save(result, hasMeta ? meta : null, outPath);
            int errors = 0;
            (MapData reloaded, MapMeta rmeta) = MapFile.LoadWithMeta(outPath);
            foreach (MapIssue issue in MapValidator.Validate(reloaded, rmeta, null))
            {
                if (issue.Severity == MapIssueSeverity.Error) errors++;
                string at = issue.Cell is { } c ? $" at ({c.X},{c.Y})" : "";
                Console.WriteLine($"{(issue.Severity == MapIssueSeverity.Error ? "error" : "warning")} {issue.Code}{at}: {issue.Message}");
            }
            Console.WriteLine($"{outPath}: {ops} op(s), {errors} validator error(s), map hash 0x{MapHasher.Compute(reloaded):x16}");
            return errors > 0 ? 1 : 0;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or RulesException or MapFileException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    private sealed record Ctx(StampCanvas Canvas, MapSymmetry Sym, List<int[]> Keep)
    {
        /// <summary>Cells already written by an earlier op (an op may not overwrite another).</summary>
        public bool[] Touched { get; } = new bool[Canvas.Width * Canvas.Height];
    }

    private static void DoStamp(Ctx ctx, TomlTable t)
    {
        string id = Str(t, "id");
        Stamp s = StampLibrary.Default.Find(id) ?? throw new InvalidOperationException($"unknown stamp '{id}'");
        int x = Int(t, "x"), y = Int(t, "y"), rot = IntOr(t, "rot", 0);
        bool mirror = t.TryGetValue("mirror", out object? m) && m is true;
        StampResult r = StampLibrary.Apply(s, ctx.Canvas, x, y, rot, mirror);
        if (r.Refused.Count > 0) throw new InvalidOperationException($"stamp {id} at ({x},{y}): cell ({r.Refused[0].X},{r.Refused[0].Y}) refused (off map, start square or resource)");
        Finish(ctx, t, $"stamp {id} at ({x},{y})", r.Changed);
    }

    private static void DoPatch(Ctx ctx, TomlTable t)
    {
        LandType land = Enum.TryParse(Str(t, "land"), true, out LandType l) && Enum.IsDefined(l) ? l : throw new InvalidOperationException("unknown land type");
        int[] r = Rect(t, "patch");
        var changed = new List<int>();
        for (int y = r[1]; y <= r[3]; y++)
            for (int x = r[0]; x <= r[2]; x++)
            {
                Check(ctx, x, y, "patch");
                int i = y * ctx.Canvas.Width + x;
                if (ctx.Canvas.Land[i] != land) { ctx.Canvas.Land[i] = land; changed.Add(i); }
            }
        Finish(ctx, t, "patch", changed);
    }

    private static void DoProp(Ctx ctx, TomlTable t)
    {
        OverlayKind kind = Enum.TryParse(Str(t, "overlay"), true, out OverlayKind o) && OverlayKinds.IsProp(o) ? o : throw new InvalidOperationException("overlay must be a prop kind");
        int x = Int(t, "x"), y = Int(t, "y");
        Check(ctx, x, y, "prop");
        int i = y * ctx.Canvas.Width + x;
        ctx.Canvas.Overlay[i] = kind; ctx.Canvas.Density[i] = (byte)IntOr(t, "density", 0);
        Finish(ctx, t, $"prop at ({x},{y})", new[] { i });
    }

    /// <summary>Verifies every changed cell (and its images) against the keep-outs, then copies the cell to its images.</summary>
    private static void Finish(Ctx ctx, TomlTable t, string what, IReadOnlyList<int> changed)
    {
        StampCanvas c = ctx.Canvas;
        bool sym = !(t.TryGetValue("sym", out object? sv) && sv is false);
        foreach (int i in changed)
        {
            int x = i % c.Width, y = i / c.Width;
            Check(ctx, x, y, what);
            Touch(ctx, i, what);
            if (!sym || ctx.Sym == MapSymmetry.None) continue;
            (int, int)[] im = MapSymmetries.Images(ctx.Sym, c.Width, c.Height, x, y);
            for (int k = 1; k < im.Length; k++)
            {
                Check(ctx, im[k].Item1, im[k].Item2, what + " (image)");
                int j = im[k].Item2 * c.Width + im[k].Item1;
                Touch(ctx, j, what + " (image)");
                c.Land[j] = c.Land[i]; c.HeightLevel[j] = c.HeightLevel[i]; c.Overlay[j] = c.Overlay[i]; c.Density[j] = c.Density[i];
                if (c.Decor is not null) c.Decor[j] = c.Decor[i];
            }
        }
    }

    private static void Touch(Ctx ctx, int i, string what)
    {
        if (ctx.Touched[i]) throw new InvalidOperationException($"{what}: cell ({i % ctx.Canvas.Width},{i / ctx.Canvas.Width}) was already written by an earlier op");
        ctx.Touched[i] = true;
    }

    private static void Check(Ctx ctx, int x, int y, string what)
    {
        StampCanvas c = ctx.Canvas;
        if (x < 0 || y < 0 || x >= c.Width || y >= c.Height) throw new InvalidOperationException($"{what}: cell ({x},{y}) is off the map");
        if (c.NearStart(x, y)) throw new InvalidOperationException($"{what}: cell ({x},{y}) is in a start square");
        foreach (int[] r in ctx.Keep)
            if (x >= r[0] && x <= r[2] && y >= r[1] && y <= r[3]) throw new InvalidOperationException($"{what}: cell ({x},{y}) is in a keep-out rect");
    }

    private static IEnumerable<TomlTable> Tables(TomlTable root, string key)
    {
        if (!root.TryGetValue(key, out object? v)) yield break;
        if (v is not TomlTableArray arr) throw new InvalidOperationException($"plan.{key} must be an array of tables");
        foreach (TomlTable t in arr) yield return t;
    }

    private static int[] Rect(TomlTable t, string what)
    {
        if (!t.TryGetValue("rect", out object? v) || v is not TomlArray a || a.Count != 4) throw new InvalidOperationException($"{what}: rect = [x0, y0, x1, y1] required");
        var r = new int[4];
        for (int i = 0; i < 4; i++) r[i] = (int)Convert.ToInt64(a[i], CultureInfo.InvariantCulture);
        if (r[0] > r[2] || r[1] > r[3]) throw new InvalidOperationException($"{what}: rect corners out of order");
        return r;
    }

    private static string Str(TomlTable t, string k) => t.TryGetValue(k, out object? v) && v is string s ? s : throw new InvalidOperationException($"plan entry needs string '{k}'");
    private static int Int(TomlTable t, string k) => t.TryGetValue(k, out object? v) && v is long l ? (int)l : throw new InvalidOperationException($"plan entry needs integer '{k}'");
    private static int IntOr(TomlTable t, string k, int d) => t.TryGetValue(k, out object? v) && v is long l ? (int)l : d;
}
