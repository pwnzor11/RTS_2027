// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Campaign;

/// <summary>One mission in a campaign list.</summary>
/// <param name="Id">Mission id (folder under data/missions).</param>
/// <param name="Requires">Mission ids that must be completed first (empty = open from the start of the campaign).</param>
/// <param name="Still">Act-break still id (<c>data/images/stills/&lt;id&gt;.png</c>) shown on the briefing, or null when the mission has none.</param>
public sealed record CampaignMission(string Id, IReadOnlyList<string> Requires, string? Still = null);

/// <summary>
/// <c>data/campaigns/&lt;faction&gt;.toml</c> (plan 3.5): the faction's missions in play order with unlock rules, and when the faction itself opens.
/// <code>
/// [campaign]
/// faction = "authority"
/// title_key = "campaign.authority.title"
/// ending = "authority_ending"          # optional: data/campaigns/&lt;ending&gt;.toml
/// open_after = ["dir_02"]              # optional: the faction opens when ANY of these missions (any faction) is completed; absent = open
/// [[missions]]
/// id = "auth_01"
/// requires = []                        # optional
/// still = "still_authority_act2"       # optional: act-break still on the briefing
/// </code>
/// </summary>
/// <param name="Faction">Faction id.</param>
/// <param name="TitleKey">Locale key of the campaign title.</param>
/// <param name="Ending">Ending file stem, or empty.</param>
/// <param name="OpenAfter">Missions of which any one opens the faction; empty = open from the start (plan 2.4).</param>
/// <param name="Missions">Missions in play order.</param>
public sealed record CampaignDef(string Faction, string TitleKey, string Ending, IReadOnlyList<string> OpenAfter, IReadOnlyList<CampaignMission> Missions)
{
    /// <summary>True when the faction can be started given the progress of the other factions' campaigns (their completed mission ids).</summary>
    public bool IsOpen(IEnumerable<string> completedAnywhere) => OpenAfter.Count == 0 || completedAnywhere.Any(OpenAfter.Contains);

    /// <summary>True when the mission is listed and all its requirements are completed in <paramref name="progress"/>.</summary>
    public bool IsMissionAvailable(string missionId, CampaignProgress progress)
    {
        CampaignMission? m = Missions.FirstOrDefault(x => x.Id == missionId);
        return m != null && m.Requires.All(progress.IsCompleted);
    }

    /// <summary>An optional id: null when absent, the id when a non-empty string, an error otherwise (an empty string is not allowed).</summary>
    private static string? OptId(TomlTable t, string key)
    {
        if (!t.TryGetValue(key, out object? v)) return null;
        return v is string { Length: > 0 } s ? s : throw new InvalidDataException($"'{key}' must be a non-empty string");
    }

    /// <summary>The path of <c>&lt;faction&gt;.toml</c> under a data folder.</summary>
    public static string PathOf(string dataDir, string faction) => Path.Combine(dataDir, "campaigns", faction + ".toml");

    /// <summary>Loads a campaign file. Throws <see cref="InvalidDataException"/> with the file name on any problem.</summary>
    public static CampaignDef Load(string path)
    {
        try { return Parse(File.ReadAllText(path)); }
        catch (Exception e) when (e is InvalidDataException or IOException) { throw new InvalidDataException(path + ": " + e.Message, e); }
    }

    /// <summary>Parses campaign text. Throws <see cref="InvalidDataException"/>.</summary>
    public static CampaignDef Parse(string text)
    {
        TomlTable root = CampaignToml.Root(text);
        TomlTable c = CampaignToml.Table(root, "campaign");
        string faction = CampaignToml.Str(c, "faction");
        if (!Rts.Scripting.MissionLoader.Factions.Contains(faction)) throw new InvalidDataException($"unknown faction '{faction}'");
        var missions = new List<CampaignMission>();
        if (!root.TryGetValue("missions", out object? mo) || mo is not TomlTableArray ma || ma.Count == 0) throw new InvalidDataException("missing [[missions]]");
        foreach (TomlTable t in ma)
        {
            string id = CampaignToml.Str(t, "id");
            if (missions.Exists(x => x.Id == id)) throw new InvalidDataException($"mission '{id}' is listed twice");
            missions.Add(new CampaignMission(id, CampaignToml.Strings(t, "requires"), OptId(t, "still")));
        }

        foreach (CampaignMission m in missions)
            foreach (string r in m.Requires)
                if (!missions.Exists(x => x.Id == r)) throw new InvalidDataException($"mission '{m.Id}' requires '{r}', which is not in this campaign");
        return new CampaignDef(faction, CampaignToml.OptStr(c, "title_key"), CampaignToml.OptStr(c, "ending"), CampaignToml.Strings(c, "open_after"), missions);
    }
}
