// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens;

namespace Rts.Game.Lan.Lobby;

/// <summary>
/// The map as it travels between machines (M9-05, M9-06, Q-M9-k). A generated map carries its parameters. A map file is a bare file name:
/// <paramref name="FileHash"/> 0 means a curated map every install has under data/maps (covered by the content hash); a non-zero hash means
/// the player's own map, which a peer may have to download first, and is checked by that hash.
/// </summary>
/// <param name="File">File name without folders (null for a generated map).</param>
/// <param name="FileHash">0 for a curated map, else <see cref="ContentHash.OfBytes"/> of the map file.</param>
/// <param name="Generated">Generator parameters (null for a map file).</param>
public sealed record LobbyMap(string? File, ulong FileHash, MapGenParams? Generated)
{
    /// <summary>True for a player's own map file that may need a transfer.</summary>
    public bool IsCustom => File is not null && FileHash != 0;

    /// <summary>The curated default map.</summary>
    public static LobbyMap Curated { get; } = new(SkirmishSettings.CuratedFile, 0, null);

    /// <summary>A generated map.</summary>
    public static LobbyMap FromGenerated(MapGenParams g) => new(null, 0, g);

    /// <summary>
    /// The map choice to play, with a file resolved to a path on this machine; null while a custom map is not here yet.
    /// A curated map keeps its bare name (the game finds it under data/maps).
    /// </summary>
    public MapChoice? Resolve(string userMapsDir, string downloadedDir)
    {
        if (Generated is not null) return new MapChoice(null, Generated);
        if (File is null) return null;
        if (!IsCustom) return new MapChoice(File, null);
        string? path = FindCustom(userMapsDir, downloadedDir);
        return path is null ? null : new MapChoice(path, null);
    }

    /// <summary>The local path of a custom map with the right hash (the player's folder first, then the download folder), or null.</summary>
    public string? FindCustom(string userMapsDir, string downloadedDir)
    {
        if (!IsCustom) return null;
        foreach (string candidate in Candidates(userMapsDir, downloadedDir))
        {
            try
            {
                if (System.IO.File.Exists(candidate) && ContentHash.OfBytes(System.IO.File.ReadAllBytes(candidate)) == FileHash) return candidate;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return null;
    }

    /// <summary>Where a downloaded copy of this map is stored: the download folder, the hash first so two maps with one name do not clash.</summary>
    public string DownloadPath(string downloadedDir) => Path.Combine(downloadedDir, $"{FileHash:x16}-{File}");

    private IEnumerable<string> Candidates(string userMapsDir, string downloadedDir)
    {
        yield return Path.Combine(userMapsDir, File!);
        yield return DownloadPath(downloadedDir);
    }

    /// <summary>The wire form of a map choice made on the host: a curated or user map file path becomes name plus hash.</summary>
    public static LobbyMap FromEntry(MapEntry entry, MapGenParams? generated)
    {
        if (entry.IsGenerated) return FromGenerated(generated!);
        if (!entry.User) return new LobbyMap(entry.File, 0, null);
        string path = entry.File!;
        ulong hash = 1;
        try { hash = ContentHash.OfBytes(System.IO.File.ReadAllBytes(path)); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return new LobbyMap(Path.GetFileName(path), hash == 0 ? 1 : hash, null);
    }
}
