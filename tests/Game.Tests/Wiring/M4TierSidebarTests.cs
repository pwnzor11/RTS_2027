// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Wiring;

/// <summary>M4-09: tier headings and locked previews, the Aircraft tab, and upgrade effect text.</summary>
public sealed class M4TierSidebarTests
{
    private static readonly SimRules Rules = Build();

    // Vehicles (Authority): 0 jeep T1, 1 tank T1, 2 mortar T2, 3 bulwark T3, 4 interceptor T2 (air). Gates: auth_engineering (T2), auth_hq (T3).
    private static SimRules Build()
    {
        string dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        SimRules b = RulesLoader.Load(dir).Sim;
        VehicleType v = b.Vehicles[0] with { Faction = Faction.Authority, Locomotion = Locomotion.Tracked, Cost = 100 };
        BuildingType bt = b.Buildings[0];
        FactionStartKit kit = b.StartKits[0] with { Tier2Provides = "auth_tier2", Tier3Provides = "auth_tier3" };
        return b with
        {
            Vehicles = new[]
            {
                v with { Id = "auth_jeep", Tier = 1 }, v with { Id = "auth_tank", Tier = 1 }, v with { Id = "auth_mortar", Tier = 2 },
                v with { Id = "auth_bulwark", Tier = 3 }, v with { Id = "auth_interceptor", Tier = 2, Locomotion = Locomotion.Air },
            },
            Buildings = new[]
            {
                bt with { Id = "auth_engineering", Provides = new[] { "auth_tier2" } },
                bt with { Id = "auth_hq", Provides = new[] { "auth_tier3" } },
            },
            StartKits = new[] { kit, b.StartKits[1] },
            Effects = new[] { new EffectDef("flak", "mul", "class:infantry", "max_health", 1150), new EffectDef("lamps", "add", "auth_searchlight", "sight", 2) },
        };
    }

    private static BuildOption Opt(ProductionCategory c, int i) => new(c, i, true, QueueBlockReason.None, 100);

    private static IReadOnlyList<TierEntry> Entries(int playerTier, ProductionCategory c, params int[] owned) =>
        TierGrouping.Entries(Rules, Faction.Authority, playerTier, c, owned.Select(i => Opt(c, i)).ToList());

    [Fact]
    public void Tier_one_only_stays_a_flat_grid_without_locked_previews_below_the_gate()
    {
        // Player at tier 1 owns jeep and tank; mortar (T2) and bulwark (T3) show greyed, so headings appear.
        var e = Entries(1, ProductionCategory.Vehicle, 0, 1);
        Assert.Equal(new[] { 1, 1, 2, 3 }, e.Select(x => x.Tier).ToArray());
        Assert.Equal(new[] { false, false, true, true }, e.Select(x => x.Locked).ToArray());
        Assert.Equal("auth_engineering", e[2].Gate);
        Assert.Equal("auth_hq", e[3].Gate);
    }

    [Fact]
    public void Reached_tiers_show_only_what_the_hud_offers_and_no_locked_previews()
    {
        var e = Entries(2, ProductionCategory.Vehicle, 0, 2);   // tier 2: mortar owned, tier 3 still previewed
        Assert.Equal(new[] { (1, false), (2, false), (3, true) }, e.Select(x => (x.Tier, x.Locked)).ToArray());
        var all = Entries(3, ProductionCategory.Vehicle, 0, 1);
        Assert.All(all, x => Assert.False(x.Locked));
        Assert.False(TierGrouping.NeedsHeadings(all));   // tier 1 only content = flat
    }

    [Fact]
    public void Aircraft_and_vehicle_tabs_are_kept_apart_in_locked_previews()
    {
        Assert.DoesNotContain(Entries(1, ProductionCategory.Vehicle, 0), x => x.Option.TypeIndex == 4);
        var air = Entries(1, ProductionCategory.Aircraft);
        Assert.Equal(new[] { 4 }, air.Select(x => x.Option.TypeIndex).ToArray());
        Assert.True(air[0].Locked);
    }

