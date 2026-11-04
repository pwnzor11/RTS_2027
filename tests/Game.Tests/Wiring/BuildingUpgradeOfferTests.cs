// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Tests.Archive;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Wiring;

/// <summary>Enable/disable logic of the Hatch to Bore Head upgrade button (M3-18).</summary>
public sealed class BuildingUpgradeOfferTests
{
    private static readonly SimRules Rules = ArchiveTestData.Rules();
    private static int Index(string id) => Rules.Buildings.ToList().FindIndex(b => b.Id == id);

    [Fact]
    public void A_building_without_a_morph_target_has_no_button()
    {
        Assert.Null(BuildingUpgradeOffer.For(Rules, Index("dir_bore_head"), 9999, new[] { "dir_hatch" }));
        Assert.Null(BuildingUpgradeOffer.For(Rules, -1, 9999, Array.Empty<string>()));
    }

    [Fact]
    public void Hatch_with_cash_and_prerequisites_is_enabled_at_the_price_difference()
    {
        int hatch = Index("dir_hatch");
        BuildingType bore = Rules.Buildings[Index("dir_bore_head")];
        int cost = bore.Cost - Rules.Buildings[hatch].Cost;

        BuildingUpgradeOffer offer = BuildingUpgradeOffer.For(Rules, hatch, cost, new[] { "dir_hatch", "dir_reactor_vent" })!.Value;

        Assert.True(offer.Enabled);
        Assert.Equal("", offer.Reason);
        Assert.Equal(cost, offer.Cost);
        Assert.Equal(Index("dir_bore_head"), offer.TargetType);
        Command c = offer.Command(0, new EntityId(5, 1));
        Assert.Equal((CommandKind.Upgrade, offer.TargetType), (c.Kind, c.Arg));
    }

    [Fact]
    public void Too_little_cash_disables_with_the_shortfall()
    {
        int hatch = Index("dir_hatch");
        BuildingUpgradeOffer offer = BuildingUpgradeOffer.For(Rules, hatch, 0, new[] { "dir_hatch" })!.Value;
        Assert.False(offer.Enabled);
        Assert.Equal($"Needs ${offer.Cost} more cash", offer.Reason);
    }

    [Fact]
    public void A_missing_prerequisite_disables_and_names_it()
    {
        int hatch = Index("dir_hatch");
        BuildingType bore = Rules.Buildings[Index("dir_bore_head")];
        string? missing = bore.Prerequisites.FirstOrDefault(p => p != "dir_hatch");
        if (missing is null) return;   // data has no extra prerequisite: nothing to check
        BuildingUpgradeOffer offer = BuildingUpgradeOffer.For(Rules, hatch, 99999, new[] { "dir_hatch" })!.Value;
        Assert.False(offer.Enabled);
        Assert.Equal($"Needs {missing}", offer.Reason);
    }
}
