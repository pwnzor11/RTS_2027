// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 RTS_2027 contributors
using Rts.Scripting;
using Rts.Sim.Rules;
using Xunit;

namespace Rts.Scripting.Tests.S19;

/// <summary>S19 D-211: campaign missions run with the ashfall hazard and the infantry ash path cost off until the retune (M13-17).</summary>
public class CampaignAshfallOffTests
{
    [Fact]
    public void CampaignRules_TurnOffAshDamageAndAshPathCost_AndKeepTheRest()
    {
        var skirmish = new SimRules(System.Array.Empty<VehicleType>(), System.Array.Empty<InfantryType>(), System.Array.Empty<BuildingType>(),
            System.Array.Empty<WeaponType>(), System.Array.Empty<WarheadType>(), null!, System.Array.Empty<EffectDef>());
        SimRules campaign = MissionSession.CampaignRules(skirmish);
        Assert.Equal(0, campaign.Radiation.AshfallDamage);
        Assert.Equal(1, campaign.Radiation.AshPathPenalty);
        Assert.Equal(skirmish.Radiation.AshfallIntervalTicks, campaign.Radiation.AshfallIntervalTicks);
        Assert.Equal(skirmish.Radiation with { AshfallDamage = 0, AshPathPenalty = 1 }, campaign.Radiation);
        Assert.True(skirmish.Radiation.AshfallDamage > 0, "skirmish keeps the hazard by default");
    }
}
