// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Data.Stamps;

/// <summary>The stamp library (M12-13): loads <c>data/stamps/*.toml</c> and applies stamps to a <see cref="StampCanvas"/>. Shared by the map
/// generator and the editor stamp tool. Integer maths only; files load in file-name order.</summary>
public sealed class StampLibrary
{
    private static readonly Lazy<StampLibrary> DefaultLazy = new(() =>
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "stamps"))) return Load(dir.FullName);
        throw new InvalidOperationException("data/stamps not found above " + AppContext.BaseDirectory);
    });

    private readonly List<Stamp> _all;

    private StampLibrary(List<Stamp> all) { _all = all; }

    /// <summary>The library found by walking up from the binaries' folder (like the biome file).</summary>
    public static StampLibrary Default => DefaultLazy.Value;

    /// <summary>Every stamp, ordered by id.</summary>
    public IReadOnlyList<Stamp> All => _all;

    /// <summary>The stamp with this id, or null.</summary>
    public Stamp? Find(string id)
    {
        foreach (Stamp s in _all) if (s.Id == id) return s;
        return null;
    }

    /// <summary>Loads every <c>*.toml</c> of <c>stamps/</c> under <paramref name="dataRoot"/>, which is either the game's <c>data</c> folder or
    /// the folder that holds it. Throws <see cref="RulesException"/> listing a bad file.</summary>
    public static StampLibrary Load(string dataRoot)
    {
        string dir = Path.Combine(dataRoot, "stamps");
        if (!Directory.Exists(dir)) dir = Path.Combine(dataRoot, "data", "stamps");
        if (!Directory.Exists(dir)) throw new RulesException(dir, "<folder>", "stamp folder not found");
        var files = Directory.GetFiles(dir, "*.toml");
        Array.Sort(files, StringComparer.Ordinal);
        var all = new List<Stamp>();
        foreach (string f in files) all.Add(Parse(File.ReadAllText(f), f));
        all.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        for (int i = 1; i < all.Count; i++)
            if (all[i].Id == all[i - 1].Id) throw new RulesException(dir, "id", "duplicate stamp id " + all[i].Id);
        return new StampLibrary(all);
    }

    /// <summary>Parses one stamp file's text.</summary>
    public static Stamp Parse(string text, string file)
    {
        TomlTable root;
        try { root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable(); }
        catch (Exception e) { throw new RulesException(file, "<toml>", "invalid TOML: " + e.Message); }
        string id = Str(root, "id", file), name = Str(root, "name", file);
        int w = Int(root, "width", file), h = Int(root, "height", file);
        if (w < 1 || h < 1 || w > 32 || h > 32) throw new RulesException(file, "width", "width and height must be 1..32");
        var biomes = new List<string>();
        if (root.TryGetValue("biomes", out object? bv))
        {
            if (bv is not TomlArray ba) throw new RulesException(file, "biomes", "must be an array");
            foreach (object? b in ba)
            {
                if (b is not string bs || Array.IndexOf(MapFile.BiomeKeys, bs) < 0) throw new RulesException(file, "biomes", "unknown biome " + b);
                biomes.Add(bs);
            }
        }
        if (!root.TryGetValue("rows", out object? rv) || rv is not TomlArray ra || ra.Count != h) throw new RulesException(file, "rows", "rows must list exactly height strings");
        var rows = new List<string>();
        foreach (object? r in ra)
        {
            if (r is not string rs || rs.Length != w) throw new RulesException(file, "rows", "every row must be a string of exactly width characters");
            rows.Add(rs);
        }
        var legend = new Dictionary<char, StampCell> { ['.'] = new StampCell(false) };
        if (root.TryGetValue("legend", out object? lv))
        {
            if (lv is not TomlTable lt) throw new RulesException(file, "legend", "must be a table");
            foreach (var kv in lt)
            {
                if (kv.Key.Length != 1 || kv.Key[0] == '.') throw new RulesException(file, "legend." + kv.Key, "legend keys are single characters other than '.'");
                if (kv.Value is not TomlTable ct) throw new RulesException(file, "legend." + kv.Key, "must be a table");
                legend[kv.Key[0]] = Cell(ct, file, "legend." + kv.Key);
            }
        }
        var cells = new StampCell[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!legend.TryGetValue(rows[y][x], out StampCell c)) throw new RulesException(file, "rows", $"character '{rows[y][x]}' at ({x},{y}) is not in the legend");
                cells[y * w + x] = c;
            }
        return new Stamp(id, name, w, h, biomes, rows, cells);
    }

    private static StampCell Cell(TomlTable t, string file, string key)
    {
        LandType? land = null; OverlayKind? overlay = null; byte? height = null, density = null, decor = null;
        foreach (var kv in t)
        {
            switch (kv.Key)
            {
                case "land": land = Enum.TryParse(kv.Value as string, true, out LandType l) && Enum.IsDefined(l) ? l : throw new RulesException(file, key + ".land", "unknown land type"); break;
                case "overlay": overlay = Enum.TryParse(kv.Value as string, true, out OverlayKind o) && Enum.IsDefined(o) ? o : throw new RulesException(file, key + ".overlay", "unknown overlay"); break;
                case "height": height = Byte(kv.Value, file, key + ".height"); break;
                case "density": density = Byte(kv.Value, file, key + ".density"); break;
                case "decor": decor = Byte(kv.Value, file, key + ".decor"); break;
                default: throw new RulesException(file, key + "." + kv.Key, "unknown key");
            }
        }
        return new StampCell(true, land, height, overlay, density, decor);
    }

    private static byte Byte(object? v, string file, string key) =>
        v is long l && l >= 0 && l <= 255 ? (byte)l : throw new RulesException(file, key, "must be an integer 0..255");

    private static string Str(TomlTable t, string k, string file) =>
        t.TryGetValue(k, out object? v) && v is string s && s.Length > 0 ? s : throw new RulesException(file, k, "missing or not a string");

    private static int Int(TomlTable t, string k, string file) =>
        t.TryGetValue(k, out object? v) && v is long l && l >= int.MinValue && l <= int.MaxValue ? (int)l : throw new RulesException(file, k, "missing or not an integer");

    /// <summary>Where source cell (<paramref name="sx"/>, <paramref name="sy"/>) lands in the placed footprint: mirrored left-right first,
    /// then turned <paramref name="turns"/> quarter turns clockwise (a turn maps (x, y) of a w x h grid to (h-1-y, x) of an h x w grid).</summary>
    private static (int X, int Y) Forward(Stamp s, int turns, bool mirror, int sx, int sy)
    {
        int w = s.Width, h = s.Height, x = mirror ? w - 1 - sx : sx, y = sy;
        for (int k = 0; k < turns; k++) { (x, y) = (h - 1 - y, x); (w, h) = (h, w); }
        return (x, y);
    }

    /// <summary>Footprint size (width, height) of a stamp after <paramref name="rotation"/> quarter turns.</summary>
    public static (int W, int H) FootprintSize(Stamp s, int rotation) => ((rotation % 4 + 4) % 4) % 2 == 0 ? (s.Width, s.Height) : (s.Height, s.Width);

    /// <summary>Applies <paramref name="s"/> with its top-left corner at (<paramref name="x"/>, <paramref name="y"/>) after mirroring left-right
    /// (<paramref name="mirror"/>) and turning <paramref name="rotation"/> quarter turns clockwise (0..3). Results are sorted row-major.
    /// Off-map cells, cells near a start and resource cells are refused and left untouched; everything else the stamp sets is written.</summary>
    public static StampResult Apply(Stamp s, StampCanvas canvas, int x, int y, int rotation, bool mirror)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(canvas);
        if (rotation < 0 || rotation > 3) throw new ArgumentOutOfRangeException(nameof(rotation), "rotation is 0..3 quarter turns");
        var changed = new List<int>();
        var refused = new List<CellCoord>();
        for (int sy = 0; sy < s.Height; sy++)
            for (int sx = 0; sx < s.Width; sx++)
            {
                StampCell c = s.Cells[sy * s.Width + sx];
                if (!c.Set) continue;
                (int dx, int dy) = Forward(s, rotation, mirror, sx, sy);
                int cx = x + dx, cy = y + dy;
                if (cx < 0 || cy < 0 || cx >= canvas.Width || cy >= canvas.Height || canvas.NearStart(cx, cy)) { refused.Add(new CellCoord(cx, cy)); continue; }
                int i = cy * canvas.Width + cx;
                if (OverlayKinds.IsResource(canvas.Overlay[i])) { refused.Add(new CellCoord(cx, cy)); continue; }
                bool diff = false;
                if (c.Land is { } land && canvas.Land[i] != land) { canvas.Land[i] = land; diff = true; }
                if (c.Height is { } hl && canvas.HeightLevel[i] != hl) { canvas.HeightLevel[i] = hl; diff = true; }
                if (c.Overlay is { } ov && canvas.Overlay[i] != ov) { canvas.Overlay[i] = ov; diff = true; }
                if (c.Density is { } dn && canvas.Density[i] != dn) { canvas.Density[i] = dn; diff = true; }
                if (c.Decor is { } dc && canvas.Decor is not null && canvas.Decor[i] != dc) { canvas.Decor[i] = dc; diff = true; }
                if (diff) changed.Add(i);
            }
        changed.Sort();
        refused.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        return new StampResult(changed, refused);
    }
}
