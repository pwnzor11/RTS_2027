// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Campaign;

namespace Rts.Game.Screens;

/// <summary>One row of the faction pick: the faction, whether it can be started, and why not.</summary>
/// <param name="Faction">Faction id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Open">True when the campaign can be started.</param>
/// <param name="Reason">Why it is locked (empty when open).</param>
/// <param name="Done">Missions completed.</param>
/// <param name="Total">Missions in the campaign.</param>
public sealed record FactionPickRow(string Faction, string Name, bool Open, string Reason, int Done, int Total)
{
    /// <summary>The menu text of the row.</summary>
    public string Label => Open ? $"{Name}  ({Done}/{Total})" : $"{Name}  (locked)";
}

/// <summary>The faction pick (plan 3.4, 2.4) with no graphics: the four campaigns, locks and the reason for each lock.</summary>
public static class FactionPickModel
{
    /// <summary>The rows in standard faction order (only factions that have a campaign file).</summary>
    public static IReadOnlyList<FactionPickRow> Build(CampaignService service)
    {
        var rows = new List<FactionPickRow>();
        foreach (string f in service.Factions)
        {
            CampaignDef def = service.Campaign(f);
            bool open = service.IsFactionOpen(f);
            rows.Add(new FactionPickRow(f, NameOf(f), open, open ? "" : LockReason(def), service.Progress(f).Completed.Count(id => def.Missions.Any(m => m.Id == id)), def.Missions.Count));
        }

        return rows;
    }

    /// <summary>Display name of a faction id.</summary>
    public static string NameOf(string faction) => faction switch
    {
        "directorate" => "Directorate",
        "left_behind" => "Left Behind",
        "expedition" => "Overseas Expedition",
        _ => "Authority",
    };

    /// <summary>"opens after act 2 of the Authority or Directorate campaign" from the <c>open_after</c> ids (prefix auth/dir/lb/exp, number = act).</summary>
    public static string LockReason(CampaignDef def)
    {
        var acts = new SortedSet<int>();
        var names = new List<string>();
        foreach (string id in def.OpenAfter)
        {
            string[] parts = id.Split('_');
            string? faction = parts[0] switch { "auth" => "authority", "dir" => "directorate", "lb" => "left_behind", "exp" => "expedition", _ => null };
            if (faction == null || parts.Length < 2 || !int.TryParse(parts[1], out int act)) return "opens after " + string.Join(" or ", def.OpenAfter);
            acts.Add(act);
            string n = NameOf(faction);
            if (!names.Contains(n)) names.Add(n);
        }

        if (acts.Count == 0) return "locked";
        string actText = $"act {acts.Min}";
        return $"opens after {actText} of the {string.Join(" or ", names)} campaign";
    }
}
