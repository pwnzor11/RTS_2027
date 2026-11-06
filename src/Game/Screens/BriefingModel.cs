// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Rts.Data;
using Rts.Game.Archive;
using Rts.Game.Sandbox;
using Rts.Scripting;
using Rts.Sim.Rules;

namespace Rts.Game.Screens;

/// <summary>
/// The briefing v2 content with no graphics (plan 3.2): title, faction theme colour, the text with Archive links resolved, an objectives
/// preview and the difficulty choice (the five skirmish tier names, D-061).
/// </summary>
public sealed class BriefingModel
{
    /// <summary>Lowest and highest campaign difficulty tier.</summary>
    public const int MinTier = 1;

    /// <summary>Highest campaign difficulty tier.</summary>
    public const int MaxTier = 5;

    private static readonly Regex LinkPattern = new(@"\[\[([A-Za-z0-9_.\-]+)\]\]", RegexOptions.Compiled);

    /// <summary>Creates the model.</summary>
    /// <param name="title">Mission title.</param>
    /// <param name="faction">Faction id (picks the theme).</param>
    /// <param name="text">Briefing text with Archive links already resolved.</param>
    /// <param name="objectives">Objective texts to preview (primary first); may be empty.</param>
    /// <param name="tierNames">Five difficulty names, tier 1 first.</param>
    /// <param name="tier">The starting tier (1..5).</param>
    public BriefingModel(string title, string faction, string text, IReadOnlyList<string> objectives, IReadOnlyList<string> tierNames, int tier)
    {
        Title = title;
        Faction = faction;
        Text = text;
        Objectives = objectives;
        TierNames = tierNames;
        Tier = Math.Clamp(tier, MinTier, MaxTier);
    }

    /// <summary>Mission title.</summary>
    public string Title { get; }

    /// <summary>Faction id.</summary>
    public string Faction { get; }

    /// <summary>The briefing text.</summary>
    public string Text { get; }

    /// <summary>Objective texts for the preview.</summary>
    public IReadOnlyList<string> Objectives { get; }

    /// <summary>The five tier names, tier 1 first.</summary>
    public IReadOnlyList<string> TierNames { get; }

    /// <summary>The chosen tier 1..5.</summary>
    public int Tier { get; private set; }

    /// <summary>Name of the chosen tier.</summary>
    public string TierName => TierNames[Tier - 1];

    /// <summary>Moves the tier by <paramref name="delta"/>, stopping at the ends.</summary>
    public void AdjustTier(int delta) => Tier = Math.Clamp(Tier + delta, MinTier, MaxTier);

    /// <summary>The theme colour of a faction (accent of the title and rule).</summary>
    public static Color ThemeOf(string faction) => faction switch
    {
        "directorate" => new Color(0xC8, 0x60, 0x58),
        "left_behind" => new Color(0xC8, 0xA0, 0x50),
        "expedition" => new Color(0x58, 0xA8, 0xC8),
        _ => new Color(0xD8, 0xC8, 0x8A),
    };

    /// <summary>
    /// Replaces each <c>[[id]]</c> with the Archive entry title, plus " (locked)" while the entry is locked. An id the Archive does not know
    /// shows as the id. (Plain text: the screen does not draw links as greyed or clickable.)
    /// </summary>
    public static string ResolveLinks(string text, ArchiveCatalog catalog, ArchiveProgress progress) => LinkPattern.Replace(text, m =>
    {
        ArchiveEntry? e = catalog.Find(m.Groups[1].Value);
        if (e == null) return m.Groups[1].Value;
        return progress.IsUnlocked(e) ? e.Title : e.Title + " (locked)";
    });

    /// <summary>The five tier names from difficulty.toml ("Tier N" when unreadable).</summary>
    public static IReadOnlyList<string> LoadTierNames(string rulesDir)
    {
        var names = new string[MaxTier];
        for (int i = 0; i < names.Length; i++) names[i] = $"Tier {i + 1}";
        try
        {
            foreach (DifficultyProfile profile in DifficultyLoader.Load(DifficultyLoader.PathBesideRules(rulesDir)))
                if (profile.Tier >= 1 && profile.Tier <= names.Length && profile.Id.Length > 0)
                    names[profile.Tier - 1] = char.ToUpperInvariant(profile.Id[0]) + profile.Id[1..];
        }
        catch (RulesException)
        {
            // Keep "Tier N" names.
        }

        return names;
    }

    /// <summary>
    /// The objectives the mission shows at its start: the mission is built and run for two ticks (so the script's setup code runs) and the
    /// visible objectives read, primary first. Empty when anything goes wrong; nothing is kept.
    /// </summary>
    public static IReadOnlyList<string> PreviewObjectives(string rulesDir, string missionId)
    {
        try
        {
            MissionGame game = MissionGame.Create(rulesDir, missionId);
            game.Sim.Step(Array.Empty<Rts.Sim.Core.Command>());
            game.Sim.Step(Array.Empty<Rts.Sim.Core.Command>());
            return game.Host.VisibleObjectives.OrderByDescending(o => o.Primary).Select(o => (o.Primary ? "" : "(optional) ") + o.Text).ToList();
        }
        catch (Exception e) when (e is InvalidDataException or IOException or InvalidOperationException or RulesException or ArgumentException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Builds the model for a mission folder: the text from the briefing file (or the locale key "briefing" when the file is missing).</summary>
    public static BriefingModel Load(string dataDir, string rulesDir, string missionId, string faction, int tier, ArchiveCatalog catalog, ArchiveProgress archive)
    {
        string folder = MissionLoader.FolderOf(dataDir, missionId);
        MissionDef def = MissionLoader.Load(folder);
        string briefingPath = Path.Combine(folder, def.BriefingFile);
        string text = File.Exists(briefingPath) ? File.ReadAllText(briefingPath)
            : def.Locale.Contains("briefing") ? def.Locale.Resolve("briefing") : "";
        return new BriefingModel(def.Title, def.Faction.Length > 0 ? def.Faction : faction, ResolveLinks(text, catalog, archive),
            PreviewObjectives(rulesDir, missionId), LoadTierNames(rulesDir), tier);
    }
}
