// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;
using Rts.Game.Sandbox;
using Rts.Scripting;

namespace Rts.Game.Campaign;

/// <summary>
/// The campaign as the game uses it (plan 3.3, 3.4): campaign lists, per-faction progress files, mission states, the tech allow-list at mission
/// start, and recording a win. Progress is profile data, never sim state, and nothing here touches skirmish.
/// </summary>
public sealed class CampaignService
{
    private readonly string _dataDir;
    private readonly string? _profileDir;
    private readonly ArchiveProgress _archive;
    private readonly string? _archivePath;
    private readonly Dictionary<string, CampaignDef> _defs = new();

    /// <summary>Creates the service.</summary>
    /// <param name="dataDir">The data folder (holds campaigns, missions, rules, maps).</param>
    /// <param name="profileDir">Folder for <c>campaign_&lt;faction&gt;.toml</c>; null = beside settings.toml.</param>
    /// <param name="archive">The Archive progress to mark on a win; null = loaded from <paramref name="archivePath"/> (or the default path).</param>
    /// <param name="archivePath">Where the Archive progress is saved; null = the default path.</param>
    public CampaignService(string dataDir, string? profileDir = null, ArchiveProgress? archive = null, string? archivePath = null)
    {
        _dataDir = dataDir;
        _profileDir = profileDir;
        _archivePath = archivePath;
        _archive = archive ?? ArchiveProgress.Load(archivePath);
    }

    /// <summary>The factions that have a campaign file, in the standard faction order.</summary>
    public IReadOnlyList<string> Factions =>
        MissionLoader.Factions.Where(f => File.Exists(CampaignDef.PathOf(_dataDir, f))).ToList();

    /// <summary>The campaign list of a faction (cached). Throws <see cref="InvalidDataException"/> if the file is missing or invalid.</summary>
    public CampaignDef Campaign(string faction)
    {
        if (!_defs.TryGetValue(faction, out CampaignDef? def))
            _defs[faction] = def = CampaignDef.Load(CampaignDef.PathOf(_dataDir, faction));
        return def;
    }

    /// <summary>The progress file path of a faction.</summary>
    public string ProgressPath(string faction) =>
        _profileDir == null ? CampaignProgress.DefaultPath(faction) : Path.Combine(_profileDir, "campaign_" + faction + ".toml");

    /// <summary>Loads a faction's progress from disk (empty when missing or unreadable).</summary>
    public CampaignProgress Progress(string faction) => CampaignProgress.Load(faction, ProgressPath(faction));

    /// <summary>Every completed mission id over all factions' progress files.</summary>
    public IReadOnlyList<string> CompletedAnywhere() =>
        Factions.SelectMany(f => Progress(f).Completed).Distinct().ToList();

    /// <summary>True when the faction can be started (its <c>open_after</c> rule, plan 2.4).</summary>
    public bool IsFactionOpen(string faction) => Campaign(faction).IsOpen(CompletedAnywhere());

    /// <summary>The state of one mission under a progress record. A mission not in the campaign is Locked.</summary>
    public CampaignMissionState StateOf(string faction, string missionId, CampaignProgress progress)
    {
        if (progress.IsCompleted(missionId)) return CampaignMissionState.Completed;
        return Campaign(faction).IsMissionAvailable(missionId, progress) ? CampaignMissionState.Next : CampaignMissionState.Locked;
    }

    /// <summary>The mission-select rows of a faction in play order.</summary>
    public IReadOnlyList<CampaignMissionEntry> Missions(string faction)
    {
        CampaignProgress p = Progress(faction);
        return Campaign(faction).Missions
            .Select(m => new CampaignMissionEntry(m.Id, StateOf(faction, m.Id, p), MissionExists(m.Id)))
            .ToList();
    }

