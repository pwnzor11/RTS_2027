// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 ROSTER-LE-05: upgrade wording must match the effect list (Vacuum Cores, Salvage Rights, Scrap Plate).</summary>
public sealed class UpgradeArchiveTests
{
    private static string Root => RepoPath.Root;
    private static ArchiveEntry Entry(string id) => ArchiveTestData.Catalog().Find(id) ?? throw new InvalidOperationException(id);

    [Fact]
    public void Vacuum_Cores_names_only_the_four_guns_it_changes()
    {
        Assert.DoesNotContain("every fleet vehicle", Entry("exp_up_vacuum_cores").Body);
    }

    [Fact]
    public void Salvage_Rights_does_not_raise_the_value_of_a_wreck()
    {
        Assert.DoesNotContain("more value for every scrap", Entry("lb_up_salvage_rights").Body);
        string rules = File.ReadAllText(Path.Combine(Root, "data", "rules", "upgrades.toml"));
        Assert.DoesNotContain("wreck value x1.5", rules);
    }

    [Fact]
    public void Scrap_Plate_adds_hit_points_not_armour()
    {
        Assert.DoesNotContain("adds to the armour", Entry("lb_up_scrap_plate").Body);
    }
}
