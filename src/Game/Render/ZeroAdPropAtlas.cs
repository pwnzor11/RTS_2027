// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.IO.Compression;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Render;

/// <summary>
/// Prop frames rendered from 0 A.D. meshes (Wildfire Games / 0 A.D. contributors, CC-BY-SA 3.0) by <c>tools/art/zeroad/render_props.py</c>, read from
/// <c>data/sprites/props/zeroad</c> (a manifest <c>props.toml</c> and one graded PNG per theater). The art is separately licensed data (docs/CREDITS.md);
/// no code or art of it is compiled in. Covers only the kinds the manifest lists, so it sits above the procedural atlas in <see cref="PropAtlases"/>.
/// </summary>
public sealed class ZeroAdPropAtlas : IPropSource
{
    private readonly int[] _variants;
    private readonly int _frame, _originX, _originY;
    private readonly int[] _row;

    private ZeroAdPropAtlas(byte[] rgba, int width, int height, int frame, int originX, int originY, int[] row, int[] variants)
    {
        Rgba = rgba;
        Width = width;
        Height = height;
        _frame = frame;
        _originX = originX;
        _originY = originY;
        _row = row;
        _variants = variants;
        
    }

    /// <inheritdoc />
    public int Width { get; }

    /// <inheritdoc />
    public int Height { get; }

    /// <inheritdoc />
    public byte[] Rgba { get; }

    /// <summary>Folder holding <c>props.toml</c>, found by walking up from the executable (like the theater files); null when absent.</summary>
    public static string? FindDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "data", "sprites", "props", "zeroad");
            if (File.Exists(Path.Combine(candidate, "props.toml"))) return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    /// <summary>Loads the atlas of a theater, or null when the data folder, the theater's PNG or the manifest is missing or unreadable (the caller then uses procedural art).</summary>
    public static ZeroAdPropAtlas? TryLoad(string theater, string? directory = null)
    {
        try
        {
            string? dir = directory ?? FindDirectory();
            return dir is null ? null : Load(theater, dir);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or TomlException)
        {
            return null;
        }
    }

    /// <summary>Loads the atlas of a theater from <paramref name="directory"/>. Throws when a file is missing or malformed.</summary>
    public static ZeroAdPropAtlas Load(string theater, string directory)
    {
        ArgumentNullException.ThrowIfNull(theater);
        string id = TerrainPalette.NormalizeTheaterId(theater);
        TomlTable root = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(Path.Combine(directory, "props.toml"))) ?? throw new InvalidDataException("empty props.toml");
        int frame = Int(root, "frame"), ox = Int(root, "origin_x"), oy = Int(root, "origin_y");
        if (!root.TryGetValue("theaters", out object? tv) || tv is not TomlTable theaters || !theaters.TryGetValue(id, out object? fv) || fv is not string file)
            throw new InvalidDataException($"props.toml has no atlas for theater '{id}'");
        var row = new int[Enum.GetValues<PropKind>().Length];
        var variants = new int[row.Length];
        Array.Fill(row, -1);
        if (root.TryGetValue("kind", out object? kv) && kv is System.Collections.IEnumerable list)
        {
            foreach (object? item in list)
            {
                if (item is not TomlTable t) throw new InvalidDataException("[[kind]] entries must be tables");
                if (!Enum.TryParse(t["name"] as string, out PropKind kind)) throw new InvalidDataException($"unknown prop kind '{t["name"]}'");
                row[(int)kind] = Int(t, "row");
                variants[(int)kind] = Int(t, "variants");
            }
        }

        (byte[] rgba, int w, int h) = ReadPng(Path.Combine(directory, file));
        for (int k = 0; k < row.Length; k++)
            if (row[k] >= 0 && ((row[k] + 1) * frame > h || variants[k] * frame > w || variants[k] < 1))
                throw new InvalidDataException($"props.toml row {row[k]} does not fit {file}");
        return new ZeroAdPropAtlas(rgba, w, h, frame, ox, oy, row, variants);
    }

    /// <inheritdoc />
    public bool Covers(PropKind kind) => _row[(int)kind] >= 0;

    /// <inheritdoc />
    public int VariantCount(PropKind kind) => Covers(kind) ? _variants[(int)kind] : 1;

    /// <inheritdoc />
    public PropRect Frame(PropKind kind, int variant)
    {
        int r = _row[(int)kind];
        if (r < 0) return new PropRect(0, 0, _frame, _frame, _originX, _originY);
        int n = _variants[(int)kind];
        return new PropRect(((variant % n + n) % n) * _frame, r * _frame, _frame, _frame, _originX, _originY);
    }

    private static int Int(TomlTable t, string key) =>
        t.TryGetValue(key, out object? v) && v is long l && l is >= 0 and <= 100000 ? (int)l : throw new InvalidDataException($"props.toml: '{key}' is missing or not a small integer");

    /// <summary>8-bit RGBA non-interlaced PNG to RGBA bytes (the frames have only alpha 0 or 255, so straight equals premultiplied).</summary>
    private static (byte[] Rgba, int Width, int Height) ReadPng(string path)
    {
        byte[] f = File.ReadAllBytes(path);
        if (f.Length < 33 || f[1] != (byte)'P') throw new InvalidDataException($"{path}: not a PNG");
        int pos = 8, w = 0, h = 0;
        using var idat = new MemoryStream();
        while (pos + 12 <= f.Length)
        {
            int len = (f[pos] << 24) | (f[pos + 1] << 16) | (f[pos + 2] << 8) | f[pos + 3];
            string type = Encoding.ASCII.GetString(f, pos + 4, 4);
            int data = pos + 8;
            if (type == "IHDR")
            {
                w = (f[data] << 24) | (f[data + 1] << 16) | (f[data + 2] << 8) | f[data + 3];
                h = (f[data + 4] << 24) | (f[data + 5] << 16) | (f[data + 6] << 8) | f[data + 7];
                if (f[data + 8] != 8 || f[data + 9] != 6 || f[data + 12] != 0) throw new InvalidDataException($"{path}: only 8-bit RGBA non-interlaced PNG is supported");
            }
            else if (type == "IDAT") idat.Write(f, data, len);
            else if (type == "IEND") break;
            pos = data + len + 4;
        }

        idat.Position = 0;
        using var z = new ZLibStream(idat, CompressionMode.Decompress);
        int stride = w * 4;
        var raw = new byte[(stride + 1) * h];
        z.ReadExactly(raw);
        var rgba = new byte[stride * h];
        for (int y = 0; y < h; y++)
        {
            int filter = raw[y * (stride + 1)], src = y * (stride + 1) + 1, dst = y * stride;
            for (int x = 0; x < stride; x++)
            {
                int a = x >= 4 ? rgba[dst + x - 4] : 0, b = y > 0 ? rgba[dst - stride + x] : 0, c = x >= 4 && y > 0 ? rgba[dst - stride + x - 4] : 0;
                int pred = filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => throw new InvalidDataException($"{path}: bad PNG filter {filter}"),
                };
                rgba[dst + x] = (byte)(raw[src + x] + pred);
            }
        }

        return (rgba, w, h);
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
