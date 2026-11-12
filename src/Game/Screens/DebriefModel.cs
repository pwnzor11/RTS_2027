// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;
using Rts.Game.Campaign;
using Rts.Scripting;
using Rts.Sim.Core;

namespace Rts.Game.Screens;

/// <summary>What the debrief after a won campaign mission shows (plan 3.4), with no graphics.</summary>
public sealed class DebriefModel
{
    private DebriefModel(string title, int seconds, int primaryDone, int primaryTotal, int optionalDone, int optionalTotal,
        IReadOnlyList<string> newTech, IReadOnlyList<string> newArchive, int tier, bool finalMission)
    {
        Title = title;
        Seconds = seconds;
        PrimaryDone = primaryDone;
        PrimaryTotal = primaryTotal;
        OptionalDone = optionalDone;
        OptionalTotal = optionalTotal;
        NewTech = newTech;
        NewArchive = newArchive;
        Tier = tier;
        FinalMission = finalMission;
    }

    /// <summary>Mission title.</summary>
    public string Title { get; }

    /// <summary>Mission time in whole seconds.</summary>
    public int Seconds { get; }

    /// <summary>Primary objectives completed.</summary>
    public int PrimaryDone { get; }

    /// <summary>Primary objectives listed.</summary>
    public int PrimaryTotal { get; }

    /// <summary>Optional objectives completed.</summary>
    public int OptionalDone { get; }

    /// <summary>Optional objectives listed.</summary>
    public int OptionalTotal { get; }

    /// <summary>Tech ids the campaign gained with this win (not there before).</summary>
    public IReadOnlyList<string> NewTech { get; }

    /// <summary>Titles of the Archive entries this win unlocked.</summary>
    public IReadOnlyList<string> NewArchive { get; }

    /// <summary>The tier the mission was won on.</summary>
    public int Tier { get; }

    /// <summary>True when this was the last mission of the faction's campaign (the ending follows).</summary>
    public bool FinalMission { get; }

    /// <summary>M10-19: medals this win earned for the first time (empty when none).</summary>
    public IReadOnlyList<string> NewMedals { get; init; } = Array.Empty<string>();

    /// <summary>The mission time as m:ss.</summary>
    public string TimeText => $"{Seconds / 60}:{Seconds % 60:D2}";

    /// <summary>Builds the debrief from the run and the progress before and after <see cref="CampaignService.RecordWin"/>.</summary>
    public static DebriefModel Build(string title, string missionId, int ticks, IEnumerable<MissionObjective> objectives, CampaignProgress before,
        CampaignProgress after, int tier, bool finalMission, ArchiveCatalog catalog, IReadOnlyList<string>? newMedals = null)
    {
        var list = objectives.Where(o => !o.Hidden || o.Status == ObjectiveStatus.Completed).ToList();
        int pt = list.Count(o => o.Primary), pd = list.Count(o => o.Primary && o.Status == ObjectiveStatus.Completed);
        int ot = list.Count(o => !o.Primary), od = list.Count(o => !o.Primary && o.Status == ObjectiveStatus.Completed);
        List<string> tech = after.TechSet.Where(t => !before.TechSet.Contains(t)).ToList();
        List<string> archive = before.IsCompleted(missionId) ? new List<string>()
            : catalog.Entries.Where(e => e.Unlock.Kind == ArchiveUnlockKind.Campaign && e.Unlock.Arg == missionId).Select(e => e.Title).ToList();
        return new DebriefModel(title, ticks / SimConstants.TicksPerSecond, pd, pt, od, ot, tech, archive, tier, finalMission)
        {
            NewMedals = newMedals ?? Array.Empty<string>(),
        };
    }

    /// <summary>The lines drawn on the screen.</summary>
    public IReadOnlyList<string> Lines()
    {
        var lines = new List<string>
        {
            "Time: " + TimeText,
            $"Primary objectives: {PrimaryDone}/{PrimaryTotal}",
        };
        if (OptionalTotal > 0) lines.Add($"Optional objectives: {OptionalDone}/{OptionalTotal}");
        lines.Add("New tech: " + (NewTech.Count == 0 ? "none" : string.Join(", ", NewTech)));
        lines.Add("New Archive entries: " + (NewArchive.Count == 0 ? "none" : string.Join(", ", NewArchive)));
        if (NewMedals.Count > 0) lines.Add("New medals: " + string.Join(", ", NewMedals));
        return lines;
    }
}
