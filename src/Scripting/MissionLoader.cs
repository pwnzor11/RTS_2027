// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Core;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Scripting;

/// <summary>Reads <c>data/missions/&lt;id&gt;/mission.toml</c> into a <see cref="MissionDef"/>.</summary>
public static class MissionLoader
{
    /// <summary>The mission folder for an id under the data directory.</summary>
    public static string FolderOf(string dataDir, string missionId) => Path.Combine(dataDir, "missions", missionId);

    /// <summary>Loads and validates mission.toml. Throws <see cref="InvalidDataException"/> with the file name on any problem.</summary>
    public static MissionDef Load(string folder)
    {
        string path = Path.Combine(folder, "mission.toml");
        TomlTable root;
        try { root = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path)) ?? throw new InvalidDataException("empty file"); }
        catch (Exception e) when (e is not InvalidDataException) { throw new InvalidDataException($"{path}: {e.Message}", e); }
        try { return Parse(root, Path.GetFileName(folder), folder); }
        catch (Exception e) when (e is InvalidDataException or InvalidCastException or KeyNotFoundException or FormatException or OverflowException)
        { throw new InvalidDataException($"{path}: {e.Message}", e); }
    }

    private static MissionDef Parse(TomlTable root, string id, string folder)
    {
        TomlTable m = Table(root, "mission");
        int schema = m.ContainsKey("schema") ? Int(m, "schema") : 1;
        if (schema is < 1 or > MaxSchema) throw new InvalidDataException($"unsupported mission schema {schema} (this build reads 1 and {MaxSchema})");
        if (schema == 1)
        {
            foreach (string key in SchemaTwoKeys)
                if (m.ContainsKey(key)) throw new InvalidDataException($"'{key}' needs schema = 2 in [mission]");
            foreach (string table in new[] { "starts_with", "unlocks_on_win" })
                if (root.ContainsKey(table)) throw new InvalidDataException($"[{table}] needs schema = 2 in [mission]");
        }
        var players = new List<MissionPlayer>();
        foreach (object? item in Array(root, "players"))
        {
            var t = (TomlTable)item!;
            IReadOnlyList<string>? tech = null;
            if (t.TryGetValue("tech", out object? techObj) && techObj is TomlArray ta)
                tech = ta.Select(x => (string)x!).ToList();
            players.Add(new MissionPlayer(Int(t, "slot"), Str(t, "name"), Str(t, "role"), (byte)Int(t, "team"),
                t.ContainsKey("cash") ? Int(t, "cash") : 0, tech, t.ContainsKey("faction") ? Faction(t) : ""));
        }
        for (int i = 0; i < players.Count; i++)
            if (players[i].Slot != i) throw new InvalidDataException("players must be listed in slot order 0..n-1");
        if (players.Count < 1) throw new InvalidDataException("at least one player required");

        var zones = new List<KeyValuePair<string, MissionZone>>();
        if (root.TryGetValue("zones", out object? zo) && zo is TomlTable zt)
        {
            foreach (string name in zt.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var r = (TomlArray)((TomlTable)zt[name])["rect"];
                if (r.Count != 4) throw new InvalidDataException($"zone '{name}': rect needs 4 numbers");
                var v = r.Select(x => (int)Convert.ToInt64(x, CultureInfo.InvariantCulture)).ToArray();
                if (v[0] > v[2] || v[1] > v[3]) throw new InvalidDataException($"zone '{name}': rect must be x0,y0,x1,y1 with x0<=x1, y0<=y1");
                zones.Add(new(name, new MissionZone(v[0], v[1], v[2], v[3])));
            }
        }

        var actors = new List<MissionActor>();
        if (root.TryGetValue("actors", out object? ao) && ao is TomlTableArray ata)
        {
            foreach (TomlTable t in ata)
            {
                var c = (TomlArray)t["cell"];
                actors.Add(new MissionActor(Str(t, "type"), Int(t, "player"),
                    new CellCoord((int)Convert.ToInt64(c[0], CultureInfo.InvariantCulture), (int)Convert.ToInt64(c[1], CultureInfo.InvariantCulture)),
                    t.ContainsKey("tag") ? Str(t, "tag") : ""));
            }
        }

        return new MissionDef
        {
            Id = id, Folder = folder, Title = Str(m, "title"), MapFile = Str(m, "map"), ScriptFile = Str(m, "script"), BriefingFile = Str(m, "briefing"),
            Seed = (ulong)Int(m, "seed"), Players = players, Zones = zones, Actors = actors,
            Schema = schema,
            Act = schema >= 2 && m.ContainsKey("act") ? IntIn(m, "act", 1, 4) : 0,
            Faction = schema >= 2 && m.ContainsKey("faction") ? Faction(m) : "",
            Next = schema >= 2 && m.ContainsKey("next") ? NextIds(m) : System.Array.Empty<string>(),
            LocaleFile = schema >= 2 && m.ContainsKey("locale") ? Str(m, "locale") : "",
            Locale = schema >= 2 && m.ContainsKey("locale") ? LoadLocale(folder, Str(m, "locale")) : MissionLocale.Empty,
            StartsWith = schema >= 2 ? TechList(root, "starts_with") : System.Array.Empty<string>(),
            UnlocksOnWin = schema >= 2 ? TechList(root, "unlocks_on_win") : System.Array.Empty<string>(),
        };
    }

    /// <summary>The highest mission schema this build reads.</summary>
    public const int MaxSchema = 2;

    /// <summary>The faction ids a schema-2 mission may name.</summary>
    public static readonly IReadOnlyList<string> Factions = new[] { "authority", "directorate", "left_behind", "expedition" };

    private static readonly string[] SchemaTwoKeys = { "act", "faction", "next", "locale" };

    /// <summary>The <c>locale</c> key: a plain file name beside mission.toml. Missing or bad lines are warnings (plan 3.2).</summary>
    private static MissionLocale LoadLocale(string folder, string name)
    {
        if (name.Length == 0 || name != Path.GetFileName(name) || name.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException($"'locale' must be a file name beside mission.toml, got '{name}'");
        return MissionLocale.Load(Path.Combine(folder, name));
    }

    private static string Faction(TomlTable m)
    {
        string f = Str(m, "faction");
        return Factions.Contains(f) ? f : throw new InvalidDataException($"faction '{f}' must be one of {string.Join(", ", Factions)}");
    }

    /// <summary><c>next</c> is one id or a list of one or two ids (two = choice point).</summary>
    private static IReadOnlyList<string> NextIds(TomlTable m)
    {
        object? v = m["next"];
        if (v is string one) return one.Length > 0 ? new[] { one } : throw new InvalidDataException("'next' must not be empty");
        if (v is TomlArray a && a.Count is 1 or 2 && a.All(x => x is string { Length: > 0 })) return a.Select(x => (string)x!).ToList();
        throw new InvalidDataException("'next' must be a mission id or a list of one or two ids");
    }

    /// <summary><c>[table] tech = [ids]</c>; an absent table or key is an empty list.</summary>
    private static IReadOnlyList<string> TechList(TomlTable root, string table)
    {
        if (!root.TryGetValue(table, out object? o)) return System.Array.Empty<string>();
        if (o is not TomlTable t) throw new InvalidDataException($"[{table}] must be a table");
        if (!t.TryGetValue("tech", out object? v)) return System.Array.Empty<string>();
        if (v is not TomlArray a || !a.All(x => x is string { Length: > 0 })) throw new InvalidDataException($"[{table}] tech must be a list of type ids");
        return a.Select(x => (string)x!).ToList();
    }

    private static int IntIn(TomlTable t, string key, int min, int max)
    {
        int v = Int(t, key);
        return v >= min && v <= max ? v : throw new InvalidDataException($"'{key}' must be between {min} and {max}, got {v}");
    }

    private static TomlTable Table(TomlTable t, string key) =>
        t.TryGetValue(key, out object? v) && v is TomlTable r ? r : throw new InvalidDataException($"missing table [{key}]");

    private static TomlTableArray Array(TomlTable t, string key) =>
        t.TryGetValue(key, out object? v) && v is TomlTableArray r ? r : throw new InvalidDataException($"missing [[{key}]]");

    private static string Str(TomlTable t, string key) =>
        t.TryGetValue(key, out object? v) && v is string s ? s : throw new InvalidDataException($"missing or non-string '{key}'");

    private static int Int(TomlTable t, string key) =>
        t.TryGetValue(key, out object? v) && v is long l ? checked((int)l) : throw new InvalidDataException($"missing or non-integer '{key}'");
}
