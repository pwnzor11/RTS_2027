// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Campaign;
using Rts.Scripting;

namespace Rts.Game.Screens;

/// <summary>One row of the mission select.</summary>
/// <param name="Id">Mission id.</param>
/// <param name="Title">Mission title (the id when the mission is not written yet).</param>
/// <param name="State">Completed, next or locked.</param>
/// <param name="Exists">False for a placeholder id with no mission folder.</param>
public sealed record MissionSelectRow(string Id, string Title, CampaignMissionState State, bool Exists, string Hint = "")
{
    /// <summary>True when the mission can be started: written, and completed or next.</summary>
    public bool CanStart => Exists && State != CampaignMissionState.Locked;

    /// <summary>The state word shown after the title.</summary>
    public string StateText => !Exists ? "not written yet" : State switch
    {
        CampaignMissionState.Completed => "completed",
        CampaignMissionState.Next => "next",
        _ => "locked",
    };

    /// <summary>The menu text of the row.</summary>
    public string Label => $"{Title}  [{StateText}]";
}

/// <summary>The mission-select list of a faction with no graphics (plan 3.4).</summary>
public static class MissionSelectModel
{
    /// <summary>The rows in play order.</summary>
    public static IReadOnlyList<MissionSelectRow> Build(CampaignService service, string dataDir, string faction) =>
        service.Missions(faction).Select(m => new MissionSelectRow(m.Id, m.Exists ? TitleOf(dataDir, m.Id) : m.Id, m.State, m.Exists, HintOf(service, faction, m))).ToList();

    private static string HintOf(CampaignService service, string faction, CampaignMissionEntry m)
    {
        if (!m.Exists) return "This mission is not written yet.";
        if (m.State == CampaignMissionState.Completed) return "Completed. You can replay it.";
        if (m.State == CampaignMissionState.Next) return "Ready to play.";
        CampaignProgress p = service.Progress(faction);
        IEnumerable<string> missing = service.Campaign(faction).Missions.First(x => x.Id == m.Id).Requires.Where(r => !p.IsCompleted(r));
        return "Finish " + string.Join(" and ", missing) + " first.";
    }

    /// <summary>True when <paramref name="missionId"/> is the last mission listed in the faction's campaign.</summary>
    public static bool IsFinal(CampaignService service, string faction, string missionId) =>
        service.Campaign(faction).Missions[^1].Id == missionId;

    private static string TitleOf(string dataDir, string id)
    {
        try { return MissionLoader.Load(MissionLoader.FolderOf(dataDir, id)).Title; }
        catch (Exception e) when (e is InvalidDataException or IOException) { return id; }
    }
}
