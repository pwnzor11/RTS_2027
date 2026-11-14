// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Tools;

/// <summary>`Tools map` subcommands: <c>new</c>, <c>gen</c>, <c>validate</c>, <c>info</c> and <c>ascii</c>. Exit codes: 0 ok, 1 error
/// (for <c>validate</c>: the map has errors), 2 usage.</summary>
public static class MapCommands
{
    private const string Usage =
        "usage: map new --size <n> --seed <s> --out <path>\n" +
        "       map gen --biome rust_belt|glass_coast|mountains|ash_plains --size s|m|l --players 2|3|4 --seed <s> --out <path> [--ocean n|e|s|w letters, e.g. ns] [--layout 1|2] [--stamps] [--ascii]\n" +
        "       map validate <path> [--rules-dir <dir>]\n" +
        "       map info <path>\n" +
        "       map ascii <path>\n" +
        "       map report <path> | <gen options>        (measured table of design 16 section 1.2; gen options as for map gen, without --out)\n" +
        "       map render <path> | <gen options> --png <out.png> [--scale <px per cell, default 6>]\n" +
        "       (report/render: --layout 1|2 picks the generator; --stamps adds 2-4 stamps (M12-13); --raw shows layout 2 seed as is, with no retry; --checks adds the layout checks to a layout 1 report)";

