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
                if (def.Faction.Length == 0 || def.Id == "tut_01") continue;
                var ids = svc.Campaign(def.Faction).Missions.Select(m => m.Id).ToList();
                Assert.Contains(def.Id, ids);
                foreach (string next in def.Next) Assert.Contains(next, ids);
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
