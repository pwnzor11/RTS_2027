// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting;

namespace Rts.Game.Ui;

/// <summary>
/// Player-facing names for ids that are stored raw (S19 naming pass, D-205): map names and mission ids. The raw values stay in saves,
/// replays and hashes; only what the player reads goes through here. Display only.
/// </summary>
public static class DisplayNames
{
    private static readonly Dictionary<string, string> Maps = new(StringComparer.Ordinal)
    {
        ["ash_plains_m_2p_41"] = "Ash Reckoning",
        ["glass_coast_m_2p_11"] = "Glass Coast",
        ["rustbelt_m2"] = "Rustbelt",
    };

    private static readonly Dictionary<string, string> MissionCache = new(StringComparer.Ordinal);

    /// <summary>The data folder (the one holding <c>missions</c>), set once at start-up; null leaves mission ids as they are.</summary>
    public static string? DataDir { get; set; }

    /// <summary>The name of a shipped map for its stored name; any other name (a map the player made) is returned unchanged.</summary>
    public static string Map(string rawName) => Maps.TryGetValue(rawName, out string? name) ? name : rawName;

    /// <summary>The title of a mission from its mission.toml, or the id when it cannot be read.</summary>
    public static string Mission(string missionId)
    {
        if (string.IsNullOrEmpty(missionId) || DataDir is null) return missionId;
        lock (MissionCache)
        {
            if (MissionCache.TryGetValue(missionId, out string? cached)) return cached;
            string title;
            try { title = MissionLoader.Load(MissionLoader.FolderOf(DataDir, missionId)).Title; }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException) { title = missionId; }
            if (string.IsNullOrWhiteSpace(title)) title = missionId;
            MissionCache[missionId] = title;
            return title;
        }
    }
}
