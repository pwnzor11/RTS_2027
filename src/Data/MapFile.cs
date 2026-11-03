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
/// </summary>
public static class MapFile
{
    public const int FormatVersion = 1;

    public static void Save(MapData map, string path)
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
        sb.Append("]\n\n[layers]\n");

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

        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static MapData Load(string path)
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
        if (version != FormatVersion) throw new MapFileException(path, $"unsupported format_version {version}");
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
            if (ovlB[i] > (byte)OverlayKind.Wall) throw new MapFileException(path, $"layers.overlay: invalid overlay {ovlB[i]} at cell {i}");
            land[i] = (LandType)landB[i]; ovl[i] = (OverlayKind)ovlB[i];
        }
        return new MapData(name, theater, w, h, tpl, land, hgtB, ovl, denB, starts);
    }

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
