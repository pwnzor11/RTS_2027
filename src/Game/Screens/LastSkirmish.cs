// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using Rts.Data;
using Rts.Game.Sprites;
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Screens;

/// <summary>
/// QOL-01 (D-313): the skirmish setup the player started last, kept in <c>%APPDATA%/RTS_2027/skirmish.toml</c> beside settings.toml so the
/// setup screen opens with the same map, slots, factions, tiers, handicaps, colours and match rules. Written atomically (temp file, then move)
/// like the settings; loading is tolerant: a missing, unreadable, newer or invalid file gives null and the screen uses its defaults. Nothing here
/// touches the simulation: it only chooses what the setup screen starts from.
/// </summary>
public static class LastSkirmish
{
    /// <summary>The file format version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Where the last setup is kept: the user's application-data folder, then RTS_2027/skirmish.toml.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RTS_2027", "skirmish.toml");

    /// <summary>The setup as TOML text.</summary>
    public static string ToToml(SkirmishSettings s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# RTS_2027: the last skirmish setup you started. Delete this file to start from the defaults.");
        sb.AppendLine(FormattableString.Invariant($"version = {CurrentVersion}"));
        if (s.Map.File is { } file) sb.AppendLine(FormattableString.Invariant($"map_file = {Quote(file)}"));
        else if (s.Map.Generated is { } g)
        {
            sb.AppendLine(FormattableString.Invariant($"gen_biome = {(int)g.Biome}"));
            sb.AppendLine(FormattableString.Invariant($"gen_size = {(int)g.Size}"));
            sb.AppendLine(FormattableString.Invariant($"gen_seed = {g.Seed}"));
            sb.AppendLine(FormattableString.Invariant($"gen_ocean = {(int)g.OceanEdges}"));
            sb.AppendLine(FormattableString.Invariant($"gen_layout = {g.Layout}"));
            sb.AppendLine(FormattableString.Invariant($"gen_stamps = {(g.Stamps ? "true" : "false")}"));
        }

        sb.AppendLine(FormattableString.Invariant($"superweapon_limit = {s.Rules.SuperweaponLimit}"));
        sb.AppendLine(FormattableString.Invariant($"reveal_map = {(s.Rules.RevealMap ? "true" : "false")}"));
        foreach (SkirmishSlot x in s.Slots)
        {
            sb.AppendLine();
            sb.AppendLine("[[slot]]");
            sb.AppendLine(FormattableString.Invariant($"kind = {(int)x.Kind}"));
            sb.AppendLine(FormattableString.Invariant($"faction = {(int)x.Faction}"));
            sb.AppendLine(FormattableString.Invariant($"ai_tier = {x.AiTier}"));
            sb.AppendLine(FormattableString.Invariant($"colour = {x.Colour}"));
            sb.AppendLine(FormattableString.Invariant($"team = {x.Team}"));
            sb.AppendLine(FormattableString.Invariant($"commander = {(x.Commander ? "true" : "false")}"));
            sb.AppendLine(FormattableString.Invariant($"handicap = {x.Handicap}"));
            sb.AppendLine(FormattableString.Invariant($"credits = {x.CreditsPreset}"));
            sb.AppendLine(FormattableString.Invariant($"small_army = {(x.SmallArmy ? "true" : "false")}"));
        }

        return sb.ToString();
    }

    /// <summary>Parses a saved setup; null for anything that is not a complete, valid setup (never throws).</summary>
    public static SkirmishSettings? Parse(string text)
    {
        try
        {
            TomlTable root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
            if (!root.TryGetValue("version", out object? v) || v is not long version || version < 1 || version > CurrentVersion) return null;
            MapChoice map;
            if (root.TryGetValue("map_file", out object? mf) && mf is string file && file.Length > 0 && file.Length <= 260)
                map = new MapChoice(file, null);
            else if (root.ContainsKey("gen_seed"))
            {
                long biome = Long(root, "gen_biome", 0), size = Long(root, "gen_size", 0), seed = Long(root, "gen_seed", 1);
                long ocean = Long(root, "gen_ocean", 0), layout = Long(root, "gen_layout", MapGenerator.CurrentLayout);
                if (!Enum.IsDefined((Biome)biome) || !Enum.IsDefined((MapSize)size) || seed < 0 || ocean < 0 || ocean > 15 || layout < 0 || layout > 100) return null;
                bool stamps = root.TryGetValue("gen_stamps", out object? st) && st is bool sb2 && sb2;
                map = new MapChoice(null, new MapGenParams((Biome)biome, (MapSize)size, SkirmishSettings.MinPlayers, (ulong)seed, null, (OceanEdges)ocean, (int)layout, stamps));
            }
            else return null;

            var rules = new MatchRules((int)Math.Clamp(Long(root, "superweapon_limit", -1), -1, Rts.Sim.Core.SimSetup.MaxSuperweaponLimit),
                root.TryGetValue("reveal_map", out object? rm) && rm is bool reveal && reveal);
            if (!root.TryGetValue("slot", out object? raw) || raw is not TomlTableArray table) return null;
            var slots = new List<SkirmishSlot>();
            foreach (TomlTable t in table)
            {
                long kind = Long(t, "kind", -1), faction = Long(t, "faction", -1);
                if (kind < 0 || kind > (long)SlotKind.Ai || faction < 0 || faction > byte.MaxValue) return null;
                slots.Add(new SkirmishSlot((SlotKind)kind, (Faction)faction, (int)Long(t, "ai_tier", SkirmishSettings.DefaultAiTier), (int)Long(t, "colour", 0),
                    (int)Long(t, "team", 0), t.TryGetValue("commander", out object? c) && c is true, (int)Long(t, "handicap", SlotOptions.NoHandicap),
                    (int)Long(t, "credits", 0), t.TryGetValue("small_army", out object? sa) && sa is true));
            }

            // Validated against a plain curated map so a map that has gone missing does not throw the rest away; the screen checks the map itself.
            var result = new SkirmishSettings(map, slots, 0) { Rules = rules };
            var probe = new SkirmishSettings(new MapChoice(SkirmishSettings.CuratedFile, null), slots, 0) { Rules = rules };
            return probe.Validate().Count == 0 ? result : null;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Loads the saved setup; null when there is none or it cannot be used (never throws).</summary>
    public static SkirmishSettings? Load(string? path = null)
    {
        try
        {
            string file = path ?? DefaultPath;
            return File.Exists(file) ? Parse(File.ReadAllText(file)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>Writes the setup (folders are created; a temp file then a move, so a crash never truncates it). False instead of throwing.</summary>
    public static bool Save(SkirmishSettings settings, string? path = null)
    {
        try
        {
            string file = path ?? DefaultPath;
            string? dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = file + ".tmp";
            File.WriteAllText(tmp, ToToml(settings));
            File.Move(tmp, file, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Forgets the saved setup (the "Reset" button). False instead of throwing.</summary>
    public static bool Clear(string? path = null)
    {
        try
        {
            string file = path ?? DefaultPath;
            if (File.Exists(file)) File.Delete(file);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static long Long(TomlTable table, string key, long fallback) =>
        table.TryGetValue(key, out object? v) && v is long l ? l : fallback;

    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
