// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.S18;

/// <summary>S18 MIS-04 / FACTION-MATH-01 / FAD-03 / LE-01: a tech-restricted mission can build a granted aircraft, and only a granted one.</summary>
public sealed class AircraftTechTests
{
    private static (Simulation Sim, MissionHost Host, int Interceptor, int Bomber) RestrictedWithAirfield()
    {
        var (sim, host) = TestData.Tiny("function OnInit() end",
            tech: "auth_conyard,auth_power,auth_factory,auth_airfield,auth_interceptor");
        SimRules rules = sim.Setup.Rules;
        int B(string id) => SimRules.IndexOf(rules.Buildings, id, b => b.Id);
        foreach (string id in new[] { "auth_conyard", "auth_power", "auth_factory", "auth_airfield" }) Place(sim, B(id));
        TestData.Run(sim, 2);
        return (sim, host, SimRules.IndexOf(rules.Vehicles, "auth_interceptor", v => v.Id), SimRules.IndexOf(rules.Vehicles, "auth_bomber", v => v.Id));
    }

    // Places a building for player 0 on the first free spot of a scan near the map's top-left quarter.
    private static void Place(Simulation sim, int type)
    {
        for (int y = 16; y < 60; y += 4)
            for (int x = 16; x < 60; x += 4)
                if (!sim.PlaceBuilding(0, type, new CellCoord(x, y)).IsNone) return;
        Assert.Fail("no room for building type " + type);
    }

    [Fact]
    public void A_granted_aircraft_can_be_queued_in_a_restricted_mission()
    {
        var (sim, host, interceptor, _) = RestrictedWithAirfield();
        Assert.True(sim.IsUnlocked(0, ProductionCategory.Aircraft, interceptor));
        sim.Step(new[] { new Command(sim.Tick, 0, 0, CommandKind.Build, default, default, default, (int)ProductionCategory.Aircraft, interceptor) });
        QueueView air = sim.HudFor(0).Queues.Single(q => q.Category == ProductionCategory.Aircraft);
        Assert.Single(air.Items);
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void A_non_granted_aircraft_is_still_refused()
    {
        var (sim, _, _, bomber) = RestrictedWithAirfield();
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Aircraft, bomber));
        sim.Step(new[] { new Command(sim.Tick, 0, 0, CommandKind.Build, default, default, default, (int)ProductionCategory.Aircraft, bomber) });
        Assert.Empty(sim.HudFor(0).Queues.Single(q => q.Category == ProductionCategory.Aircraft).Items);
    }
}
