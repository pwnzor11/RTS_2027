// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Archive;
using Rts.Game.Campaign;
using Rts.Scripting;

namespace Rts.Game.Tests.Campaign;

/// <summary>S12: every written campaign mission is listed in its faction's campaign file, and its <c>next</c> names a mission of that campaign.</summary>
public sealed class MissionLinksTests
{
    private static string DataDir =>
        Path.GetFullPath(Path.Combine(RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found"), ".."));

    [Fact]
    public void Written_missions_and_their_next_ids_are_in_their_campaign()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "rts_links_" + Guid.NewGuid().ToString("N"));
        try
        {
            var svc = new CampaignService(DataDir, tmp, new ArchiveProgress(), Path.Combine(tmp, "archive.toml"));
            int checkedCount = 0;
            foreach (string folder in Directory.GetDirectories(Path.Combine(DataDir, "missions")))
            {
                MissionDef def = MissionLoader.Load(folder);
                if (def.Faction.Length == 0 || def.Id == "tut_01" || def.Drill is not null) continue;   // S47 U-12: drills are not in a campaign
                var ids = svc.Campaign(def.Faction).Missions.Select(m => m.Id).ToList();
                Assert.Contains(def.Id, ids);
                // S47 pilot: auth_07 names auth_08 (design 22), a mission that is planned but not written yet; once it exists it must be in the list.
                foreach (string next in def.Next)
                    if (Directory.Exists(Path.Combine(DataDir, "missions", next))) Assert.Contains(next, ids);
                checkedCount++;
            }

            Assert.True(checkedCount >= 4);
        }
        finally
        {
            if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true);
        }
    }
}
