// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Data;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Render;

/// <summary>
/// The Rust Belt terrain palette, read from <c>data/theaters/rustbelt.toml</c>: three shades per land type, the
/// ashfall and pitchblende overlay colours and the texture settings. The game bakes it per map (see
/// <see cref="TerrainBake"/>); <c>tools/maps/make_rustbelt_m2.py</c> reads the same file for the proof images.
/// </summary>
public sealed class TerrainPalette
{
    /// <summary>The map header <c>theater</c> value that selects this palette. Other theaters keep the debug look.</summary>
    public const string RustBeltTheater = "rustbelt";

    /// <summary>The only palette file layout this build reads.</summary>
    public const int SupportedFormatVersion = 1;

    private readonly LandShades[] _land;

    private TerrainPalette(int texelCells, LandShades[] land, Rgb ashfallSpeck, int ashfallMaxPermille,
        Rgb pitchChunk, Rgb pitchGlint, int pitchMaxPermille, int glintOneIn)
    {
        TexelCells = texelCells;
        _land = land;
        AshfallSpeck = ashfallSpeck;
        AshfallMaxPermille = ashfallMaxPermille;
        PitchChunk = pitchChunk;
        PitchGlint = pitchGlint;
        PitchMaxPermille = pitchMaxPermille;
        GlintOneIn = glintOneIn;
    }

    /// <summary>Texels per map cell in the baked texture.</summary>
    public int TexelCells { get; }

    /// <summary>Colour of the darker ashfall speckle.</summary>
    public Rgb AshfallSpeck { get; }

    /// <summary>Speckle coverage at density 255, in permille. Coverage rises linearly with density.</summary>
    public int AshfallMaxPermille { get; }

    /// <summary>Colour of pitchblende rock chunks.</summary>
    public Rgb PitchChunk { get; }

    /// <summary>Colour of the bright glints inside pitchblende chunks.</summary>
    public Rgb PitchGlint { get; }

    /// <summary>Chunk coverage at density 255, in permille.</summary>
    public int PitchMaxPermille { get; }

    /// <summary>One texel in this many inside a chunk is a glint.</summary>
    public int GlintOneIn { get; }

    /// <summary>The dark, base and light shades for a land type.</summary>
    public LandShades ShadesFor(LandType land) => _land[(int)land];

    /// <summary>True when a map's theater key selects the Rust Belt look.</summary>
    public static bool IsRustBelt(string theater) => string.Equals(theater, RustBeltTheater, StringComparison.Ordinal);

    /// <summary>
    /// Loads a palette. The argument is a theater id (<c>rustbelt</c>, <c>glass_coast</c>, <c>rust_belt</c>, ...) looked up as
    /// <c>data/theaters/&lt;id&gt;.toml</c>, or a path to a palette file (anything with a directory separator or a <c>.toml</c>
    /// extension). An unknown theater id falls back to the Rust Belt palette and raises <see cref="Warned"/> once per id.
    /// </summary>
    public static TerrainPalette Load(string theaterIdOrPath)
    {
        ArgumentNullException.ThrowIfNull(theaterIdOrPath);
        if (theaterIdOrPath.Contains('/') || theaterIdOrPath.Contains('\\')
            || theaterIdOrPath.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
            return FromToml(File.ReadAllText(theaterIdOrPath));

        string id = NormalizeTheaterId(theaterIdOrPath);
        lock (Cache)
        {
            if (Cache.TryGetValue(id, out TerrainPalette? cached)) return cached;
            string? file = id.Length == 0 ? null : FindTheaterFile(id);
            TerrainPalette palette;
            if (file is null)
            {
                if (id != RustBeltTheater) Warn($"unknown theater '{theaterIdOrPath}': using the {RustBeltTheater} palette");
                palette = id == RustBeltTheater ? throw new FileNotFoundException("data/theaters/rustbelt.toml not found") : Load(RustBeltTheater);
            }
            else palette = FromToml(File.ReadAllText(file));
            Cache[id] = palette;
            return palette;
        }
    }

    /// <summary>Raised (once per unknown theater id) when <see cref="Load"/> falls back to the Rust Belt palette.</summary>
    public static event Action<string>? Warned;

    /// <summary>
    /// Folds a theater or biome key to a palette file name: lower case, letters and digits only, so <c>rust_belt</c> is the
    /// same theater as <c>rustbelt</c>, <c>glass_coast</c> as <c>glasscoast</c>.
    /// </summary>
    public static string NormalizeTheaterId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var sb = new System.Text.StringBuilder(id.Length);
        foreach (char c in id)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
            else if (c is >= 'A' and <= 'Z') sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    /// <summary>
    /// The theater id a map is drawn with: its biome (when <paramref name="meta"/> names one), otherwise its header theater.
    /// Maps without metadata are Rust Belt unless their header says otherwise.
    /// </summary>
    public static string TheaterIdFor(MapData map, MapMeta? meta)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (meta is not null && !string.IsNullOrEmpty(meta.Biome)) return NormalizeTheaterId(meta.Biome);
        return string.IsNullOrEmpty(map.Theater) ? RustBeltTheater : map.Theater;
    }

