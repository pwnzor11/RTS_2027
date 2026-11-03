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
        try { return Parse(root, Path.GetFileName(folder)); }
        catch (Exception e) when (e is InvalidDataException or InvalidCastException or KeyNotFoundException or FormatException or OverflowException)
        { throw new InvalidDataException($"{path}: {e.Message}", e); }
    }

    private static MissionDef Parse(TomlTable root, string id)
    {
        TomlTable m = Table(root, "mission");
        var players = new List<MissionPlayer>();
        foreach (object? item in Array(root, "players"))
        {
            var t = (TomlTable)item!;
            IReadOnlyList<string>? tech = null;
            if (t.TryGetValue("tech", out object? techObj) && techObj is TomlArray ta)
                tech = ta.Select(x => (string)x!).ToList();
            players.Add(new MissionPlayer(Int(t, "slot"), Str(t, "name"), Str(t, "role"), (byte)Int(t, "team"),
                t.ContainsKey("cash") ? Int(t, "cash") : 0, tech));
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
            Id = id, Title = Str(m, "title"), MapFile = Str(m, "map"), ScriptFile = Str(m, "script"), BriefingFile = Str(m, "briefing"),
            Seed = (ulong)Int(m, "seed"), Players = players, Zones = zones, Actors = actors,
        };
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
