// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Screens;

/// <summary>Chooses the picture behind the loading screen (M10-20) from the act stills in <c>data/images/stills</c>.</summary>
public static class LoadingArt
{
    /// <summary>The id a faction has in file names and tips (authority, directorate, left_behind, expedition), or null for Neutral.</summary>
    public static string? IdOf(Faction faction) => faction switch
    {
        Faction.Authority => "authority",
        Faction.Directorate => "directorate",
        Faction.LeftBehind => "left_behind",
        Faction.Expedition => "expedition",
        _ => null,
    };

    /// <summary>
    /// The still to show: <paramref name="stillId"/> when that file exists (a mission's own act still); else one of the faction's act stills picked by
    /// <paramref name="seed"/>; else null (the screen is plain). <paramref name="dataDir"/> is the folder above data/rules.
    /// </summary>
    public static string? Choose(string dataDir, string? faction, string? stillId, int seed)
    {
        string dir = Path.Combine(dataDir, "images", "stills");
        if (stillId is { Length: > 0 })
        {
            string own = Path.Combine(dir, stillId + ".png");
            if (File.Exists(own)) return own;
        }

        if (faction is null) return null;
        var found = new List<string>();
        for (int act = 2; act <= 4; act++)
        {
            string path = Path.Combine(dir, $"still_{faction}_act{act}.png");
            if (File.Exists(path)) found.Add(path);
        }

        return found.Count == 0 ? null : found[(int)((uint)seed % (uint)found.Count)];
    }
}
