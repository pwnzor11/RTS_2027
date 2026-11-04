// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Llm;

/// <summary>M5-06: the leader personas in <c>data/llm/personas.toml</c>. Contract written by the lead; M5-06 fills the bodies.</summary>
public static class PersonaCatalog
{
    /// <summary>Loads every persona from <paramref name="path"/>; never throws (a missing or bad file gives an empty list).</summary>
    public static IReadOnlyList<CommanderPersona> Load(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : Array.Empty<CommanderPersona>();
        }
        catch (Exception)
        {
            return Array.Empty<CommanderPersona>();
        }
    }

    /// <summary>The persona for <paramref name="player"/> playing <paramref name="faction"/> (the Faction enum's name, e.g. "Authority"):
    /// the faction's personas in file order, rotated by player slot so two commanders of one faction differ. Deterministic.
    /// No match gives <see cref="CommanderPersona.Plain"/>.</summary>
    public static CommanderPersona Pick(IReadOnlyList<CommanderPersona> all, string faction, byte player)
    {
        var matches = new List<CommanderPersona>();
        if (all is not null)
        {
            foreach (CommanderPersona p in all)
            {
                if (string.Equals(p.Faction, faction, StringComparison.OrdinalIgnoreCase)) matches.Add(p);
            }
        }

        if (matches.Count == 0) return CommanderPersona.Plain(faction);
        return matches[player % matches.Count];
    }

    /// <summary>Parses personas.toml text in file order; a table without a non-empty faction, name and voice is skipped. Never throws.</summary>
    internal static IReadOnlyList<CommanderPersona> Parse(string text)
    {
        var result = new List<CommanderPersona>();
        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
        }
        catch (Exception)
        {
            return result;
        }

        foreach (var entry in root)
        {
            if (entry.Value is not TomlTable t) continue;
            string? faction = Str(t, "faction");
            string? name = Str(t, "name");
            string? voice = Str(t, "voice");
            if (faction is null || name is null || voice is null) continue;
            result.Add(new CommanderPersona(entry.Key, faction, name, voice));
        }

        return result;
    }

    private static string? Str(TomlTable t, string key) =>
        t.TryGetValue(key, out object? v) && v is string s && s.Trim().Length > 0 ? s.Trim() : null;
}
