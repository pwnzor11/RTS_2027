// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Xunit;

namespace AI.Tests.S15;

/// <summary>Q-65 (D-182): the build-order insert used by the Directorate economy fix.</summary>
public sealed class Q65TuningTests
{
    [Fact]
    public void ExtraRefineryIsInsertedBehindTheFactoryNotAtTheEnd()
    {
        var b = new BuildOrderManager(["dir_reactor_vent", "dir_smelter_vent", "dir_muster_hatch", "dir_assembly_bore", "dir_pillbox", "dir_rad_mortar"],
            "dir_reactor_vent", "dir_shaft");
        b.InsertAfter("dir_smelter_vent", "dir_assembly_bore");
        Assert.Equal(2, b.StepsOf("dir_smelter_vent"));
        b.InsertAfter("dir_smelter_vent", "dir_missing");   // not listed: appended
        Assert.Equal(3, b.StepsOf("dir_smelter_vent"));
    }
}
