// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using System.Text;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>
/// Reads and writes <c>&lt;name&gt;.map.toml</c>. <c>[map]</c> holds metadata; <c>[layers]</c> holds each cell
/// layer as a base64 string of the packed little-endian array (template = ushort, others = byte).
/// Format 2 (M4-01, design 08 §1) adds optional <c>[map]</c> keys <c>biome</c>, <c>symmetry</c>, <c>max_players</c>, an optional
/// <c>[map.generator]</c> table (biome, size s|m|l, players, seed; informational) and an optional <c>[[features]]</c> array
/// (<c>type</c>, <c>cell = [x, y]</c>). Version 1 files still load, with <see cref="MapMeta.DefaultFor"/>. None of it enters the map hash.
/// Format 3 (M7-02, docs/design/11_m7_plan.md §2.2) allows <see cref="LandType.Coast"/> (value 8) in the land layer and adds optional
/// <c>[map]</c> keys <c>convoy_edge</c> and <c>landing</c> (lists of <c>[x, y]</c> marker cells; see <see cref="MapMeta"/>). Versions 1 and 2 still load.
/// Format 4 (M12-04, docs/design/16_maps_overhaul.md §2.4) allows the blocking prop overlay kinds (<see cref="OverlayKind.Tree"/> to
/// <see cref="OverlayKind.Snag"/>; the density byte is the sprite variant), adds an optional <c>[layers] decor</c> (one decal byte per cell;
/// render only, not in the map hash) and an optional <c>layout</c> key in <c>[map.generator]</c> (the generator version; absent = 0).
/// Versions 1 to 3 still load.
/// </summary>
public static class MapFile
{
    public const int FormatVersion = 4;

    /// <summary>Oldest format version <see cref="Load"/> still reads.</summary>
    public const int OldestReadableVersion = 1;

    /// <summary>Symmetry names as written in <c>[map] symmetry</c>, in <see cref="MapSymmetry"/> order.</summary>
    public static readonly string[] SymmetryKeys = ["none", "rot2", "rot4", "mirror_x", "mirror_y", "mirror_diag"];

    /// <summary>Biome names as written in <c>biome</c> keys, in <see cref="Biome"/> order.</summary>
    public static readonly string[] BiomeKeys = ["rust_belt", "glass_coast", "mountains", "ash_plains"];

    private static readonly string[] SizeKeys = ["s", "m", "l"];

    /// <summary>Writes the map with no metadata (format 4; loads back with <see cref="MapMeta.DefaultFor"/>).</summary>
    public static void Save(MapData map, string path) => Save(map, null, path);