    /// <summary>
    /// The human player's allow-list at mission start (plan 3.3): the mission's own restriction (<c>[players].tech</c>) plus <c>starts_with</c>
    /// plus the campaign tech set, sorted. Empty when the mission restricts nobody (no player tech and no starts_with): the set only lifts
    /// within a restriction and never adds one.
    /// </summary>
    public static IReadOnlyList<string> AllowListFor(MissionDef def, CampaignProgress progress)
    {
        MissionPlayer? human = def.Players.FirstOrDefault(p => p.Role == "human");
        if (human == null || (human.Tech == null && def.StartsWith.Count == 0)) return Array.Empty<string>();
        var set = new SortedSet<string>(StringComparer.Ordinal);
        if (human.Tech != null) foreach (string id in human.Tech) set.Add(id);
        foreach (string id in def.StartsWith) set.Add(id);
        foreach (string id in progress.TechSet) set.Add(id);
        return set.ToList();
    }

    /// <summary>The mission definition with the allow-list put in the human player's tech (what the sim's existing restriction path reads).</summary>
    public static MissionDef WithAllowList(MissionDef def, IReadOnlyList<string> allow)
    {
        if (allow.Count == 0) return def;
        return new MissionDef
        {
            Id = def.Id, Title = def.Title, MapFile = def.MapFile, ScriptFile = def.ScriptFile, BriefingFile = def.BriefingFile,
            Seed = def.Seed, Zones = def.Zones, Actors = def.Actors, Schema = def.Schema, Act = def.Act, Faction = def.Faction,
            Next = def.Next, LocaleFile = def.LocaleFile, StartsWith = def.StartsWith, UnlocksOnWin = def.UnlocksOnWin,
            Players = def.Players.Select(p => p.Role == "human" ? p with { Tech = allow } : p).ToList(),
        };
    }

    /// <summary>
    /// Sets up a campaign mission: checks the faction is open and the mission available (completed missions may be replayed), computes the
    /// allow-list from the saved progress and builds the game with it.
    /// </summary>
    /// <param name="rulesDir">The data/rules directory.</param>
    /// <param name="faction">Campaign faction.</param>
    /// <param name="missionId">Mission id.</param>
    /// <param name="difficulty">Skirmish tier 1..5 (the AI strength).</param>
    /// <exception cref="InvalidOperationException">The faction is locked, or the mission is locked or not in the campaign.</exception>
    public CampaignStart Start(string rulesDir, string faction, string missionId, int difficulty)
    {
        if (difficulty is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(difficulty), "difficulty must be 1..5");
        if (!IsFactionOpen(faction)) throw new InvalidOperationException($"the {faction} campaign is not open yet");
        CampaignProgress progress = Progress(faction);
        if (StateOf(faction, missionId, progress) == CampaignMissionState.Locked)
            throw new InvalidOperationException($"mission '{missionId}' is locked in the {faction} campaign");

        MissionDef def = MissionLoader.Load(MissionLoader.FolderOf(_dataDir, missionId));
        IReadOnlyList<string> allow = AllowListFor(def, progress);
        MissionGame game = MissionGame.Create(rulesDir, missionId, difficulty, d => WithAllowList(d, allow));
        // M8-03 wires SetDifficulty here (the host's Mission.Difficulty() value); not called yet because the setter does not exist on this branch.
        return new CampaignStart(game, faction, allow, difficulty);
    }

    /// <summary>
    /// Records a won mission: adds its <c>unlocks_on_win</c> ids to the tech set, marks it completed with the difficulty, calls
    /// <see cref="ArchiveProgress.MarkCampaignMission"/> and saves both files. Returns the new progress.
    /// </summary>
    /// <exception cref="InvalidOperationException">The mission is not in the faction's campaign.</exception>
    public CampaignProgress RecordWin(string faction, string missionId, int difficulty)
    {
        if (!Campaign(faction).Missions.Any(m => m.Id == missionId))
            throw new InvalidOperationException($"mission '{missionId}' is not in the {faction} campaign");
        MissionDef def = MissionLoader.Load(MissionLoader.FolderOf(_dataDir, missionId));
        CampaignProgress next = Progress(faction).WithWin(missionId, difficulty, def.UnlocksOnWin);
        next.Save(ProgressPath(faction));
        _archive.MarkCampaignMission(missionId);
        _archive.Save(_archivePath);
        return next;
    }

    private bool MissionExists(string id) => Directory.Exists(MissionLoader.FolderOf(_dataDir, id));
}
