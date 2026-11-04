// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Sim.Rules;

namespace AI.Tests.Tiers;

/// <summary>M3-11: every tier plays every faction for a while without an exception, deterministically, and is registered for skirmish setup.</summary>
public class TierSmokeTests
{
    private const int Ticks = 3000;

    [Theory]
    [InlineData(1, Faction.Authority)]
    [InlineData(2, Faction.Authority)]
    [InlineData(3, Faction.Authority)]
    [InlineData(4, Faction.Authority)]
    [InlineData(5, Faction.Authority)]
    [InlineData(1, Faction.Directorate)]
    [InlineData(2, Faction.Directorate)]
    [InlineData(3, Faction.Directorate)]
    [InlineData(4, Faction.Directorate)]
    [InlineData(5, Faction.Directorate)]
    public void EveryTierPlaysEveryFactionFor3000Ticks(int tier, Faction faction)
    {
        DifficultyProfile p = TestWorld.Tiers[tier - 1];
        Faction other = faction == Faction.Authority ? Faction.Directorate : Faction.Authority;
        var map = MapGenerator.Generate(48, (ulong)tier);
        var a = new AiSide(AiRegistry.TieredId, p, faction);
        var b = new AiSide(AiRegistry.TieredId, p, other);
        AiGameResult r1 = AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, (ulong)tier, a, b, true, Ticks);
        AiGameResult r2 = AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, (ulong)tier, a, b, true, Ticks);
        Assert.Equal(r1, r2);   // deterministic given the seed
        Assert.True(r1.StatsA.Structures >= 3, "built a base");
        Assert.True(r1.StatsB.Structures >= 3, "the opponent built a base");
    }

    [Fact]
    public void AllFiveTiersAreRegisteredForBothFactions()
    {
        Assert.Contains(AiRegistry.TieredId, AiRegistry.Ids);
        foreach (DifficultyProfile p in TestWorld.Tiers)
            Assert.True(AiRegistry.TryCreate(AiRegistry.TieredId, p, out IAiPlayer? ai) && ai.Id == AiRegistry.TieredId);
        Assert.Equal(5, TestWorld.Tiers.Count);
        for (int i = 0; i < 5; i++) Assert.Equal(i + 1, TestWorld.Tiers[i].Tier);
    }

    [Fact]
    public void ARecruitProfileKeepsTheTierOneOpeningUnchanged()
    {
        // tier 1 through the generic registry id and through "recruit" are the same AI
        var map = MapGenerator.Generate(48, 3);
        AiGameResult a = AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 3,
            new AiSide(AiRegistry.RecruitId, TestWorld.Tiers[0]), new AiSide(AiRegistry.RecruitId, TestWorld.Tiers[0]), true, 2000);
        AiGameResult b = AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 3,
            new AiSide(AiRegistry.TieredId, TestWorld.Tiers[0]), new AiSide(AiRegistry.TieredId, TestWorld.Tiers[0]), true, 2000);
        Assert.Equal(a, b);
    }
}
