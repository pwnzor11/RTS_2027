// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Game.Archive;
using Rts.Scripting;

namespace Rts.Game.Screens;

/// <summary>
/// S19 MIS-12: the name shown before a line of mission dialogue. A speaker id such as <c>leader_auth_wrenfield</c> is the id of a Leader entry in the
/// Archive (data/archive/leaders.toml), whose title is the display name ("Brigadier Hester Wrenfield"). <c>instructor</c> is the tutorial's voice.
/// An id with no entry is shown title-cased, never as the raw id.
/// </summary>
public static class SpeakerNames
{
    private static readonly Dictionary<string, ArchiveCatalog> Cache = new(StringComparer.Ordinal);

    /// <summary>The name to show for <paramref name="speakerId"/>, using <paramref name="catalog"/> (null = no Archive: title-case only).</summary>
    public static string NameOf(string speakerId, ArchiveCatalog? catalog)
    {
        if (string.Equals(speakerId, "instructor", StringComparison.OrdinalIgnoreCase)) return "Instructor";
        if (catalog?.Find(speakerId) is { Category: ArchiveCategory.Leader } entry && entry.Title.Length > 0) return entry.Title;
        return TitleCase(speakerId);
    }

    /// <summary>Title-cases an id: underscores and dashes become spaces and every word starts with a capital ("some_id" gives "Some Id").</summary>
    public static string TitleCase(string id)
    {
        string[] words = id.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w => char.ToUpper(w[0], CultureInfo.InvariantCulture) + w.Substring(1).ToLowerInvariant()));
    }

    /// <summary>The Archive that sits beside the mission's folder (<c>data/missions/&lt;id&gt;</c> gives <c>data/archive</c>); empty when it cannot be read. Loaded once per folder.</summary>
    public static ArchiveCatalog CatalogFor(MissionDef def)
    {
        if (def.Folder.Length == 0) return ArchiveCatalog.Empty;
        string dir = Path.GetFullPath(Path.Combine(def.Folder, "..", "..", "archive"));
        lock (Cache)
        {
            if (Cache.TryGetValue(dir, out ArchiveCatalog? known)) return known;
            ArchiveCatalog loaded;
            try { loaded = ArchiveCatalog.Load(dir); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException) { loaded = ArchiveCatalog.Empty; }
            Cache[dir] = loaded;
            return loaded;
        }
    }
}
