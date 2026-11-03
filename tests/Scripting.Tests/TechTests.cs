// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>Tech.Grant / Tech.Revoke gate Simulation.IsUnlocked per player (UnlockFilter hook).</summary>
public sealed class TechTests
{
    [Fact]
    public void Grant_and_revoke_change_is_unlocked_only_for_restricted_players()
    {
        var (sim, host) = TestData.Tiny(
            "function OnInit() Trigger.AfterDelay(2, Give); Trigger.AfterDelay(4, Take) end\n"
            + "function Give() Tech.Grant(0, 'auth_wall') end\nfunction Take() Tech.Revoke(0, 'auth_power') end",
            tech: "auth_power");
        sim.PlaceBuilding(0, SimRules.IndexOf(sim.Setup.Rules.Buildings, "auth_conyard", b => b.Id), new Rts.Sim.Core.CellCoord(20, 20));
        sim.PlaceBuilding(1, SimRules.IndexOf(sim.Setup.Rules.Buildings, "auth_conyard", b => b.Id), new Rts.Sim.Core.CellCoord(40, 40));
        int power = SimRules.IndexOf(sim.Setup.Rules.Buildings, "auth_power", b => b.Id);
        int wall = SimRules.IndexOf(sim.Setup.Rules.Buildings, "auth_wall", b => b.Id);
        int turret = SimRules.IndexOf(sim.Setup.Rules.Buildings, "auth_turret", b => b.Id);

        Assert.True(sim.IsUnlocked(0, ProductionCategory.Structure, power));
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Defence, wall));
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Defence, turret));
        Assert.True(sim.IsUnlocked(1, ProductionCategory.Defence, wall), "an unrestricted player keeps the normal rules");

        TestData.Run(sim, 3);
        Assert.True(sim.IsUnlocked(0, ProductionCategory.Defence, wall));
        TestData.Run(sim, 3);
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Structure, power));
        Assert.Empty(host.Errors);
    }
}
