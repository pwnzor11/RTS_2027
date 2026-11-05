// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Sim.Rules;
using Xunit;

namespace AI.Tests;

/// <summary>ai match --record-dir: the side label is printed as is, but its file-name form must hold no path or drive separators.</summary>
public sealed class AiSideFileLabelTests
{
    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    [InlineData(Faction.LeftBehind)]
    public void TheFileLabelHasNoSeparatorsAndTheLabelIsUnchanged(Faction faction)
    {
        var side = new AiSide(AiRegistry.TieredId, TestWorld.Tiers[0], faction);
        Assert.DoesNotContain('/', side.FileLabel);
        Assert.DoesNotContain('\\', side.FileLabel);
        Assert.DoesNotContain(':', side.FileLabel);
        Assert.Equal(AiRegistry.TieredId + ":" + TestWorld.Tiers[0].Id + (faction == Faction.Authority ? "" : "/" + faction.ToString().ToLowerInvariant()), side.Label);
    }

    [Fact]
    public void ANonAuthoritySideGetsAFileNameThatNamesTheFaction()
    {
        var side = new AiSide(AiRegistry.TieredId, TestWorld.Tiers[0], Faction.LeftBehind);
        Assert.Equal(AiRegistry.TieredId + "_" + TestWorld.Tiers[0].Id + "_leftbehind", side.FileLabel);
    }

    [Fact]
    public void AnAuthoritySideGetsTheSameUnderscoredNameAsBefore()
    {
        var side = new AiSide(AiRegistry.TieredId, TestWorld.Tiers[0]);
        Assert.Equal(AiRegistry.TieredId + "_" + TestWorld.Tiers[0].Id, side.FileLabel);
    }
}
