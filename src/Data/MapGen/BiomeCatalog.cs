// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Data.MapGen;

/// <summary>
/// Loads <c>data/mapgen/biomes.toml</c> (design 08 §1). <see cref="Default"/> is the file found by walking up from the binaries' folder
/// (the Game and Tools copy <c>data/</c> beside the exe; tests find the source tree's copy). The file is the only source of the values.
/// </summary>
public static class BiomeCatalog
{
    /// <summary>Path of the biome file relative to the game root.</summary>
    public static readonly string RelativePath = Path.Combine("data", "mapgen", "biomes.toml");

    private static readonly string[] Keys =
    [
        "theater", "base_land", "symmetry_2p", "symmetry_4p", "water_permille", "shallow_permille", "rock_permille", "rough_permille",
        "rock_style", "cliff_plateaus", "rivers", "river_width", "river_wander_permille", "ford_spacing", "road_grid", "ocean_width",
        "ash_fields_per_start", "pitch_fields_per_start", "contested_fields", "extra_fields", "field_density_min", "field_density_max", "ruins", "decor",
    ];

    private static readonly string[] DecorKeys =
    [
        "prop_permille", "cluster_min", "cluster_max", "single_permille", "gap_permille", "cliff_permille", "map_edge_permille", "water_permille",
        "road_permille", "yard_min", "yard_max", "dead_target_permille", "kinds", "yard_kinds",
    ];

    private static readonly string[] PropNames = ["tree", "wreck", "block", "pylon", "snag"];

    private static readonly string[] RockStyles = ["rubble", "dunes", "spurs", "blobs"];