    public static int Run(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine(Usage); return 2; }
        switch (args[0])
        {
            case "new": return New(args[1..]);
            case "info": return Info(args[1..]);
            case "gen": return Gen(args[1..]);
            case "validate": return ValidateMap(args[1..]);
            case "ascii": return Ascii(args[1..]);
            case "report": return Report(args[1..], render: false);
            case "render": return Report(args[1..], render: true);
            default:
                Console.Error.WriteLine($"error: unknown map command '{args[0]}'");
                Console.Error.WriteLine(Usage);
                return 2;
        }
    }

    private static int New(string[] args)
    {
        int? size = null; ulong? seed = null; string? outPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (i + 1 >= args.Length || a is not ("--size" or "--seed" or "--out"))
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }
            string v = args[++i];
            switch (a)
            {
                case "--size":
                    if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int sz)) { Console.Error.WriteLine(Usage); return 2; }
                    size = sz; break;
                case "--seed":
                    if (!ulong.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out ulong sd)) { Console.Error.WriteLine(Usage); return 2; }
                    seed = sd; break;
                default: outPath = v; break;
            }
        }
        if (size is null || seed is null || outPath is null) { Console.Error.WriteLine(Usage); return 2; }

        try
        {
            string name = Path.GetFileName(outPath);
            const string suffix = ".map.toml";
            name = name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? name[..^suffix.Length] : Path.GetFileNameWithoutExtension(name);
            MapData map = MapGenerator.Generate(size.Value, seed.Value, name);
            MapFile.Save(map, outPath);
            Console.WriteLine($"wrote {outPath} {map.Width}x{map.Height} map hash 0x{MapHasher.Compute(map):x16}");
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    private static int Gen(string[] args)
    {
        Biome? biome = null; MapSize? size = null; int? players = null; ulong? seed = null; string? outPath = null; bool ascii = false;
        int layout = MapGenerator.CurrentLayout; bool stamps = false;
        OceanEdges ocean = OceanEdges.None;   // M7-13: --ocean ns turns the north and south edges (and their symmetry images) into sea
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "--ascii") { ascii = true; continue; }
            if (a == "--stamps") { stamps = true; continue; }
            if (i + 1 >= args.Length || a is not ("--biome" or "--size" or "--players" or "--seed" or "--out" or "--ocean" or "--layout")) { Console.Error.WriteLine(Usage); return 2; }
            string v = args[++i];
            switch (a)
            {
                case "--layout":
                    if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int lay) || lay < 1 || lay > 2) { Console.Error.WriteLine("error: --layout must be 1 or 2"); return 2; }
                    layout = lay; break;
                case "--biome":
                    int b = Array.IndexOf(MapFile.BiomeKeys, v);
                    if (b < 0) { Console.Error.WriteLine($"error: unknown biome '{v}' (one of {string.Join(", ", MapFile.BiomeKeys)})"); return 2; }
                    biome = (Biome)b; break;
                case "--size":
                    size = v switch { "s" or "S" => MapSize.Small, "m" or "M" => MapSize.Medium, "l" or "L" => MapSize.Large, _ => null };
                    if (size is null) { Console.Error.WriteLine("error: --size must be s, m or l"); return 2; }
                    break;
                case "--players":
                    if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int pl) || pl < 2 || pl > 4)
                    { Console.Error.WriteLine("error: --players must be 2, 3 or 4"); return 2; }
                    players = pl; break;
                case "--seed":
                    if (!ulong.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out ulong sd)) { Console.Error.WriteLine(Usage); return 2; }
                    seed = sd; break;
                case "--ocean":
                    foreach (char ch in v)
                    {
                        int bit = "nesw".IndexOf(char.ToLowerInvariant(ch));
                        if (bit < 0) { Console.Error.WriteLine("error: --ocean takes the letters n, e, s, w"); return 2; }
                        ocean |= (OceanEdges)(1 << bit);
                    }
                    break;
                default: outPath = v; break;
            }
        }
        if (biome is null || size is null || players is null || seed is null || outPath is null) { Console.Error.WriteLine(Usage); return 2; }
        try
        {
            var p = new MapGenParams(biome.Value, size.Value, players.Value, seed.Value, null, ocean, layout, stamps);
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(p);
            long ms = (long)System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            MapFile.Save(map, meta, outPath);
            Console.WriteLine($"wrote {outPath} {map.Name} {map.Width}x{map.Height} {MapFile.SymmetryKeys[(int)meta.Symmetry]} " +
                $"{map.StartPositions.Count} starts, {meta.Features.Count} features, {meta.ConvoyEdge.Count} convoy cells, {meta.Landing.Count} landings, map hash 0x{MapHasher.Compute(map):x16} ({ms} ms)");
            if (ascii) PrintAscii(map, meta);
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or RulesException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    private static int ValidateMap(string[] args)
    {
        string? path = null, rulesDir = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--rules-dir" or "--rules" && i + 1 < args.Length) { rulesDir = args[++i]; continue; }
            if (path is not null || args[i].StartsWith("--", StringComparison.Ordinal)) { Console.Error.WriteLine(Usage); return 2; }
            path = args[i];
        }
        if (path is null) { Console.Error.WriteLine(Usage); return 2; }
        try
        {
            (MapData map, MapMeta meta) = MapFile.LoadWithMeta(path);
            TerrainTable? terrain = rulesDir is null ? null : RulesLoader.Load(rulesDir).Sim.Terrain;
            IReadOnlyList<MapIssue> issues = MapValidator.Validate(map, meta, terrain);
            int errors = 0;
            foreach (MapIssue issue in issues)
            {
                if (issue.Severity == MapIssueSeverity.Error) errors++;
                string at = issue.Cell is { } c ? $" at ({c.X},{c.Y})" : "";
                Console.WriteLine($"{(issue.Severity == MapIssueSeverity.Error ? "error" : "warning")} {issue.Code}{at}: {issue.Message}");
            }
            Console.WriteLine($"{path}: {errors} error(s), {issues.Count - errors} warning(s) ({(terrain is null ? "built-in terrain rule" : "terrain from " + rulesDir)})");
            return errors > 0 ? 1 : 0;
        }
        catch (Exception e) when (e is MapFileException or RulesException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    /// <summary>`map report` and `map render`: the source is a map file path or the generator options (--biome --size --players --seed [--ocean]).</summary>
    private static int Report(string[] args, bool render)
    {
        string? path = null, png = null; int scale = 6;
        Biome? biome = null; MapSize? size = null; int? players = null; ulong? seed = null; OceanEdges ocean = OceanEdges.None;
        int layout = MapGenerator.CurrentLayout; bool raw = false, checks = false, stamps = false;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (!a.StartsWith("--", StringComparison.Ordinal)) { if (path is not null) { Console.Error.WriteLine(Usage); return 2; } path = a; continue; }
            if (a == "--raw") { raw = true; continue; }
            if (a == "--stamps") { stamps = true; continue; }
            if (a == "--checks") { checks = true; continue; }
            if (i + 1 >= args.Length) { Console.Error.WriteLine(Usage); return 2; }
            string v = args[++i];
            switch (a)
            {
                case "--layout":
                    if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out layout) || layout < 1 || layout > 2) { Console.Error.WriteLine("error: --layout must be 1 or 2"); return 2; }
                    break;
                case "--png": png = v; break;
                case "--scale": if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out scale) || scale < 1 || scale > 16) { Console.Error.WriteLine("error: --scale must be 1..16"); return 2; } break;
                case "--biome":
                    int b = Array.IndexOf(MapFile.BiomeKeys, v);
                    if (b < 0) { Console.Error.WriteLine($"error: unknown biome '{v}'"); return 2; }
                    biome = (Biome)b; break;
                case "--size": size = v switch { "s" or "S" => MapSize.Small, "m" or "M" => MapSize.Medium, "l" or "L" => MapSize.Large, _ => null }; if (size is null) { Console.Error.WriteLine("error: --size must be s, m or l"); return 2; } break;
                case "--players": if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int pl) || pl < 2 || pl > 4) { Console.Error.WriteLine("error: --players must be 2, 3 or 4"); return 2; } players = pl; break;
                case "--seed": if (!ulong.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out ulong sd)) { Console.Error.WriteLine(Usage); return 2; } seed = sd; break;
                case "--ocean":
                    foreach (char ch in v) { int bit = "nesw".IndexOf(char.ToLowerInvariant(ch)); if (bit < 0) { Console.Error.WriteLine("error: --ocean takes n, e, s, w"); return 2; } ocean |= (OceanEdges)(1 << bit); }
                    break;
                default: Console.Error.WriteLine(Usage); return 2;
            }
        }
        bool gen = biome is not null || size is not null || players is not null || seed is not null;
        if ((path is null) == !gen || (gen && (biome is null || size is null || players is null || seed is null)) || (render && png is null)) { Console.Error.WriteLine(Usage); return 2; }
        try
        {
            MapData map; MapMeta meta; string? rawFailure = null; int attempts = 1;
            if (gen)
            {
                var gp = new MapGenParams(biome!.Value, size!.Value, players!.Value, seed!.Value, null, ocean, layout, stamps);
                if (raw && layout == MapGenerator.ZonedLayout) (map, meta) = MapGenerator.GenerateZonedRaw(gp, out rawFailure);
                else (map, meta) = MapGenerator.GenerateWithMeta(gp, Rts.Data.MapGen.BiomeCatalog.For(gp.Biome), out attempts);
            }
            else (map, meta) = MapFile.LoadWithMeta(path!);
            if (png is not null) { MapRender.WritePng(map, meta, scale, png); Console.WriteLine($"wrote {png} ({map.Width * scale}x{map.Height * scale})"); }
            if (!render)
            {
                foreach (string line in Rts.Data.MapGen.MapReport.Measure(map, meta).Lines()) Console.WriteLine(line);
                if (checks || meta.Generator?.Layout == MapGenerator.ZonedLayout)
                {
                    Console.WriteLine($"layout checks (layout {meta.Generator?.Layout ?? 0}, attempts {attempts}{(rawFailure is null ? "" : ", raw seed rejected: " + rawFailure)}):");
                    foreach (Rts.Data.MapGen.LayoutCheck c in Rts.Data.MapGen.LayoutChecks.Evaluate(map, meta))
                        Console.WriteLine($"  {(c.Pass ? "pass" : "FAIL")} {c.Name,-20} {c.Value}   [{c.Target}]");
                }
            }
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or RulesException or MapFileException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    private static int Ascii(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine(Usage); return 2; }
        try
        {
            (MapData map, MapMeta meta) = MapFile.LoadWithMeta(args[0]);
            PrintAscii(map, meta);
            return 0;
        }
        catch (MapFileException e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    /// <summary>One character per cell: 1-4 starts, F feature, $ Ashfall, P Pitchblende, ~ deep water, , shallow, # rock, X cliff,
    /// = road, : rough, ^ height 1, A height 2+, . flat ground; props t tree, w wreck, b block, p pylon, s snag.</summary>
    private static void PrintAscii(MapData map, MapMeta meta)
    {
        bool[]? features = Rts.Data.MapGen.MapAnalysis.FeatureMask(map, meta.Features);
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int i = y * map.Width + x;
                int start = -1;
                for (int s = 0; s < map.StartPositions.Count; s++) if (map.StartPositions[s] == new CellCoord(x, y)) start = s;
                char ch;
                if (start >= 0) ch = (char)('1' + start);
                else if (features is not null && features[i]) ch = 'F';
                else if (map.Overlay[i] == OverlayKind.Ashfall) ch = '$';
                else if (map.Overlay[i] == OverlayKind.Pitchblende) ch = 'P';
                else if (OverlayKinds.IsProp(map.Overlay[i])) ch = "twbps"[(int)map.Overlay[i] - (int)OverlayKind.Tree];   // M12: tree wreck block pylon snag
                else ch = map.Land[i] switch
                {
                    LandType.Water => '~', LandType.Shallow => ',', LandType.Rock => '#', LandType.Cliff => 'X', LandType.Road => '=',
                    LandType.Rough => ':', _ => map.HeightLevel[i] >= 2 ? 'A' : map.HeightLevel[i] == 1 ? '^' : '.',
                };
                sb.Append(ch);
            }
            sb.Append('\n');
        }
        Console.Write(sb.ToString());
    }

    private static int Info(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine(Usage); return 2; }
        try
        {
            MapData map = MapFile.Load(args[0]);
            Console.WriteLine($"name: {map.Name}");
            Console.WriteLine($"size: {map.Width}x{map.Height}");
            Console.WriteLine($"theater: {map.Theater}");
            Console.WriteLine("start positions: " + string.Join(" ", map.StartPositions.Select(p => $"({p.X},{p.Y})")));
            var land = new int[RuleCounts.LandTypes];
            var overlay = new int[Enum.GetValues<OverlayKind>().Length];
            for (int i = 0; i < map.Land.Count; i++) { land[(int)map.Land[i]]++; overlay[(int)map.Overlay[i]]++; }
            Console.WriteLine("land: " + string.Join(", ", Enum.GetValues<LandType>().Select(l => $"{l}={land[(int)l]}")));
            Console.WriteLine("overlay: " + string.Join(", ", Enum.GetValues<OverlayKind>().Select(o => $"{o}={overlay[(int)o]}")));
            Console.WriteLine($"map hash: 0x{MapHasher.Compute(map):x16}");
            return 0;
        }
        catch (MapFileException e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }
}
