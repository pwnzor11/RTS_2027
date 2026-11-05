// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Missions;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-02 contracts (docs/design/11_m7_plan.md §2): enum values, the rules schema v7 fields with "off" defaults, the "shipments",
/// "beachhead" and "naval" parts and the format versions. No gameplay behaviour is tested here: none exists yet.</summary>
public sealed class M7ContractTests
{
    [Fact]
    public void EnumValues_ArePinned()
    {
        Assert.Equal(4, (int)Faction.Expedition);
        Assert.Equal(5, RuleCounts.Factions);
        Assert.Equal(RuleCounts.Factions, Enum.GetValues<Faction>().Length);
        Assert.Equal(3, RuleCounts.PlayableFactions);   // the Expedition stays out of setup until M7-08/M7-14
        Assert.DoesNotContain(Faction.Expedition, RuleCounts.PlayableOrder);
        Assert.False(RuleCounts.IsPlayable(Faction.Expedition));
        Assert.Equal(6, (int)Locomotion.Naval);
        Assert.Equal(7, RuleCounts.Locomotions);
        Assert.Equal(RuleCounts.Locomotions, Enum.GetValues<Locomotion>().Length);
        Assert.Equal(8, (int)LandType.Coast);
        Assert.Equal(9, RuleCounts.LandTypes);
        Assert.Equal(RuleCounts.LandTypes, Enum.GetValues<LandType>().Length);
        Assert.Equal((5, 6, 7), ((int)ArmorClass.ShipLight, (int)ArmorClass.ShipHeavy, (int)ArmorClass.Submerged));
        Assert.Equal(8, RuleCounts.ArmorClasses);
        Assert.Equal(RuleCounts.ArmorClasses, Enum.GetValues<ArmorClass>().Length);
        Assert.Equal((3, 4, 5), ((int)WeaponTargets.Naval, (int)WeaponTargets.Submerged, (int)WeaponTargets.NavalAndSubmerged));
        Assert.Equal((19, 20, 21, 22), ((int)MissionKind.Unload, (int)MissionKind.Submerge, (int)MissionKind.Sortie, (int)MissionKind.ConvoySail));
        Assert.Equal(25, Enum.GetValues<Rts.Sim.Core.CommandKind>().Length);   // M7 adds no CommandKind
    }

    [Fact]
    public void Terrain_HasCoastWithLandingAndNavalColumn()
    {
        foreach (RuleSet rules in new[] { RulesLoader.Load(TestData.RulesDir), RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!) })
        {
            TerrainTable t = rules.Sim.Terrain;
            Assert.True(t.Landing(LandType.Coast));
            Assert.False(t.Landing(LandType.Clear));
            Assert.False(t.Landing(LandType.Shallow));
            Assert.Equal(100, t.SpeedPercent(LandType.Water, Locomotion.Naval));
            Assert.Equal(100, t.SpeedPercent(LandType.Shallow, Locomotion.Naval));
            Assert.Equal(0, t.SpeedPercent(LandType.Coast, Locomotion.Naval));
            foreach (LandType l in new[] { LandType.Clear, LandType.Road, LandType.Rough, LandType.Ash, LandType.Rock, LandType.Cliff })
                Assert.Equal(0, t.SpeedPercent(l, Locomotion.Naval));
            foreach (Locomotion loco in new[] { Locomotion.Foot, Locomotion.Wheeled, Locomotion.Tracked })
                Assert.Equal(t.SpeedPercent(LandType.Clear, loco), t.SpeedPercent(LandType.Coast, loco));
        }
    }
}
