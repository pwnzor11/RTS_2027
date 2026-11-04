// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;

namespace Rts.Game.Screens.Editor;

/// <summary>A map file the editor can open.</summary>
/// <param name="Path">Full path.</param>
/// <param name="Name">File name without <c>.map.toml</c>.</param>
/// <param name="IsUser">True for the user maps folder, false for data/maps.</param>
public sealed record MapFileEntry(string Path, string Name, bool IsUser);

/// <summary>Where maps live and how they are named: the user folder <c>%APPDATA%/RTS_2027/maps</c> and the shipped <c>data/maps</c>.</summary>
public static class MapFolders
{
    /// <summary>File name suffix.</summary>
    public const string Suffix = ".map.toml";

    /// <summary>The user maps folder.</summary>
    public static string UserDir => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Settings.DefaultPath) ?? ".", "maps");

    /// <summary>The shipped maps folder beside the rules directory (<c>data/maps</c>).</summary>
    public static string DataDir(string rulesDir) => System.IO.Path.GetFullPath(System.IO.Path.Combine(rulesDir, "..", "maps"));

    /// <summary>Every <c>*.map.toml</c> in the user folder then the data folder, each sorted by name; missing folders give nothing.</summary>
    public static IReadOnlyList<MapFileEntry> List(string userDir, string dataDir)
    {
        var list = new List<MapFileEntry>();
        foreach ((string dir, bool user) in new[] { (userDir, true), (dataDir, false) })
        {
            if (!Directory.Exists(dir)) continue;
            string[] files;
            try { files = Directory.GetFiles(dir, "*" + Suffix); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string f in files)
            {
                string file = System.IO.Path.GetFileName(f);
                list.Add(new MapFileEntry(f, file[..^Suffix.Length], user));
            }
        }
        return list;
    }

    /// <summary>A safe file stem for a map name: lower-case letters, digits, '_' and '-'; spaces become '_'; "map" if nothing is left.</summary>
    public static string FileStem(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name.Trim())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-') sb.Append(c);
            else if (c is >= 'A' and <= 'Z') sb.Append(char.ToLowerInvariant(c));
            else if (c == ' ') sb.Append('_');
        }
        return sb.Length == 0 ? "map" : sb.ToString();
    }

    /// <summary>The path a map named <paramref name="name"/> is saved to in <paramref name="dir"/>.</summary>
    public static string PathFor(string dir, string name) => System.IO.Path.Combine(dir, FileStem(name) + Suffix);
}
