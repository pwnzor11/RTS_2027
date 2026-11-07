// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Archive;
using Rts.Game.Campaign;
using Rts.Game.Screens;

namespace Rts.Game.Tests.Campaign;

/// <summary>M8-14: the optional act-break still of a campaign mission (campaign file <c>still</c> key, shown on the briefing).</summary>
public sealed class CampaignStillTests
{
    private static string RulesDir =>
        RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");

    private static string DataDir => Path.GetFullPath(Path.Combine(RulesDir, ".."));

    private static string Campaign(string stillLine) => $"""
        [campaign]
        faction = "authority"
        [[missions]]
        id = "auth_01"
        {stillLine}
        """;

    [Fact]
    public void A_still_key_is_read_into_the_mission()
    {
        CampaignDef def = CampaignDef.Parse(Campaign("still = \"still_authority_act2\""));
        Assert.Equal("still_authority_act2", def.Missions[0].Still);
    }

    [Fact]
    public void A_mission_without_a_still_key_has_no_still()
    {
        CampaignDef def = CampaignDef.Parse(Campaign(""));
        Assert.Null(def.Missions[0].Still);
    }

    [Fact]
    public void An_empty_still_id_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => CampaignDef.Parse(Campaign("still = \"\"")));
        Assert.Throws<InvalidDataException>(() => CampaignDef.Parse(Campaign("still = 3")));
    }

    [Fact]
    public void The_shipped_act_openers_carry_their_stills()
    {
        CampaignDef auth = CampaignDef.Load(CampaignDef.PathOf(DataDir, "authority"));
        Assert.Null(auth.Missions.Single(m => m.Id == "auth_01").Still);
        Assert.Equal("still_authority_act2", auth.Missions.Single(m => m.Id == "auth_02").Still);
        Assert.Equal("still_authority_act4", auth.Missions.Single(m => m.Id == "auth_04").Still);
    }

    [Fact]
    public void The_briefing_of_an_act_opener_carries_its_still()
    {
        BriefingModel m = BriefingModel.Load(DataDir, RulesDir, "auth_02", "authority", 3, ArchiveCatalog.Empty, new ArchiveProgress());
        Assert.Equal("still_authority_act2", m.Still);
    }

    [Fact]
    public void The_briefing_of_a_mission_without_a_still_has_none()
    {
        BriefingModel m = BriefingModel.Load(DataDir, RulesDir, "auth_01", "authority", 3, ArchiveCatalog.Empty, new ArchiveProgress());
        Assert.Null(m.Still);
    }
}
