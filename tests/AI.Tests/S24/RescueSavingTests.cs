// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Xunit;

namespace AI.Tests.S24;

/// <summary>s24 (M13-23, D-242): the economy rescue saves only for something it can buy.</summary>
public sealed class RescueSavingTests
{
    private static RecruitKnowledge With(params (ProductionCategory, int)[] buildable)
    {
        var k = new RecruitKnowledge
        {
            Hud = new PlayerHud(1345, 0, 5000, 100, 50, Array.Empty<QueueView>(), buildable, false)
        };
        return k;
    }

    [Fact]
    public void HarvesterCannotBeBoughtWithoutAFactoryOption()
    {
        Assert.False(EconomyRescue.CanBuy(With((ProductionCategory.Infantry, 0)), harvester: 3, refinery: 7, haveRefinery: true));
        Assert.True(EconomyRescue.CanBuy(With((ProductionCategory.Vehicle, 3)), harvester: 3, refinery: 7, haveRefinery: true));
    }

    [Fact]
    public void RefineryIsTheGoalWhenNoneStands()
    {
        Assert.False(EconomyRescue.CanBuy(With((ProductionCategory.Vehicle, 3)), harvester: 3, refinery: 7, haveRefinery: false));
        Assert.True(EconomyRescue.CanBuy(With((ProductionCategory.Structure, 7)), harvester: 3, refinery: 7, haveRefinery: false));
    }

    [Theory]
    [InlineData(300, 170, 100, true)]    // surplus survives the sale
    [InlineData(300, 200, 100, true)]    // exactly enough
    [InlineData(300, 240, 100, false)]   // would drop below use
    [InlineData(100, 190, 100, false)]   // already short: never sell more power
    [InlineData(100, 190, 0, true)]      // not a power structure
    public void SaleKeepsThePowerGrid(int powerOut, int powerUse, int buildingPower, bool allowed) =>
        Assert.Equal(allowed, EconomyRescue.KeepsPower(powerOut, powerUse, buildingPower));
}
