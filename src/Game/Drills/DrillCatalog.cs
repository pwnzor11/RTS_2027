// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Profile;
using Rts.Scripting;

namespace Rts.Game.Drills;

/// <summary>S47 U-12: finds the drill missions (a mission folder whose mission.toml has a <c>[drill]</c> table) and joins them with the profile.</summary>
public static class DrillCatalog
{
    /// <summary>Every drill under <c>&lt;dataDir&gt;/missions</c>, in list order, with the best results of <paramref name="profile"/>. A folder that cannot be read is skipped.</summary>
    public static IReadOnlyList<DrillEntry> Load(string dataDir, PlayerProfile profile)
    {
        var list = new List<DrillEntry>();
        string root = Path.Combine(dataDir, "missions");
        if (!Directory.Exists(root)) return list;
        foreach (string folder in Directory.GetDirectories(root).OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                MissionDef def = MissionLoader.Load(folder);
                if (def.Drill is null) continue;
                list.Add(new DrillEntry(def.Id, def.Title, def.Drill, profile.Drills.TryGetValue(def.Id, out DrillRecord r) ? r : default));
            }
            catch (Exception e) when (e is InvalidDataException or IOException)
            {
                // A broken mission folder is skipped; the campaign tests catch it.
            }
        }

        return list.OrderBy(e => e.Def.Order).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
    }
}
