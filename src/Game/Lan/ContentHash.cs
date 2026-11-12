// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Lan;

/// <summary>
/// A stable 64-bit hash over every data file the simulation or the mission scripts read (M9-13, design 20 §6): rules, AI tiers, map
/// generator tables, maps, missions (toml and lua), campaigns and terrain themes. Audio, images, sprites, fonts and archive text are not
/// included. Files are taken in ordinal path order and line endings are normalised, so the same data gives the same hash on every
/// install, and two installs with different gameplay data refuse each other at join time.
/// </summary>
public static class ContentHash
{
    /// <summary>Sub-folders of the data folder that count, and the file extensions read in them.</summary>
    private static readonly string[] Folders = { "rules", "ai", "mapgen", "maps", "missions", "campaigns", "theaters" };

    private static readonly string[] Extensions = { ".toml", ".lua" };

    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    /// <summary>The hash of the data folder <paramref name="dataDir"/> (the folder that holds rules/, maps/ ...). A missing folder counts as empty.</summary>
    public static ulong Compute(string dataDir)
    {
        ulong h = Offset;
        string root = Path.GetFullPath(dataDir);
        var files = new List<(string Rel, string Full)>();
        foreach (string folder in Folders)
        {
            string dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir)) continue;
            foreach (string full in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(full);
                if (!Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
                files.Add((Path.GetRelativePath(root, full).Replace('\\', '/'), full));
            }
        }

        files.Sort((a, b) => string.CompareOrdinal(a.Rel, b.Rel));
        foreach ((string rel, string full) in files)
        {
            h = Mix(h, System.Text.Encoding.UTF8.GetBytes(rel));
            h = Mix(h, new byte[] { 0 });
            byte[] bytes = File.ReadAllBytes(full);
            h = Mix(h, StripCarriageReturns(bytes));
            h = Mix(h, new byte[] { 0 });
        }

        return h;
    }

    /// <summary>The hash for a game whose rules folder is <paramref name="rulesDir"/> (data/rules): the data folder is its parent.</summary>
    public static ulong ForRulesDir(string rulesDir) => Compute(Path.Combine(rulesDir, ".."));

    /// <summary>The hash of one file's bytes with line endings normalised (identifies a custom map across machines, Q-M9-k).</summary>
    public static ulong OfBytes(byte[] bytes) => Mix(Offset, StripCarriageReturns(bytes));

    private static byte[] StripCarriageReturns(byte[] bytes)
    {
        if (Array.IndexOf(bytes, (byte)'\r') < 0) return bytes;
        var list = new List<byte>(bytes.Length);
        foreach (byte b in bytes) if (b != (byte)'\r') list.Add(b);
        return list.ToArray();
    }

    private static ulong Mix(ulong h, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data) h = (h ^ b) * Prime;
        return h;
    }
}
