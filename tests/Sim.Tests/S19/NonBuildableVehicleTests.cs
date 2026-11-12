// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Tests.S18;

namespace Rts.Sim.Tests.S19;

/// <summary>M13-11 (FACTION-LE-04, D-205): `buildable = false` vehicles are never offered or queued by production.</summary>
public sealed class NonBuildableVehicleTests
{
    [Fact]
    public void FlaggedVehicles_AreNeverUnlocked()
    {
        var w = new S18World(Faction.LeftBehind, Faction.Expedition);
        w.Place(0, "lb_bench", 20, 20);
        w.Run(2);
        Assert.False(w.Sim.IsUnlocked(0, ProductionCategory.Vehicle, w.V("lb_mother_rig_packed")));
        Assert.False(w.Sim.IsUnlocked(0, ProductionCategory.Vehicle, w.V("exp_supply_tender")));
        Assert.False(w.Sim.IsUnlocked(0, ProductionCategory.Vehicle, w.V("exp_command_ship")));
    }
}
