// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Screens;

/// <summary>Reads <c>data/ui/tips.toml</c> and picks a tip for the loading screen (M10-20).</summary>
public static class LoadingTips
{
    /// <summary>Where the tips file is, under the data folder (the folder above data/rules).</summary>
    public static string PathFor(string rulesDir) => Path.Combine(rulesDir, "..", "ui", "tips.toml");

    /// <summary>The tips in the file, in file order. A missing or unreadable file gives an empty list; a row without text is skipped.</summary>
    public static IReadOnlyList<LoadingTip> Load(string rulesDir)
    {
        try
        {
            string path = PathFor(rulesDir);
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : Array.Empty<LoadingTip>();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<LoadingTip>();
        }
    }

    /// <summary>Parses tips text; invalid TOML gives an empty list.</summary>
    public static IReadOnlyList<LoadingTip> Parse(string toml)
    {
        TomlTable? root;
        try { root = TomlSerializer.Deserialize<TomlTable>(toml); }
        catch (TomlException) { return Array.Empty<LoadingTip>(); }
        if (root is null) return Array.Empty<LoadingTip>();
        if (!root.TryGetValue("tip", out object? rows) || rows is not TomlTableArray array) return Array.Empty<LoadingTip>();
        var tips = new List<LoadingTip>();
        foreach (TomlTable row in array)
        {
            if (row.TryGetValue("text", out object? t) && t is string text && text.Trim().Length > 0)
                tips.Add(new LoadingTip(text.Trim(), row.TryGetValue("faction", out object? f) && f is string faction && faction.Length > 0 ? faction : null));
        }

        return tips;
    }

    /// <summary>
    /// A tip for a game with the given factions: any tip with no faction, or whose faction is in <paramref name="factions"/>. <paramref name="seed"/>
    /// picks which (the same seed gives the same tip). Null when there are no tips.
    /// </summary>
    public static LoadingTip? Pick(IReadOnlyList<LoadingTip> tips, IReadOnlyCollection<string> factions, int seed)
    {
        var fit = tips.Where(t => t.Faction is null || factions.Contains(t.Faction, StringComparer.Ordinal)).ToList();
        if (fit.Count == 0) return null;
        return fit[(int)((uint)seed % (uint)fit.Count)];
    }
}
