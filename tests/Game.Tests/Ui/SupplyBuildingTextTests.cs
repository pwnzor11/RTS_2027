// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Ui;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Ui;

/// <summary>SUP-4: the faction supply building named in the greyed cameo tooltip, and the keyed text it uses.</summary>
public sealed class SupplyBuildingTextTests
{
    private static readonly SimRules Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/rules not found")).Sim;

    [Theory]
    [InlineData(Faction.Authority, "auth_ration_hall")]
    [InlineData(Faction.Directorate, "dir_bunk_gallery")]
    [InlineData(Faction.LeftBehind, "lb_still_rig")]
    public void EachFactionsSupplyBuildingIsItsCheapestNonConyardSupplier(Faction faction, string id)
    {
        int i = SupplyBuildingLookup.Find(Rules, faction);
        Assert.True(i >= 0);
        Assert.Equal(id, Rules.Buildings[i].Id);
        Assert.NotEqual(Rules.StartKits[(int)faction].Conyard, id);
    }

    [Fact]
    public void NameIsTheDisplayNameNotTheRulesId()
    {
        Assert.Equal("Ration Hall", SupplyBuildingLookup.NameOf(Rules, Faction.Authority));
    }

    [Fact]
    public void NeutralFactionHasNoSupplyBuilding()
    {
        Assert.Equal(-1, SupplyBuildingLookup.Find(Rules, Faction.Neutral));
        Assert.Null(SupplyBuildingLookup.NameOf(Rules, Faction.Neutral));
    }

    [Fact]
    public void CapTooltipNamesTheSupplyBuilding()
    {
        Assert.Equal("Supply cap reached. Build more Ration Hall.", BlockReasonText.Of(QueueBlockReason.SupplyCap, "Ration Hall"));
    }

    [Fact]
    public void CapTooltipWithoutABuildingUsesThePlainLine()
    {
        Assert.Equal("Supply cap reached.", BlockReasonText.Of(QueueBlockReason.SupplyCap, null));
        Assert.Equal("Supply cap reached.", BlockReasonText.Of(QueueBlockReason.SupplyCap, ""));
    }

    [Fact]
    public void KeyedTextFillsThePlaceholderAndKnowsItsKeys()
    {
        Assert.Equal("Waiting for supply", UiText.Get(UiText.SupplyWaiting));
        Assert.Equal("ui.nope", UiText.Get("ui.nope"));
        Assert.Equal("Supply cap reached. Build more Gun Turret.", UiText.Format(UiText.SupplyCapReached, "supplyBuilding", "Gun Turret"));
    }
}
