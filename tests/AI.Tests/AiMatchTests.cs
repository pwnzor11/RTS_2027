// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Sim.Map;

namespace AI.Tests;

public class AiMatchTests
{
    private static AiSide Side(string ai, int tier) => new(ai, TestWorld.Tiers[tier - 1]);
    private static MapData Map(ulong seed) => Rts.Data.MapGenerator.Generate(32, seed);

    private static AiGameResult Play(string a, int ta, string b, int tb, ulong seed, bool aInSlot0, int ticks, Stream? replay = null) =>
        AiMatchRunner.Play(Map(seed), TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, seed, Side(a, ta), Side(b, tb), aInSlot0, ticks, replay);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SkirmisherBeatsIdle(bool aInSlot0)
    {
        AiGameResult r = Play("skirmisher", 3, "idle", 1, 1, aInSlot0, 4000);
        Assert.Equal(0, r.Winner);
        Assert.True(r.EndTick < 4000);
        Assert.Equal(0, r.ValueB);
    }

    [Fact]
    public void IdleVersusIdleIsADraw()
    {
        AiGameResult r = Play("idle", 1, "idle", 1, 1, true, 300);
        Assert.Equal(-1, r.Winner);
        Assert.Equal(300, r.EndTick);
        Assert.Equal(r.ValueA, r.ValueB);
    }

    [Fact]
    public void SameInputsGiveIdenticalGamesAndReplays()
    {
        using var ra = new MemoryStream();
        using var rb = new MemoryStream();
        AiGameResult a = Play("skirmisher", 4, "skirmisher", 2, 3, true, 1500, ra);
        AiGameResult b = Play("skirmisher", 4, "skirmisher", 2, 3, true, 1500, rb);
        Assert.Equal(a, b);
        Assert.NotEqual(0UL, a.FinalHash);
        Assert.Equal(ra.ToArray(), rb.ToArray());
        Assert.True(ra.Length > 100);
    }

    [Fact]
    public void DifferentSeedsDiffer()
    {
        AiGameResult a = Play("skirmisher", 3, "skirmisher", 3, 1, true, 1200);
        AiGameResult b = Play("skirmisher", 3, "skirmisher", 3, 2, true, 1200);
        Assert.NotEqual(a.FinalHash, b.FinalHash);
    }
}
