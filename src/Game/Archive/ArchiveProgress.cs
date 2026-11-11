// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Archive;

/// <summary>
/// What the player has unlocked in the Archive, stored per user as <c>archive_progress.toml</c> beside settings.toml.
/// Records encounters (rules type ids the player has seen), finished research (upgrade ids) and completed campaign
/// missions. Loading never throws: a missing, unreadable or corrupt file gives an empty record.
/// </summary>
public sealed class ArchiveProgress
{
    /// <summary>The file format version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    private readonly SortedSet<string> _encounters = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _research = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _missions = new(StringComparer.Ordinal);

    /// <summary>The default path: the folder that holds settings.toml, then archive_progress.toml.</summary>
    public static string DefaultPath =>
        Path.Combine(Path.GetDirectoryName(Settings.DefaultPath) ?? ".", "archive_progress.toml");

    /// <summary>True when something was added since the last load or save.</summary>
    public bool Dirty { get; private set; }

    /// <summary>Rules ids the player has seen, ordinal order.</summary>
    public IReadOnlyCollection<string> Encounters => _encounters;

    /// <summary>Upgrade ids the player has finished researching.</summary>
    public IReadOnlyCollection<string> Research => _research;

    /// <summary>Campaign mission ids the player has completed.</summary>
    public IReadOnlyCollection<string> Missions => _missions;

    /// <summary>Records that the player has seen a rules type. Returns true if it was new.</summary>
    public bool RecordEncounter(string rulesId) => Add(_encounters, rulesId);

    /// <summary>Records a finished upgrade research. Returns true if it was new.</summary>
    public bool RecordResearch(string upgradeId) => Add(_research, upgradeId);

    /// <summary>
    /// Records that a campaign mission was completed. Returns true if it was new.
    /// </summary>
    // TODO (M3-17): M4 wires this to the campaign (call it when a mission is won, then Save()).
    public bool MarkCampaignMission(string missionId) => Add(_missions, missionId);

    /// <summary>Whether an entry is readable under this progress.</summary>
    public bool IsUnlocked(ArchiveEntry entry) => IsUnlocked(entry.Unlock);

    /// <summary>Whether a rule is satisfied under this progress.</summary>
    public bool IsUnlocked(ArchiveUnlock rule) => rule.Kind switch
    {
        ArchiveUnlockKind.Open => true,
        ArchiveUnlockKind.Campaign => _missions.Contains(rule.Arg),
        ArchiveUnlockKind.Encounter => _encounters.Contains(rule.Arg),
        ArchiveUnlockKind.Research => _research.Contains(rule.Arg),
        _ => false,
    };

    /// <summary>Loads the file; never throws. A missing or corrupt file gives an empty record.</summary>
    public static ArchiveProgress Load(string? path = null)
    {
        try
        {
            string file = path ?? DefaultPath;
            if (!File.Exists(file)) return new ArchiveProgress();
            ArchiveProgress p = Parse(File.ReadAllText(file), out bool readable);
            if (!readable) Campaign.CampaignProgress.KeepAsBad(file);   // S18 GS-05: keep it as <file>.bad, do not overwrite it later
            return p;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new ArchiveProgress();
        }
    }

    /// <summary>Parses file text; never throws. Bad text, a newer version or wrong-typed lists give what could be read (or empty).</summary>
    public static ArchiveProgress Parse(string text) => Parse(text, out _);

    // S18 GS-05: readable is false for text that is not TOML or has a newer version (the caller keeps such a file as .bad).
    private static ArchiveProgress Parse(string text, out bool readable)
    {
        var progress = new ArchiveProgress();
        readable = false;
        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
        }
        catch (Exception)
        {
            return progress;
        }
        if (root.TryGetValue("version", out object? v) && v is long version && version > CurrentVersion) return progress;
        readable = true;
        Fill(progress._encounters, root, "encounters");
        Fill(progress._research, root, "research");
        Fill(progress._missions, root, "missions");
        progress.Dirty = false;
        return progress;
    }

    /// <summary>The record as TOML text.</summary>
    public string ToToml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# RTS_2027 Archive progress. Delete this file to lock everything again.");
        sb.AppendLine($"version = {CurrentVersion}");
        Write(sb, "encounters", _encounters);
        Write(sb, "research", _research);
        Write(sb, "missions", _missions);
        return sb.ToString();
    }

    /// <summary>Writes the file (folders are created) and clears <see cref="Dirty"/>. Returns false instead of throwing.</summary>
    public bool Save(string? path = null)
    {
        try
        {
            string file = path ?? DefaultPath;
            string? dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = file + ".tmp";   // S18 GS-05: write through a temporary file, as CampaignProgress does
            File.WriteAllText(tmp, ToToml());
            File.Move(tmp, file, overwrite: true);
            Dirty = false;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool Add(SortedSet<string> set, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !set.Add(id)) return false;
        Dirty = true;
        return true;
    }

    private static void Fill(SortedSet<string> set, TomlTable root, string key)
    {
        if (!root.TryGetValue(key, out object? raw) || raw is not TomlArray array) return;
        foreach (object? item in array)
        {
            if (item is string s && !string.IsNullOrWhiteSpace(s)) set.Add(s);
        }
    }

    private static void Write(StringBuilder sb, string key, SortedSet<string> set)
    {
        sb.Append(key).Append(" = [");
        sb.Append(string.Join(", ", set.Select(s => '"' + s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + '"')));
        sb.AppendLine("]");
    }
}