    /// <summary>The palette a map is drawn with (see <see cref="TheaterIdFor"/>).</summary>
    public static TerrainPalette ForMap(MapData map, MapMeta? meta = null) => Load(TheaterIdFor(map, meta));

    private static readonly Dictionary<string, TerrainPalette> Cache = new(StringComparer.Ordinal);

    private static void Warn(string message)
    {
        Console.Error.WriteLine("warning: " + message);
        Warned?.Invoke(message);
    }

    private static string? FindTheaterFile(string id)
    {
        // The shipped files are rustbelt.toml, glass_coast.toml, mountains.toml and ash_plains.toml; ids are folded without underscores.
        string[] names = id switch
        {
            "glasscoast" => new[] { "glasscoast", "glass_coast" },
            "ashplains" => new[] { "ashplains", "ash_plains" },
            _ => new[] { id },
        };
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            foreach (string name in names)
            {
                string candidate = Path.Combine(dir, "data", "theaters", name + ".toml");
                if (File.Exists(candidate)) return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    /// <summary>
    /// Parses palette TOML. Throws <see cref="InvalidDataException"/> when a land type has no colour, a colour is not
    /// <c>#RRGGBB</c>, or a value is missing or out of range.
    /// </summary>
    public static TerrainPalette FromToml(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        TomlTable root = TomlSerializer.Deserialize<TomlTable>(text) ?? throw new InvalidDataException("empty palette file");

        int version = Int(root, "format_version");
        if (version != SupportedFormatVersion) throw new InvalidDataException($"unsupported palette format_version {version}");
        int texel = Int(root, "texel_cells");
        if (texel < 1 || texel > 32) throw new InvalidDataException($"texel_cells {texel} out of range 1..32");

        TomlTable land = Table(root, "land");
        var shades = new LandShades[RuleCounts.LandTypes];
        foreach (LandType type in Enum.GetValues<LandType>())
        {
            if (type == LandType.Coast && !land.ContainsKey("coast"))   // M7-02: older palettes have no beach; draw it like clear ground
            {
                shades[(int)type] = shades[(int)LandType.Clear];
                continue;
            }
            TomlTable t = Table(land, type.ToString().ToLowerInvariant());
            shades[(int)type] = new LandShades(Colour(t, "dark"), Colour(t, "base"), Colour(t, "light"));
        }

        TomlTable overlay = Table(root, "overlay");
        TomlTable ash = Table(overlay, "ashfall");
        TomlTable pitch = Table(overlay, "pitchblende");
        int oneIn = Int(pitch, "glint_one_in");
        if (oneIn < 1) throw new InvalidDataException("glint_one_in must be at least 1");

        return new TerrainPalette(texel, shades,
            Colour(ash, "speck"), Permille(ash, "max_coverage_permille"),
            Colour(pitch, "chunk"), Colour(pitch, "glint"), Permille(pitch, "max_coverage_permille"), oneIn);
    }

    private static TomlTable Table(TomlTable parent, string key) =>
        parent.TryGetValue(key, out object? v) && v is TomlTable t
            ? t
            : throw new InvalidDataException($"palette section [{key}] is missing");

    private static int Int(TomlTable parent, string key)
    {
        if (parent.TryGetValue(key, out object? v) && v is long l && l >= int.MinValue && l <= int.MaxValue) return (int)l;
        throw new InvalidDataException($"palette key '{key}' is missing or not an integer");
    }

    private static int Permille(TomlTable parent, string key)
    {
        int p = Int(parent, key);
        if (p < 0 || p > 1000) throw new InvalidDataException($"palette key '{key}' must be 0..1000 permille");
        return p;
    }

    private static Rgb Colour(TomlTable parent, string key)
    {
        if (!parent.TryGetValue(key, out object? v) || v is not string s || s.Length != 7 || s[0] != '#')
            throw new InvalidDataException($"palette colour '{key}' must be a #RRGGBB string");
        uint rgb = uint.Parse(s.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Rgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    /// <summary>An 8-bit RGB colour.</summary>
    public readonly record struct Rgb(byte R, byte G, byte B);

    /// <summary>The three shades one land type is drawn with.</summary>
    /// <param name="Dark">The darkest shade.</param>
    /// <param name="Base">The shade most texels use, and the colour a neighbour blends toward.</param>
    /// <param name="Light">The lightest shade.</param>
    public readonly record struct LandShades(Rgb Dark, Rgb Base, Rgb Light);
}