    [Fact]
    public void Rows_put_a_heading_before_each_tier_and_wrap_cameos_by_column()
    {
        var rows = TierGrouping.Rows(Entries(1, ProductionCategory.Vehicle, 0, 1), 2);
        Assert.Equal(new[] { (true, 1), (false, 1), (true, 2), (false, 2), (true, 3), (false, 3) }, rows.Select(r => (r.Heading, r.Tier)).ToArray());
        Assert.Equal(2, rows[1].Cells.Count);
        Assert.True(rows[2].Locked);
        Assert.False(rows[0].Locked);
        Assert.Equal("Tier 1", TierGrouping.HeadingText(rows[0]));
        Assert.Equal("Tier 2  (needs Engineering)", TierGrouping.HeadingText(rows[2]));
        Assert.Equal("Needs Engineering", TierGrouping.NeedsText(rows[3].Cells[0]));
    }

    [Fact]
    public void Three_cameos_in_two_columns_make_two_rows_and_flat_grids_have_no_headings()
    {
        var flat = TierGrouping.Rows(Entries(3, ProductionCategory.Vehicle, 0, 1, 1), 2);
        Assert.All(flat, r => Assert.False(r.Heading));
        var rows = TierGrouping.Rows(Entries(3, ProductionCategory.Vehicle, 0, 1, 2), 2);
        Assert.Equal(new[] { true, false, true, false }, rows.Select(r => r.Heading).ToArray());
    }

    [Fact]
    public void No_gate_named_in_the_rules_means_no_locked_previews()
    {
        SimRules bare = Rules with { StartKits = new[] { Rules.StartKits[0] with { Tier2Provides = "", Tier3Provides = "" }, Rules.StartKits[1] } };
        var e = TierGrouping.Entries(bare, Faction.Authority, 1, ProductionCategory.Vehicle, new[] { Opt(ProductionCategory.Vehicle, 0) });
        Assert.Single(e);
    }

    [Fact]
    public void Aircraft_tab_is_wired_to_its_category_key_and_caption()
    {
        Assert.Equal(ProductionCategory.Aircraft, SidebarTabs.CategoryOf(SidebarTab.Aircraft));
        Assert.Equal('Y', SidebarTabs.KeyLetter(SidebarTab.Aircraft));
        Assert.Equal("AIR", SidebarTabs.Caption(SidebarTab.Aircraft));
        Assert.Equal(5, SidebarTabs.CountFor(false));
        Assert.Equal(6, SidebarTabs.CountFor(true));
        Assert.Equal(6, SidebarLayout.Compute(1280, 720, 1f, 6).Tabs.Count);
        Assert.Equal(5, SidebarLayout.Compute(1280, 720, 1f).Tabs.Count);
    }

    [Fact]
    public void Upgrade_tooltip_text_comes_from_the_effects_list()
    {
        var up = new UpgradeType("auth_up_x", Faction.Authority, 100, 0, Array.Empty<string>(), "") { Effects = new[] { "flak", "lamps", "missing" } };
        Assert.Equal(new[] { "All infantry: max health x1.15", "Auth Searchlight: sight +2".Replace("Auth Searchlight", "Searchlight") }, UpgradeEffectText.Lines(Rules, up));
        var special = new UpgradeType("auth_grouting", Faction.Authority, 100, 0, Array.Empty<string>(), "grouting");
        Assert.Equal(new[] { "Grouting" }, UpgradeEffectText.Lines(Rules, special));
    }

    [Theory]
    [InlineData(1150, "1.15")]
    [InlineData(800, "0.8")]
    [InlineData(2000, "2")]
    [InlineData(750, "0.75")]
    public void Permille_reads_as_a_factor(int value, string expected) => Assert.Equal(expected, UpgradeEffectText.Permille(value));
}
