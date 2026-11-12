// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using System.Text;
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Profile;

/// <summary>
/// M10-19: the local player's medals and records, stored as <c>%APPDATA%/RTS_2027/profile.toml</c> beside the settings. Loading never throws:
/// a missing file gives an empty profile; an unreadable one gives an empty profile and is kept as <c>.bad</c> (like the settings); odd values
/// are skipped or clamped. Immutable: the <c>With</c> methods return a changed copy.
/// </summary>
public sealed class PlayerProfile
{
    /// <summary>The file format version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Largest count or time stored (keeps a hand-edited file sane).</summary>
    public const int MaxCount = 1_000_000;

    private readonly SortedDictionary<string, MissionRecord> _missions;
    private readonly SortedDictionary<Faction, SkirmishTally> _factions;
    private readonly SortedDictionary<int, SkirmishTally> _tiers;

    private PlayerProfile(SortedDictionary<string, MissionRecord> missions, SortedDictionary<Faction, SkirmishTally> factions,
        SortedDictionary<int, SkirmishTally> tiers, int fastestWinSeconds, int biggestArmy)
    {
        _missions = missions;
        _factions = factions;
        _tiers = tiers;
        FastestWinSeconds = fastestWinSeconds;
        BiggestArmy = biggestArmy;
    }

    /// <summary>An empty profile.</summary>
    public static PlayerProfile Empty => new(new(StringComparer.Ordinal), new(), new(), 0, 0);

    /// <summary>Campaign mission records by mission id.</summary>
    public IReadOnlyDictionary<string, MissionRecord> Missions => _missions;

    /// <summary>Skirmish wins and losses by the player's faction.</summary>
    public IReadOnlyDictionary<Faction, SkirmishTally> ByFaction => _factions;

    /// <summary>Skirmish wins and losses by AI tier (1..3).</summary>
    public IReadOnlyDictionary<int, SkirmishTally> ByAiTier => _tiers;

    /// <summary>Fastest skirmish win in seconds; 0 = none yet.</summary>
    public int FastestWinSeconds { get; }

    /// <summary>Biggest army (total unit cost) the player had at a sample in a skirmish; 0 = none yet.</summary>
    public int BiggestArmy { get; }

    /// <summary>The profile file path: beside the settings file.</summary>
    public static string DefaultPath => Path.Combine(Path.GetDirectoryName(Settings.DefaultPath) ?? ".", "profile.toml");

    /// <summary>A copy with <paramref name="record"/> merged into mission <paramref name="missionId"/>.</summary>
    public PlayerProfile WithMission(string missionId, MissionRecord record)
    {
        if (!ValidKey(missionId)) return this;
        var missions = new SortedDictionary<string, MissionRecord>(_missions, StringComparer.Ordinal);
        missions[missionId] = missions.TryGetValue(missionId, out MissionRecord old) ? old.Merge(record) : record;
        return new PlayerProfile(missions, _factions, _tiers, FastestWinSeconds, BiggestArmy);
    }

    /// <summary>A copy with one finished skirmish against the AI added.</summary>
    /// <param name="faction">The player's faction.</param>
    /// <param name="aiTier">The AI tier played against (1..3).</param>
    /// <param name="won">True for a win.</param>
    /// <param name="seconds">Game length in seconds (a fastest-win candidate when won).</param>
    /// <param name="armyValue">The player's biggest army in the game.</param>
    public PlayerProfile WithSkirmish(Faction faction, int aiTier, bool won, int seconds, int armyValue)
    {
        var factions = new SortedDictionary<Faction, SkirmishTally>(_factions);
        factions[faction] = (factions.TryGetValue(faction, out SkirmishTally f) ? f : default).Add(won);
        var tiers = new SortedDictionary<int, SkirmishTally>(_tiers);
        int tier = Math.Clamp(aiTier, 1, 3);
        tiers[tier] = (tiers.TryGetValue(tier, out SkirmishTally t) ? t : default).Add(won);
        int fastest = FastestWinSeconds;
        if (won && seconds > 0 && (fastest == 0 || seconds < fastest)) fastest = Math.Min(seconds, MaxCount);
        return new PlayerProfile(_missions, factions, tiers, fastest, Math.Max(BiggestArmy, Math.Clamp(armyValue, 0, MaxCount)));
    }

    /// <summary>Loads <paramref name="path"/> (default path when null); never throws.</summary>
    public static PlayerProfile Load(string? path = null)
    {
        try
        {
            string file = path ?? DefaultPath;
            if (!File.Exists(file)) return Empty;
            PlayerProfile parsed = Parse(File.ReadAllText(file), out bool unreadable);
            if (unreadable) KeepAsBad(file);
            return parsed;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Empty;
        }
    }

