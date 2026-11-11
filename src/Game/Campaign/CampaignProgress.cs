// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Campaign;

/// <summary>
/// Campaign progress of one profile and faction (plan section 4): <c>{version, faction, completed[], difficulty{}, techSet[]}</c>.
/// Game-layer data, NOT sim state: a mission save stores its own mission id and the tech set at start, so losing or editing this file never
/// corrupts a sim save. Human-readable TOML, one file per faction (<see cref="DefaultPath"/>). Immutable; every change returns a new record.
/// </summary>
/// <param name="Version">File format version (<see cref="CurrentVersion"/>).</param>
/// <param name="Faction">Faction id ("authority", "directorate", "left_behind", "expedition").</param>
/// <param name="Completed">Completed mission ids in the order they were first completed.</param>
/// <param name="Difficulty">Skirmish tier (1..5) each completed mission was last won on, by mission id.</param>
/// <param name="TechSet">The campaign tech set: type ids granted by finished missions (ordinal order).</param>
public sealed record CampaignProgress(
    int Version, string Faction, IReadOnlyList<string> Completed, IReadOnlyDictionary<string, int> Difficulty, IReadOnlyList<string> TechSet)
{
    /// <summary>The file format version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>An empty record for a faction.</summary>
    public static CampaignProgress New(string faction) =>
        new(CurrentVersion, faction, Array.Empty<string>(), new SortedDictionary<string, int>(StringComparer.Ordinal), Array.Empty<string>());

    /// <summary>The default path: <c>campaign_&lt;faction&gt;.toml</c> in the folder that holds settings.toml.</summary>
    public static string DefaultPath(string faction) =>
        Path.Combine(Path.GetDirectoryName(Settings.DefaultPath) ?? ".", "campaign_" + faction + ".toml");

    /// <summary>True if the mission is completed.</summary>
    public bool IsCompleted(string missionId) => Completed.Contains(missionId);

    /// <summary>The record with a mission won on a tier (1..5): adds it to <see cref="Completed"/> once, keeps the highest tier, and merges the tech it unlocks.</summary>
    public CampaignProgress WithWin(string missionId, int tier, IEnumerable<string> unlocks)
    {
        if (string.IsNullOrWhiteSpace(missionId)) throw new ArgumentException("mission id is empty", nameof(missionId));
        if (tier is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(tier), "tier must be 1..5");
        var done = Completed.ToList();
        if (!done.Contains(missionId)) done.Add(missionId);
        var diff = new SortedDictionary<string, int>(Difficulty.ToDictionary(k => k.Key, k => k.Value), StringComparer.Ordinal);
        diff[missionId] = diff.TryGetValue(missionId, out int old) ? Math.Max(old, tier) : tier;
        var tech = new SortedSet<string>(TechSet, StringComparer.Ordinal);
        foreach (string id in unlocks) if (!string.IsNullOrWhiteSpace(id)) tech.Add(id);
        return this with { Completed = done, Difficulty = diff, TechSet = tech.ToList() };
    }

    /// <summary>The record as TOML text (stable: completed in order, difficulty and techSet sorted).</summary>
    public string ToToml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# RTS_2027 campaign progress. Delete this file to restart this faction's campaign.");
        sb.AppendLine("version = " + Version.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("faction = " + Quote(Faction));
        sb.AppendLine("completed = " + List(Completed));
        sb.AppendLine("techSet = " + List(TechSet.OrderBy(x => x, StringComparer.Ordinal)));
        sb.AppendLine();
        sb.AppendLine("[difficulty]");
        foreach (var kv in Difficulty.OrderBy(k => k.Key, StringComparer.Ordinal))
            sb.AppendLine(Quote(kv.Key) + " = " + kv.Value.ToString(CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    /// <summary>Parses file text. Throws <see cref="InvalidDataException"/> for bad TOML, a newer version, or wrong-typed or out-of-range values.</summary>
    public static CampaignProgress Parse(string text)
    {
        TomlTable root;
        try { root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable(); }
        catch (Exception e) { throw new InvalidDataException("campaign progress: " + e.Message, e); }
        if (!root.TryGetValue("version", out object? v) || v is not long version) throw new InvalidDataException("campaign progress: missing version");
        if (version < 1 || version > CurrentVersion) throw new InvalidDataException("campaign progress: unsupported version " + version);
        if (!root.TryGetValue("faction", out object? f) || f is not string { Length: > 0 } faction) throw new InvalidDataException("campaign progress: missing faction");

        var completed = new List<string>();
        foreach (string id in Strings(root, "completed")) if (!completed.Contains(id)) completed.Add(id);
        var tech = new SortedSet<string>(Strings(root, "techSet"), StringComparer.Ordinal);
        var diff = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (root.TryGetValue("difficulty", out object? d))
        {
            if (d is not TomlTable dt) throw new InvalidDataException("campaign progress: difficulty must be a table");
            foreach (var kv in dt)
            {
                if (kv.Value is not long tier || tier < 1 || tier > 5) throw new InvalidDataException($"campaign progress: difficulty '{kv.Key}' must be 1..5");
                diff[kv.Key] = (int)tier;
            }
        }

        return new CampaignProgress((int)version, faction, completed, diff, tech.ToList());
    }

    /// <summary>
    /// Loads the file; never throws. A missing or unreadable file, or one for another faction, gives an empty record. S18 GS-05: a file that
    /// exists but cannot be parsed (bad TOML, a newer version, bad values, another faction) is first renamed to <c>&lt;file&gt;.bad</c>, so the
    /// next save does not destroy it.
    /// </summary>
    public static CampaignProgress Load(string faction, string? path = null)
    {
        string file = path ?? DefaultPath(faction);
        try
        {
            if (!File.Exists(file)) return New(faction);
            CampaignProgress p = Parse(File.ReadAllText(file));
            if (p.Faction == faction) return p;
            KeepAsBad(file);
            return New(faction);
        }
        catch (InvalidDataException)
        {
            KeepAsBad(file);
            return New(faction);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return New(faction);
        }
    }

    /// <summary>S18 GS-05: renames an unreadable profile file to <c>&lt;file&gt;.bad</c> (replacing an older one); never throws.</summary>
    internal static void KeepAsBad(string file)
    {
        try { File.Move(file, file + ".bad", overwrite: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Writes the file (folders are created) through a temporary file. Returns false instead of throwing.</summary>
    public bool Save(string? path = null)
    {
        try
        {
            string file = path ?? DefaultPath(Faction);
            string? dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = file + ".tmp";
            File.WriteAllText(tmp, ToToml());
            File.Move(tmp, file, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IEnumerable<string> Strings(TomlTable root, string key)
    {
        if (!root.TryGetValue(key, out object? raw)) yield break;
        if (raw is not TomlArray a) throw new InvalidDataException($"campaign progress: {key} must be a list");
        foreach (object? item in a)
        {
            if (item is not string { Length: > 0 } s) throw new InvalidDataException($"campaign progress: {key} must hold ids");
            yield return s;
        }
    }

    private static string List(IEnumerable<string> ids) => "[" + string.Join(", ", ids.Select(Quote)) + "]";

    internal static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
