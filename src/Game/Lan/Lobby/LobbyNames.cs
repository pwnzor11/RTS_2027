// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;

namespace Rts.Game.Lan.Lobby;

/// <summary>Player name rules for the LAN lobby (M9-05): the default is the Windows user name, names are short and plain, and no two in a lobby are alike.</summary>
public static class LobbyNames
{
    /// <summary>The name used when nothing usable is set.</summary>
    public const string Fallback = "Player";

    /// <summary>The name to use for <paramref name="setting"/> (the PlayerName setting): itself when it is usable, else the Windows user name, else "Player".</summary>
    public static string Resolve(string? setting, string? userName = null)
    {
        string name = Clean(setting);
        if (name.Length > 0) return name;
        name = Clean(userName ?? Environment.UserName);
        return name.Length > 0 ? name : Fallback;
    }

    /// <summary>Trims, drops control characters, collapses runs of spaces and cuts to <see cref="LobbyData.MaxNameBytes"/> UTF-8 bytes. May return an empty string.</summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder();
        int bytes = 0;
        bool space = false;
        foreach (Rune r in text.EnumerateRunes())
        {
            if (Rune.IsControl(r)) continue;
            if (Rune.IsWhiteSpace(r)) { space = sb.Length > 0; continue; }
            int n = r.Utf8SequenceLength;
            if (bytes + n + (space ? 1 : 0) > LobbyData.MaxNameBytes) break;
            if (space) { sb.Append(' '); bytes++; space = false; }
            sb.Append(r.ToString());
            bytes += n;
        }

        return sb.ToString();
    }

    /// <summary>A name that differs from every name in <paramref name="taken"/> (case ignored): "Sam", then "Sam 2", "Sam 3" ...</summary>
    public static string Unique(string name, IEnumerable<string> taken)
    {
        var set = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        if (!set.Contains(name)) return name;
        for (int i = 2; ; i++)
        {
            string suffix = " " + i;
            string stem = name;
            while (Encoding.UTF8.GetByteCount(stem) + suffix.Length > LobbyData.MaxNameBytes && stem.Length > 1) stem = stem[..^1];
            string candidate = stem + suffix;
            if (!set.Contains(candidate)) return candidate;
        }
    }
}
