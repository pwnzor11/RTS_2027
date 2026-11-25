// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Archive;
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 ROSTER-LE-03: Archive text must not promise mechanics the data does not have (designation, twin guns, slots, amphibious walking, tow lift).</summary>
public sealed class ExpeditionArchiveTests
{
    private static ArchiveEntry Entry(string id) => ArchiveTestData.Catalog().Find(id) ?? throw new InvalidOperationException(id);

    [Fact]
    public void Patrol_Launch_has_no_twin_machine_gun()
    {
        Assert.DoesNotContain("twin machine gun", Entry("exp_launch").Body);
    }

    [Fact]
    public void Pathfinder_does_not_designate_targets()
    {
        Assert.DoesNotContain("Spotter and designator", Entry("exp_pathfinder").Subtitle);
        Assert.DoesNotContain("firing table", Entry("exp_pathfinder").Body);
    }

    [Fact]
    public void Gyro_Scout_does_not_mark_targets()
    {
        Assert.DoesNotContain("Rotary spotter", Entry("exp_gyro").Subtitle);
        Assert.DoesNotContain("marking targets", Entry("exp_gyro").Body);
    }

    [Fact]
    public void Survey_Hover_does_not_measure_richness()
    {
        Assert.DoesNotContain("measures how thick", Entry("exp_survey_hover").Body);
    }

    [Fact]
    public void Skyhook_carries_six_infantry_and_no_vehicle_slots()
    {
        Assert.DoesNotContain("two light vehicles", Entry("exp_skyhook").Body);
    }

    [Fact]
    public void Marine_is_not_amphibious()
    {
        Assert.DoesNotContain("Amphibious", Entry("exp_marine").Subtitle);
    }

    [Fact]
    public void Tow_Truck_speeds_packed_rigs_instead_of_lifting_them()
    {
        Assert.DoesNotContain("lifts a packed rig onto its bed", Entry("lb_tow").Body);
    }
}
