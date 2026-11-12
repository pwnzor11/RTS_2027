// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens;
using Rts.Sim.Map;

namespace Rts.Game.Ui;

/// <summary>
/// Previews per map choice (M10-18): a generated map is keyed by its parameters, a map file by its full path and last-write time (so an
/// edited map is drawn again). Keeps the last few pictures. A map that cannot be made or read gives null, never an exception.
/// Shared by the skirmish setup, the editor's open list and the LAN lobby.
/// </summary>
public static class MapPreviewCache
{
    private const int Capacity = 16;
    private static readonly object Gate = new();
    private static readonly List<(string Key, MapPreviewImage Image)> Entries = new();

    /// <summary>How many pictures are held (tests).</summary>
    public static int Count { get { lock (Gate) return Entries.Count; } }

    /// <summary>Drops every picture (tests).</summary>
    public static void Clear() { lock (Gate) Entries.Clear(); }

    /// <summary>The cache key of a choice (the same text for the same map), or null when the map file does not exist.</summary>
    public static string? KeyOf(string rulesDir, MapChoice choice, int side = MapPreview.DefaultSide)
    {
        if (choice.Generated is { } g)
            return $"gen:{(int)g.Biome}:{(int)g.Size}:{g.Players}:{g.Seed}:{(int)g.OceanEdges}:{g.Layout}:{side}";
        if (choice.File is not { } file) return null;
        string path = Path.GetFullPath(MapPicker.ResolveFile(rulesDir, file));
        return File.Exists(path) ? $"file:{path}:{File.GetLastWriteTimeUtc(path).Ticks}:{side}" : null;
    }

    /// <summary>The picture for a choice, from the cache or made now; null when the map cannot be loaded or generated.</summary>
    public static MapPreviewImage? For(string rulesDir, MapChoice choice, int side = MapPreview.DefaultSide)
    {
        string? key = KeyOf(rulesDir, choice, side);
        if (key is null) return null;
        lock (Gate)
        {
            int at = Entries.FindIndex(e => e.Key == key);
            if (at >= 0)
            {
                var hit = Entries[at];
                Entries.RemoveAt(at);
                Entries.Add(hit);
                return hit.Image;
            }
        }

        MapPreviewImage? image = Make(rulesDir, choice, side);
        if (image is null) return null;
        lock (Gate)
        {
            Entries.Add((key, image));
            if (Entries.Count > Capacity) Entries.RemoveAt(0);
        }

        return image;
    }

    private static MapPreviewImage? Make(string rulesDir, MapChoice choice, int side)
    {
        try
        {
            MapData map = choice.Generated is { } g
                ? MapGenerator.GenerateWithMeta(g).Map
                : MapFile.LoadWithMeta(MapPicker.ResolveFile(rulesDir, choice.File!)).Item1;
            return MapPreview.Render(map, side);
        }
        catch (Exception e) when (e is MapFileException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
