// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>The music files that exist: the ids (file names without extension) of the *.ogg files in a folder. A missing or empty folder is fine: no music.</summary>
public sealed class MusicLibrary
{
    private readonly Dictionary<string, string> _paths;

    private MusicLibrary(Dictionary<string, string> paths) => _paths = paths;

    /// <summary>A library with no tracks.</summary>
    public static MusicLibrary Empty { get; } = new(new Dictionary<string, string>());

    /// <summary>Track ids in name order.</summary>
    public IReadOnlyList<string> Ids => _paths.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

    /// <summary>A library holding exactly these track ids (no files). For tests and tools.</summary>
    public static MusicLibrary FromIds(IEnumerable<string> ids)
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in ids) paths[id] = id + ".ogg";
        return new MusicLibrary(paths);
    }

    /// <summary>Scans <paramref name="dir"/> for *.ogg files; never throws.</summary>
    public static MusicLibrary Scan(string dir)
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (Directory.Exists(dir))
            {
                foreach (string file in Directory.EnumerateFiles(dir, "*.ogg"))
                {
                    paths[Path.GetFileNameWithoutExtension(file)] = Path.GetFullPath(file);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Unreadable folder: no music.
        }

        return new MusicLibrary(paths);
    }

    /// <summary>The full path of a track, or null.</summary>
    public string? PathOf(string id) => _paths.GetValueOrDefault(id);

    /// <summary>The ids that start with <paramref name="prefix"/>, in name order.</summary>
    public IReadOnlyList<string> WithPrefix(string prefix) =>
        Ids.Where(id => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
}