    private static readonly Lazy<IReadOnlyList<BiomeParams>> DefaultLazy = new(() =>
    {
        string path = FindFile(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException($"{RelativePath} not found above {AppContext.BaseDirectory}");
        return Load(path);
    });

    /// <summary>The biomes of the located <c>biomes.toml</c>, indexed by <see cref="Biome"/>. Throws when the file is missing or invalid.</summary>
    public static IReadOnlyList<BiomeParams> Default => DefaultLazy.Value;

    /// <summary>The located parameters of <paramref name="biome"/>.</summary>
    public static BiomeParams For(Biome biome) => Default[(int)biome];

    /// <summary>Layout 2 field placement of a biome (v6, Q-69; see <see cref="Layout2Fields"/>), tuned by the income probe of docs/balance/s16_layout2.md.</summary>
    public static Layout2Fields Layout2FieldsFor(string biomeKey) => biomeKey switch
    {
        "rust_belt" => new Layout2Fields(7, 2, 2, 100, 40),
        "mountains" => new Layout2Fields(7, 2, 2, 100, 40),
        "ash_plains" => new Layout2Fields(7, 2, 2, 100, 40),
        _ => new Layout2Fields(7, 2, 2, 100, 40),   // glass_coast
    };

    /// <summary>The first <c>data/mapgen/biomes.toml</c> found walking up from <paramref name="startDirectory"/>, or null.</summary>
    public static string? FindFile(string startDirectory)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, RelativePath);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>Reads and parses a biome file.</summary>
    /// <exception cref="RulesException">The file is missing or invalid (every problem is listed).</exception>
    public static IReadOnlyList<BiomeParams> Load(string path)
    {
        string text;
        try { text = File.ReadAllText(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new RulesException(path, "<file>", "cannot read: " + e.Message); }
        return Parse(text, path);
    }

    /// <summary>Parses biome TOML text; the result is indexed by <see cref="Biome"/>.</summary>
    /// <exception cref="RulesException">The text is invalid (every problem is listed).</exception>
    public static IReadOnlyList<BiomeParams> Parse(string text, string file)
    {
        TomlTable root;
        try { root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable(); }
        catch (Exception e) { throw new RulesException(file, "<toml>", "invalid TOML: " + e.Message); }

        var problems = new List<RulesProblem>();
        foreach (var kv in root)
            if (kv.Key != "biome") problems.Add(new RulesProblem(file, kv.Key, "unknown key"));
        if (!root.TryGetValue("biome", out object? bv) || bv is not TomlTable biomes)
            throw new RulesException(file, "biome", "missing [biome.<key>] tables");
        foreach (var kv in biomes)
            if (Array.IndexOf(MapFile.BiomeKeys, kv.Key) < 0) problems.Add(new RulesProblem(file, "biome." + kv.Key, "unknown biome"));

        var result = new BiomeParams?[MapFile.BiomeKeys.Length];
        for (int b = 0; b < MapFile.BiomeKeys.Length; b++)
        {
            string key = MapFile.BiomeKeys[b];
            if (!biomes.TryGetValue(key, out object? tv) || tv is not TomlTable t)
            {
                problems.Add(new RulesProblem(file, "biome." + key, "missing biome table"));
                continue;
            }
            result[b] = ParseOne(key, t, file, problems);
        }
        if (problems.Count > 0) throw new RulesException(problems);
        return result.Select(r => r!).ToArray();
    }

    private static DecorParams ParseDecor(TomlTable biome, string prefix, string file, List<RulesProblem> problems)
    {
        if (!biome.TryGetValue("decor", out object? dv) || dv is not TomlTable t)
        {
            problems.Add(new RulesProblem(file, prefix + "decor", "missing [biome.<key>.decor] table"));
            return DecorParams.None;
        }
        string pre = prefix + "decor.";
        foreach (var kv in t)
            if (Array.IndexOf(DecorKeys, kv.Key) < 0) problems.Add(new RulesProblem(file, pre + kv.Key, "unknown key"));
        int Int(string k, int min, int max)
        {
            if (t.TryGetValue(k, out object? v) && v is long l)
            {
                if (l >= min && l <= max) return (int)l;
                problems.Add(new RulesProblem(file, pre + k, $"must be {min}..{max}, got {l.ToString(CultureInfo.InvariantCulture)}"));
                return min;
            }
            problems.Add(new RulesProblem(file, pre + k, "missing or not an integer"));
            return min;
        }
        List<PropWeight> Kinds(string k)
        {
            var list = new List<PropWeight>();
            if (!t.TryGetValue(k, out object? v) || v is not TomlArray arr || arr.Count == 0)
            {
                problems.Add(new RulesProblem(file, pre + k, "missing or empty list of \"kind:weight\" strings"));
                return list;
            }
            foreach (object? item in arr)
            {
                string text = item as string ?? "";
                string[] parts = text.Split(':');
                int idx = Array.IndexOf(PropNames, parts[0]);
                if (idx < 0 || parts.Length > 2 || (parts.Length == 2 && !(int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int w0) && w0 >= 1)))
                {
                    problems.Add(new RulesProblem(file, pre + k, $"'{text}' must be <{string.Join("|", PropNames)}> or <kind>:<weight>"));
                    continue;
                }
                list.Add(new PropWeight((OverlayKind)((int)OverlayKind.Tree + idx), parts.Length == 2 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 1));
            }
            return list;
        }
        int cmin = Int("cluster_min", 1, 12), cmax = Int("cluster_max", 1, 24);
        if (cmin > cmax) problems.Add(new RulesProblem(file, pre + "cluster_min", "must not exceed cluster_max"));
        int ymin = Int("yard_min", 0, 16), ymax = Int("yard_max", 0, 24);
        if (ymin > ymax) problems.Add(new RulesProblem(file, pre + "yard_min", "must not exceed yard_max"));
        return new DecorParams(Int("prop_permille", 0, 80), cmin, cmax, Int("single_permille", 0, 1000), Int("gap_permille", 0, 500),
            Int("cliff_permille", 100, 4000), Int("map_edge_permille", 100, 4000), Int("water_permille", 100, 4000), Int("road_permille", 50, 4000),
            ymin, ymax, Int("dead_target_permille", 0, 1000), Kinds("kinds"), Kinds("yard_kinds"));
    }

