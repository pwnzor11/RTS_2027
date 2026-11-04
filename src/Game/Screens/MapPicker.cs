// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;

namespace Rts.Game.Screens;

/// <summary>One choice in the skirmish map list: a map file (curated or user) or the generator.</summary>
/// <param name="Label">Text shown on the setup screen.</param>
/// <param name="File">The map file (a name under data/maps for curated maps, a full path for user maps); null for the generator entry.</param>
/// <param name="User">True for a map from the user's maps folder.</param>
/// <param name="MaxPlayers">Most players the map supports (0 for the generator entry: 2 to 4).</param>
public sealed record MapEntry(string Label, string? File, bool User, int MaxPlayers)
{
    /// <summary>True for the "Generated" entry.</summary>
    public bool IsGenerated => File is null;
}

/// <summary>
/// Finds the maps a skirmish can use (design 08 §2, M4-11): the curated maps in <c>data/maps</c>, the player's own maps in
/// <c>%APPDATA%/RTS_2027/maps</c> (Q-M4-h assumed yes) and the generator. Also resolves a <see cref="MapChoice"/> file to a path.
/// </summary>
public static class MapPicker
{
    /// <summary>Extension of map files.</summary>
    public const string MapSuffix = ".map.toml";

    /// <summary>The folder for the player's own maps: <c>%APPDATA%/RTS_2027/maps</c>.</summary>
    // ASSUMED (M4-11, Q-M4-h): user maps are offered in the skirmish list.
    public static string UserMapsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RTS_2027", "maps");

    /// <summary>The path of a <see cref="MapChoice.File"/>: a rooted path is used as it is, a bare name is a curated map under data/maps.</summary>
    public static string ResolveFile(string rulesDir, string file) =>
        Path.IsPathRooted(file) ? file : Path.Combine(rulesDir, "..", "maps", file);

    /// <summary>The list name of a map file: its name without the <c>.map.toml</c> suffix.</summary>
    public static string LabelOf(string file)
    {
        string name = Path.GetFileName(file);
        return name.EndsWith(MapSuffix, StringComparison.OrdinalIgnoreCase) ? name[..^MapSuffix.Length] : name;
    }

    /// <summary>The name of a biome on the setup screen.</summary>
    public static string BiomeName(Biome biome) => biome switch
    {
        Biome.RustBelt => "Rust Belt",
        Biome.GlassCoast => "Glass Coast",
        Biome.Mountains => "Mountains",
        Biome.AshPlains => "Ash Plains",
        _ => biome.ToString(),
    };

    /// <summary>The name of a generated map size on the setup screen.</summary>
    public static string SizeName(MapSize size) => size switch
    {
        MapSize.Small => "Small",
        MapSize.Medium => "Medium",
        MapSize.Large => "Large",
        _ => size.ToString(),
    };

    /// <summary>
    /// The map list: the curated map first, the other maps of data/maps in name order, the user's maps in name order, then the
    /// "Generated" entry. A file that cannot be read is left out. <paramref name="userDir"/> null = <see cref="UserMapsDir"/>.
    /// </summary>
    public static IReadOnlyList<MapEntry> List(string rulesDir, string? userDir = null)
    {
        var list = new List<MapEntry>();
        string dataDir = Path.GetFullPath(Path.Combine(rulesDir, "..", "maps"));
        AddFile(list, Path.Combine(dataDir, SkirmishSettings.CuratedFile), SkirmishSettings.CuratedFile, user: false, "(curated)");
        foreach (string path in Files(dataDir))
        {
            string name = Path.GetFileName(path);
            if (name != SkirmishSettings.CuratedFile) AddFile(list, path, name, user: false, "");
        }
        foreach (string path in Files(userDir ?? UserMapsDir)) AddFile(list, path, Path.GetFullPath(path), user: true, "(yours)");
        list.Add(new MapEntry("Generated", null, false, 0));
        return list;
    }

    private static IEnumerable<string> Files(string dir) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*" + MapSuffix).Order(StringComparer.Ordinal)
            : Enumerable.Empty<string>();

    private static void AddFile(List<MapEntry> list, string path, string file, bool user, string tag)
    {
        if (!File.Exists(path)) return;
        try
        {
            (Rts.Sim.Map.MapData map, MapMeta meta) = MapFile.LoadWithMeta(path);
            int max = meta.MaxPlayers > 0 ? Math.Min(meta.MaxPlayers, map.StartPositions.Count) : map.StartPositions.Count;
            if (max < SkirmishSettings.MinPlayers) return;
            string label = $"{map.Name} {map.Width}x{map.Height}, {max} players" + (tag.Length > 0 ? " " + tag : "");
            list.Add(new MapEntry(label, file, user, max));
        }
        catch (MapFileException)
        {
            // A map that does not load is not offered.
        }
    }
}
