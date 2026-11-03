// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
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

    /// <summary>Loads and parses a palette file.</summary>
    public static TerrainPalette Load(string path) => FromToml(File.ReadAllText(path));

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