    /// <summary>Writes the map and, when given, its metadata (format 4).</summary>
    public static void Save(MapData map, MapMeta? meta, string path)
    {
        ArgumentNullException.ThrowIfNull(map);
        var sb = new StringBuilder();
        sb.Append("[map]\n");
        sb.Append("name = ").Append(Quote(map.Name)).Append('\n');
        sb.Append("theater = ").Append(Quote(map.Theater)).Append('\n');
        sb.Append("width = ").Append(map.Width.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("height = ").Append(map.Height.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("format_version = ").Append(FormatVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("start_positions = [");
        for (int i = 0; i < map.StartPositions.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('[').Append(map.StartPositions[i].X.ToString(CultureInfo.InvariantCulture)).Append(", ")
              .Append(map.StartPositions[i].Y.ToString(CultureInfo.InvariantCulture)).Append(']');
        }
        sb.Append("]\n");
        if (meta is not null) AppendMeta(sb, meta);
        sb.Append("\n[layers]\n");

        int n = map.Template.Count;
        var tpl = new byte[n * 2];
        for (int i = 0; i < n; i++) { tpl[i * 2] = (byte)(map.Template[i] & 0xFF); tpl[i * 2 + 1] = (byte)(map.Template[i] >> 8); }
        var land = new byte[n]; var ovl = new byte[n]; var hgt = new byte[n]; var den = new byte[n];
        for (int i = 0; i < n; i++) { land[i] = (byte)map.Land[i]; ovl[i] = (byte)map.Overlay[i]; hgt[i] = map.HeightLevel[i]; den[i] = map.Density[i]; }
        sb.Append("template = \"").Append(Convert.ToBase64String(tpl)).Append("\"\n");
        sb.Append("land = \"").Append(Convert.ToBase64String(land)).Append("\"\n");
        sb.Append("height = \"").Append(Convert.ToBase64String(hgt)).Append("\"\n");
        sb.Append("overlay = \"").Append(Convert.ToBase64String(ovl)).Append("\"\n");
        sb.Append("density = \"").Append(Convert.ToBase64String(den)).Append("\"\n");
        if (map.Decor is { } decor && decor.Any(b => b != 0)) sb.Append("decor = \"").Append(Convert.ToBase64String(decor.ToArray())).Append("\"\n");   // format 4, only when used

        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    /// <summary>Reads a map file (format 1 to 3); metadata is ignored.</summary>
    public static MapData Load(string path) => LoadWithMeta(path).Map;

    /// <summary>Reads a map file and its metadata (format 1 to 4; a version-1 file gets <see cref="MapMeta.DefaultFor"/>).</summary>
    public static (MapData Map, MapMeta Meta) LoadWithMeta(string path)
    {
        string text;
        try { text = File.ReadAllText(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new MapFileException(path, "cannot read file: " + e.Message, e); }

        TomlTable root;
        try { root = TomlSerializer.Deserialize<TomlTable>(text) ?? throw new MapFileException(path, "empty file"); }
        catch (MapFileException) { throw; }
        catch (Exception e) { throw new MapFileException(path, "invalid TOML: " + e.Message, e); }

        var m = Section(root, "map", path);
        var l = Section(root, "layers", path);

        int version = Int(m, "format_version", path);
        if (version < OldestReadableVersion || version > FormatVersion) throw new MapFileException(path, $"unsupported format_version {version}");
        string name = Str(m, "name", path);
        string theater = Str(m, "theater", path);
        int w = Int(m, "width", path), h = Int(m, "height", path);
        if (w < 1 || w > SimConstants.MaxMapCells || h < 1 || h > SimConstants.MaxMapCells)
            throw new MapFileException(path, $"map size {w}x{h} out of range 1..{SimConstants.MaxMapCells}");
        int n = w * h;

        var starts = new List<CellCoord>();
        if (!m.TryGetValue("start_positions", out var sp) || sp is not TomlArray spa)
            throw new MapFileException(path, "map.start_positions missing or not an array");
        foreach (var item in spa)
        {
            if (item is not TomlArray pair || pair.Count != 2 || !IsInt(pair[0]) || !IsInt(pair[1]))
                throw new MapFileException(path, "map.start_positions entries must be [x, y]");
            int x = (int)Convert.ToInt64(pair[0], CultureInfo.InvariantCulture), y = (int)Convert.ToInt64(pair[1], CultureInfo.InvariantCulture);
            if (x < 0 || y < 0 || x >= w || y >= h) throw new MapFileException(path, $"start position [{x}, {y}] outside map");
            starts.Add(new CellCoord(x, y));
        }

        byte[] tplB = Layer(l, "template", n * 2, path);
        byte[] landB = Layer(l, "land", n, path);
        byte[] hgtB = Layer(l, "height", n, path);
        byte[] ovlB = Layer(l, "overlay", n, path);
        byte[] denB = Layer(l, "density", n, path);

        var tpl = new ushort[n];
        for (int i = 0; i < n; i++) tpl[i] = (ushort)(tplB[i * 2] | (tplB[i * 2 + 1] << 8));
        var land = new LandType[n]; var ovl = new OverlayKind[n];
        for (int i = 0; i < n; i++)
        {
            if (landB[i] >= RuleCounts.LandTypes) throw new MapFileException(path, $"layers.land: invalid land type {landB[i]} at cell {i}");
            if (ovlB[i] > (byte)OverlayKind.Wall && version < 4) throw new MapFileException(path, $"layers.overlay: prop {ovlB[i]} at cell {i} needs format_version 4");
            if (ovlB[i] > (byte)OverlayKinds.Last) throw new MapFileException(path, $"layers.overlay: invalid overlay {ovlB[i]} at cell {i}");
            land[i] = (LandType)landB[i]; ovl[i] = (OverlayKind)ovlB[i];
        }
        byte[]? decor = version >= 4 && l.ContainsKey("decor") ? Layer(l, "decor", n, path) : null;
        var map = new MapData(name, theater, w, h, tpl, land, hgtB, ovl, denB, starts, decor);
        MapMeta meta = version >= 2 ? ReadMeta(root, m, map, version, path) : MapMeta.DefaultFor(map);
        // S19 (D-205): "dev = true" marks a developer fixture. The skirmish map list hides it; every loader still reads it.
        if (m.TryGetValue("dev", out object? dev) && dev is true) meta = meta with { Dev = true };
        return (map, meta);
    }

    /// <summary>The letters n, e, s, w (in that order) of the edges set in <paramref name="edges"/>, e.g. "ns".</summary>
    public static string OceanEdgeKeys(OceanEdges edges)
    {
        var sb = new StringBuilder();
        for (int b = 0; b < 4; b++) if (((int)edges & (1 << b)) != 0) sb.Append("nesw"[b]);
        return sb.ToString();
    }

    private static void AppendMeta(StringBuilder sb, MapMeta meta)
    {
        sb.Append("biome = ").Append(Quote(meta.Biome)).Append('\n');
        sb.Append("symmetry = ").Append(Quote(SymmetryKeys[(int)meta.Symmetry])).Append('\n');
        sb.Append("max_players = ").Append(meta.MaxPlayers.ToString(CultureInfo.InvariantCulture)).Append('\n');
        if (meta.Dev) sb.Append("dev = true\n");
        AppendCells(sb, "convoy_edge", meta.ConvoyEdge);
        AppendCells(sb, "landing", meta.Landing);
        if (meta.Generator is { } g)
        {
            sb.Append("\n[map.generator]\n");
            sb.Append("biome = ").Append(Quote(BiomeKeys[(int)g.Biome])).Append('\n');
            sb.Append("size = ").Append(Quote(SizeKeys[(int)g.Size])).Append('\n');
            sb.Append("players = ").Append(g.Players.ToString(CultureInfo.InvariantCulture)).Append('\n');
            // TOML integers are signed 64-bit: a seed above long.MaxValue is written as a decimal string.
            sb.Append("seed = ").Append(g.Seed <= long.MaxValue ? g.Seed.ToString(CultureInfo.InvariantCulture)
                : Quote(g.Seed.ToString(CultureInfo.InvariantCulture))).Append('\n');
            if (g.OceanEdges != OceanEdges.None) sb.Append("ocean_edges = ").Append(Quote(OceanEdgeKeys(g.OceanEdges))).Append('\n');   // M7-13, only when on
            if (g.Stamps) sb.Append("stamps = true\n");   // M12-13, only when on
            sb.Append("layout = ").Append(g.Layout.ToString(CultureInfo.InvariantCulture)).Append('\n');   // M12-05: generator version
        }
        foreach (MapFeature f in meta.Features)
        {
            sb.Append("\n[[features]]\n");
            sb.Append("type = ").Append(Quote(f.Type)).Append('\n');
            sb.Append("cell = [").Append(f.Cell.X.ToString(CultureInfo.InvariantCulture)).Append(", ")
              .Append(f.Cell.Y.ToString(CultureInfo.InvariantCulture)).Append("]\n");
        }
    }

    private static void AppendCells(StringBuilder sb, string key, IReadOnlyList<CellCoord> cells)
    {
        if (cells.Count == 0) return;
        sb.Append(key).Append(" = [");
        for (int i = 0; i < cells.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('[').Append(cells[i].X.ToString(CultureInfo.InvariantCulture)).Append(", ")
              .Append(cells[i].Y.ToString(CultureInfo.InvariantCulture)).Append(']');
        }
        sb.Append("]\n");
    }

    private static IReadOnlyList<CellCoord> ReadCells(TomlTable m, string key, MapData map, string path)
    {
        var cells = new List<CellCoord>();
        if (!m.TryGetValue(key, out var v)) return cells;
        if (v is not TomlArray list) throw new MapFileException(path, $"map.{key} must be a list of [x, y]");
        foreach (var item in list)
        {
            if (item is not TomlArray pair || pair.Count != 2 || !IsInt(pair[0]) || !IsInt(pair[1]))
                throw new MapFileException(path, $"map.{key} entries must be [x, y]");
            int x = (int)Convert.ToInt64(pair[0], CultureInfo.InvariantCulture), y = (int)Convert.ToInt64(pair[1], CultureInfo.InvariantCulture);
            if (!map.Contains(new CellCoord(x, y))) throw new MapFileException(path, $"map.{key} cell [{x}, {y}] outside map");
            cells.Add(new CellCoord(x, y));
        }
        return cells.Count == 0 ? Array.Empty<CellCoord>() : cells;
    }

    private static MapMeta ReadMeta(TomlTable root, TomlTable m, MapData map, int version, string path)
    {
        string biome = m.ContainsKey("biome") ? Str(m, "biome", path) : "";
        MapSymmetry symmetry = MapSymmetry.None;
        if (m.ContainsKey("symmetry"))
        {
            int s = Array.IndexOf(SymmetryKeys, Str(m, "symmetry", path));
            if (s < 0) throw new MapFileException(path, $"map.symmetry must be one of {string.Join(", ", SymmetryKeys)}");
            symmetry = (MapSymmetry)s;
        }
        int maxPlayers = m.ContainsKey("max_players") ? Int(m, "max_players", path) : map.StartPositions.Count;
        if (maxPlayers < 1) throw new MapFileException(path, "map.max_players must be at least 1");

        MapGenParams? generator = null;
        if (m.TryGetValue("generator", out var gv))
        {
            if (gv is not TomlTable g) throw new MapFileException(path, "map.generator must be a table");
            int b = Array.IndexOf(BiomeKeys, Str(g, "biome", path));
            int z = Array.IndexOf(SizeKeys, Str(g, "size", path));
            if (b < 0) throw new MapFileException(path, $"map.generator.biome must be one of {string.Join(", ", BiomeKeys)}");
            if (z < 0) throw new MapFileException(path, "map.generator.size must be one of s, m, l");
            int players = Int(g, "players", path);
            ulong seed = g.TryGetValue("seed", out var sv) && sv is string ss && ulong.TryParse(ss, NumberStyles.None, CultureInfo.InvariantCulture, out ulong big)
                ? big : (ulong)Int64(g, "seed", path);
            OceanEdges ocean = OceanEdges.None;
            if (g.ContainsKey("ocean_edges"))
            {
                foreach (char ch in Str(g, "ocean_edges", path))
                {
                    int bit = "nesw".IndexOf(ch);
                    if (bit < 0) throw new MapFileException(path, "map.generator.ocean_edges must use only the letters n, e, s, w");
                    ocean |= (OceanEdges)(1 << bit);
                }
            }
            int layout = g.ContainsKey("layout") ? Int(g, "layout", path) : 0;   // M12-05: files written before the version field name generator 0
            generator = new MapGenParams((Biome)b, (MapSize)z, players, seed, null, ocean, layout, g.TryGetValue("stamps", out var stv) && stv is true);
        }

        var features = new List<MapFeature>();
        if (root.TryGetValue("features", out var fv))
        {
            if (fv is not TomlTableArray fa) throw new MapFileException(path, "features must be an array of [[features]] tables");
            foreach (TomlTable t in fa)
            {
                string type = t.TryGetValue("type", out var tv) && tv is string ts && ts.Length > 0
                    ? ts : throw new MapFileException(path, "features.type missing or empty");
                if (!t.TryGetValue("cell", out var cv) || cv is not TomlArray pair || pair.Count != 2 || !IsInt(pair[0]) || !IsInt(pair[1]))
                    throw new MapFileException(path, "features.cell must be [x, y]");
                int x = (int)Convert.ToInt64(pair[0], CultureInfo.InvariantCulture), y = (int)Convert.ToInt64(pair[1], CultureInfo.InvariantCulture);
                if (!map.Contains(new CellCoord(x, y))) throw new MapFileException(path, $"feature cell [{x}, {y}] outside map");
                features.Add(new MapFeature(type, new CellCoord(x, y)));
            }
        }
        return new MapMeta(biome, symmetry, maxPlayers, generator, features)
        {
            ConvoyEdge = version >= 3 ? ReadCells(m, "convoy_edge", map, path) : Array.Empty<CellCoord>(),
            Landing = version >= 3 ? ReadCells(m, "landing", map, path) : Array.Empty<CellCoord>(),
        };
    }

    private static long Int64(TomlTable t, string key, string path) =>
        t.TryGetValue(key, out var v) && IsInt(v) ? Convert.ToInt64(v, CultureInfo.InvariantCulture)
            : throw new MapFileException(path, $"map.{key} missing or not an integer");

    private static TomlTable Section(TomlTable root, string key, string path) =>
        root.TryGetValue(key, out var v) && v is TomlTable t ? t : throw new MapFileException(path, $"missing [{key}] section");

    private static string Str(TomlTable t, string key, string path) =>
        t.TryGetValue(key, out var v) && v is string s ? s : throw new MapFileException(path, $"map.{key} missing or not a string");

    private static bool IsInt(object? o) => o is long or int;

    private static int Int(TomlTable t, string key, string path) =>
        t.TryGetValue(key, out var v) && IsInt(v) ? (int)Convert.ToInt64(v, CultureInfo.InvariantCulture)
            : throw new MapFileException(path, $"map.{key} missing or not an integer");

    private static byte[] Layer(TomlTable t, string key, int expected, string path)
    {
        if (!t.TryGetValue(key, out var v) || v is not string s)
            throw new MapFileException(path, $"layers.{key} missing or not a string");
        byte[] b;
        try { b = Convert.FromBase64String(s); }
        catch (FormatException) { throw new MapFileException(path, $"layers.{key} is not valid base64"); }
        if (b.Length != expected) throw new MapFileException(path, $"layers.{key} has {b.Length} bytes, expected {expected}");
        return b;
    }

    private static string Quote(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
