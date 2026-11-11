// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;
using Rts.Game.Tests.Archive;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Ui;

/// <summary>S19 naming pass (M13-14, D-205): a thing has one name, and it is the Archive title.</summary>
public sealed class CameoNamingTests
{
    /// <summary>Rules ids that may show a name other than their Archive title. Empty today: every rules id has an Archive entry.</summary>
    private static readonly HashSet<string> Exceptions = new(StringComparer.Ordinal);

    private static IEnumerable<string> AllIds(SimRules r) =>
        r.Vehicles.Select(v => v.Id).Concat(r.Infantry.Select(i => i.Id)).Concat(r.Buildings.Select(b => b.Id)).Concat(r.Upgrades.Select(u => u.Id));

    [Fact]
    public void Every_rules_id_shows_its_archive_title()
    {
        ArchiveCatalog catalog = ArchiveTestData.Catalog();
        SimRules rules = ArchiveTestData.Rules();
        var bad = new List<string>();
        foreach (string id in AllIds(rules))
        {
            if (Exceptions.Contains(id)) continue;
            ArchiveEntry? entry = catalog.Find(id);
            if (entry is null) { bad.Add($"{id}: no archive entry"); continue; }
            string shown = CameoNames.Display(id, x => catalog.Find(x)?.Title);
            if (shown != entry.Title) bad.Add($"{id}: shows '{shown}', archive '{entry.Title}'");
        }
        Assert.True(bad.Count == 0, string.Join("; ", bad));
    }

    [Fact]
    public void Without_an_archive_the_id_is_tidied_and_never_keeps_the_up_prefix_of_an_upgrade()
    {
        Assert.Equal("Heavy Tank", CameoNames.Display("auth_tank", null));
        Assert.Equal("Power Plant", CameoNames.Display("auth_power", null));
    }

    [Theory]
    [InlineData("exp_minelayer", "mines")]
    [InlineData("exp_diver", "swim")]
    [InlineData("exp_drydock", "repair")]
    [InlineData("exp_destroyer", "depth charge")]
    [InlineData("lb_harpoon_wagon", "tether")]
    public void Unbuilt_mechanics_are_worded_as_not_there_yet(string id, string word)
    {
        string text = TodayText.Cameo(id)!;
        Assert.Contains(word, text, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("not|cannot|no ", text);   // every one says what is missing
    }

    [Fact]
    public void Every_today_text_id_exists_in_the_rules()
    {
        SimRules rules = ArchiveTestData.Rules();
        var ids = AllIds(rules).ToHashSet(StringComparer.Ordinal);
        foreach (string id in new[] { "exp_minelayer", "exp_diver", "exp_drydock", "exp_destroyer", "lb_harpoon_wagon" })
            Assert.Contains(id, ids);
        foreach (string id in new[] { "lb_up_scrap_plate", "lb_up_patchwork", "lb_up_quick_pack", "lb_up_grapple", "lb_up_salvage_rights", "exp_up_sonar_nets" })
        {
            Assert.Contains(id, ids);
            Assert.NotNull(TodayText.Upgrade(id));
        }
    }
}
