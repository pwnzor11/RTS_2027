// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 ROSTER-AD-08: archive and lore lines must agree with the data (locomotion, build site, speed, effects, sim behaviour).</summary>
public sealed class DirectorateArchiveTests
{
    private static string Root => RepoPath.Root;
    private static ArchiveEntry Entry(string id) => ArchiveTestData.Catalog().Find(id) ?? throw new InvalidOperationException(id);

    [Fact]
    public void Ash_Sower_is_wheeled_not_tracked()
    {
        Assert.Contains("wheeled vehicle", Entry("dir_ash_sower").Body);
        Assert.DoesNotContain("tracked vehicle", Entry("dir_ash_sower").Body);
    }

    [Fact]
    public void Rocket_Truck_is_built_at_the_Assembly_Bore()
    {
        Assert.DoesNotContain("Muster Hatch", Entry("dir_rocket_truck").Body);
        Assert.Contains("Assembly Bore", Entry("dir_rocket_truck").Body);
    }

    [Fact]
    public void Volunteer_does_not_charge_faster_than_a_conscript()
    {
        Assert.DoesNotContain("charges the enemy line", Entry("dir_volunteer").Body);
    }

    [Fact]
    public void Convoy_Drill_subtitle_names_the_vehicles_it_speeds()
    {
        Assert.DoesNotContain("Faster light vehicles", Entry("auth_up_convoy_drill").Subtitle);
    }

    [Fact]
    public void Broadcast_Mast_has_no_radar_or_sky_watch()
    {
        Assert.DoesNotContain("Radar", Entry("dir_mast").Subtitle);
        Assert.DoesNotContain("watches the sky", Entry("dir_mast").Body);
        string tech = File.ReadAllText(Path.Combine(Root, "data", "archive", "technology.toml"));
        Assert.DoesNotContain("watches the sky for aircraft", tech);
    }

    [Fact]
    public void Hot_Cores_is_not_limited_to_the_crawler()
    {
        Assert.DoesNotContain("the crawler's rounds", Entry("dir_up_hot_cores").Body);
    }

    [Fact]
    public void Foam_layer_slows_every_ground_unit_not_only_foot_and_wheel()
    {
        Assert.DoesNotContain("foot or wheel", Entry("auth_foam_truck").Body);
    }

    [Fact]
    public void Signals_HQ_has_no_map_ping_because_no_code_does_it()
    {
        Assert.DoesNotContain("pings the map", Entry("auth_signals_hq").Body);
    }
}