    private static BiomeParams? ParseOne(string key, TomlTable t, string file, List<RulesProblem> problems)
    {
        string prefix = "biome." + key + ".";
        int before = problems.Count;
        foreach (var kv in t)
            if (Array.IndexOf(Keys, kv.Key) < 0) problems.Add(new RulesProblem(file, prefix + kv.Key, "unknown key"));

        string Str(string k)
        {
            if (t.TryGetValue(k, out object? v) && v is string s) return s;
            problems.Add(new RulesProblem(file, prefix + k, "missing or not a string"));
            return "";
        }
        int Int(string k, int min, int max)
        {
            if (t.TryGetValue(k, out object? v) && v is long l)
            {
                if (l >= min && l <= max) return (int)l;
                problems.Add(new RulesProblem(file, prefix + k, $"must be {min}..{max}, got {l.ToString(CultureInfo.InvariantCulture)}"));
                return min;
            }
            problems.Add(new RulesProblem(file, prefix + k, "missing or not an integer"));
            return min;
        }
        MapSymmetry Sym(string k, params MapSymmetry[] allowed)
        {
            string s = Str(k);
            int i = Array.IndexOf(MapFile.SymmetryKeys, s);
            if (i >= 0 && Array.IndexOf(allowed, (MapSymmetry)i) >= 0) return (MapSymmetry)i;
            if (s.Length > 0)
                problems.Add(new RulesProblem(file, prefix + k, "must be one of " + string.Join(", ", allowed.Select(a => MapFile.SymmetryKeys[(int)a]))));
            return allowed[0];
        }

        string theater = Str("theater");
        string baseKey = Str("base_land");
        LandType baseLand = baseKey switch { "clear" => LandType.Clear, "ash" => LandType.Ash, _ => LandType.Clear };
        if (baseKey.Length > 0 && baseKey is not ("clear" or "ash")) problems.Add(new RulesProblem(file, prefix + "base_land", "must be clear or ash"));
        MapSymmetry s2 = Sym("symmetry_2p", MapSymmetry.Rot2, MapSymmetry.Rot4, MapSymmetry.MirrorX, MapSymmetry.MirrorY);
        MapSymmetry s4 = Sym("symmetry_4p", MapSymmetry.Rot4);
        string style = Str("rock_style");
        if (style.Length > 0 && Array.IndexOf(RockStyles, style) < 0)
            problems.Add(new RulesProblem(file, prefix + "rock_style", "must be one of " + string.Join(", ", RockStyles)));
        int roadGrid = Int("road_grid", 0, 64);
        if (roadGrid is > 0 and < 8) problems.Add(new RulesProblem(file, prefix + "road_grid", "must be 0 or 8..64"));
        int dMin = Int("field_density_min", 1, 12), dMax = Int("field_density_max", 1, 12);
        if (dMin > dMax) problems.Add(new RulesProblem(file, prefix + "field_density_min", "must not exceed field_density_max"));
        int ash = Int("ash_fields_per_start", 0, 8), pitch = Int("pitch_fields_per_start", 0, 4);
        if (ash + pitch < 2) problems.Add(new RulesProblem(file, prefix + "ash_fields_per_start", "ash + pitch fields per start must be at least 2"));

        var p = new BiomeParams(key, theater, baseLand, s2, s4,
            Int("water_permille", 0, 300), Int("shallow_permille", 0, 300), Int("rock_permille", 0, 300), Int("rough_permille", 0, 400),
            style, Int("cliff_plateaus", 0, 6), Int("rivers", 0, 2), Int("river_width", 1, 4), Int("river_wander_permille", 0, 1000),
            Int("ford_spacing", 8, 64), roadGrid, Int("ocean_width", 0, 16), ash, pitch, Int("contested_fields", 0, 8),
            Int("extra_fields", 0, 16), dMin, dMax, Int("ruins", 0, 4), ParseDecor(t, prefix, file, problems));
        return problems.Count > before ? null : p;
    }
}