    private static void KeepAsBad(string file)
    {
        try
        {
            File.Move(file, file + ".bad", overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left where it is: the next Save replaces it.
        }
    }

    /// <summary>Parses profile text; never throws. Bad text, or a newer version, gives <see cref="Empty"/>.</summary>
    public static PlayerProfile Parse(string text) => Parse(text, out _);

    private static PlayerProfile Parse(string text, out bool unreadable)
    {
        unreadable = false;
        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
        }
        catch (Exception)
        {
            unreadable = true;
            return Empty;
        }

        if (Int(root, "version", 1, 0, int.MaxValue) > CurrentVersion)
        {
            unreadable = true;
            return Empty;
        }

        var missions = new SortedDictionary<string, MissionRecord>(StringComparer.Ordinal);
        if (root.TryGetValue("missions", out object? m) && m is TomlTable mt)
        {
            foreach (KeyValuePair<string, object> e in mt)
            {
                if (!ValidKey(e.Key) || e.Value is not TomlTable t) continue;
                missions[e.Key] = new MissionRecord(Bool(t, "completed"), Bool(t, "no_losses"), Int(t, "best_seconds", 0, 0, MaxCount));
            }
        }

        var factions = new SortedDictionary<Faction, SkirmishTally>();
        if (root.TryGetValue("skirmish_factions", out object? f) && f is TomlTable ft)
        {
            foreach (KeyValuePair<string, object> e in ft)
                if (e.Value is TomlTable t && Enum.TryParse(e.Key, out Faction fac) && Enum.IsDefined(fac) && !e.Key.All(char.IsDigit)) factions[fac] = Tally(t);
        }

        var tiers = new SortedDictionary<int, SkirmishTally>();
        if (root.TryGetValue("skirmish_tiers", out object? ti) && ti is TomlTable tt)
        {
            foreach (KeyValuePair<string, object> e in tt)
                if (e.Value is TomlTable t && int.TryParse(e.Key, NumberStyles.None, CultureInfo.InvariantCulture, out int tier) && tier is >= 1 and <= 3) tiers[tier] = Tally(t);
        }

        int fastest = 0, biggest = 0;
        if (root.TryGetValue("skirmish", out object? s) && s is TomlTable st)
        {
            fastest = Int(st, "fastest_win_seconds", 0, 0, MaxCount);
            biggest = Int(st, "biggest_army", 0, 0, MaxCount);
        }

        return new PlayerProfile(missions, factions, tiers, fastest, biggest);
    }

    private static SkirmishTally Tally(TomlTable t) => new(Int(t, "wins", 0, 0, MaxCount), Int(t, "losses", 0, 0, MaxCount));

    /// <summary>The profile as TOML text.</summary>
    public string ToToml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# RTS_2027 medals and records. Delete this file to start again.");
        sb.AppendLine(FormattableString.Invariant($"version = {CurrentVersion}"));
        sb.AppendLine();
        sb.AppendLine("[skirmish]");
        sb.AppendLine(FormattableString.Invariant($"fastest_win_seconds = {FastestWinSeconds}"));
        sb.AppendLine(FormattableString.Invariant($"biggest_army = {BiggestArmy}"));
        foreach ((Faction fac, SkirmishTally t) in _factions)
        {
            sb.AppendLine();
            sb.AppendLine(FormattableString.Invariant($"[skirmish_factions.{fac}]"));
            sb.AppendLine(FormattableString.Invariant($"wins = {t.Wins}"));
            sb.AppendLine(FormattableString.Invariant($"losses = {t.Losses}"));
        }

        foreach ((int tier, SkirmishTally t) in _tiers)
        {
            sb.AppendLine();
            sb.AppendLine(FormattableString.Invariant($"[skirmish_tiers.{tier}]"));
            sb.AppendLine(FormattableString.Invariant($"wins = {t.Wins}"));
            sb.AppendLine(FormattableString.Invariant($"losses = {t.Losses}"));
        }

        foreach ((string id, MissionRecord r) in _missions)
        {
            sb.AppendLine();
            sb.AppendLine(FormattableString.Invariant($"[missions.{id}]"));
            sb.AppendLine(FormattableString.Invariant($"completed = {(r.Completed ? "true" : "false")}"));
            sb.AppendLine(FormattableString.Invariant($"no_losses = {(r.NoLosses ? "true" : "false")}"));
            sb.AppendLine(FormattableString.Invariant($"best_seconds = {r.BestSeconds}"));
        }

        return sb.ToString();
    }

    /// <summary>Writes the file (folders are created, through a temp file). Returns false instead of throwing.</summary>
    public bool Save(string? path = null)
    {
        try
        {
            string file = path ?? DefaultPath;
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

    // Mission ids become TOML table names, so only plain id characters are kept.
    private static bool ValidKey(string key) =>
        key.Length is > 0 and <= 64 && key.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-');

    private static bool Bool(TomlTable t, string key) => t.TryGetValue(key, out object? v) && v is bool b && b;

    private static int Int(TomlTable table, string key, int fallback, int min, int max) =>
        table.TryGetValue(key, out object? value) && value is long l ? (int)Math.Clamp(l, min, max) : fallback;
}
