// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Combat.CombatFixture;
using static Rts.Sim.Tests.Economy.EconomyFixture;

namespace Rts.Sim.Tests.Economy;

/// <summary>Regression (M2-19): a harvester whose speed divides a cell exactly (16) must still harvest and unload.</summary>
public class HarvesterSpeedTests
{
    [Theory]
    [InlineData(8)] [InlineData(10)] [InlineData(12)] [InlineData(14)] [InlineData(15)] [InlineData(16)] [InlineData(17)]
    [InlineData(18)] [InlineData(20)] [InlineData(22)] [InlineData(24)]
    public void Harvester_AtAnySpeed_UnloadsCredits(int speed)
    {
        SimRules rules = Loaded.Sim;
        int h = Harvester;
        VehicleType[] vs = rules.Vehicles.ToArray();
        vs[h] = vs[h] with { Speed = speed };
        rules = rules with { Vehicles = vs };
        // start south-west of the field so the first leg is a north-east diagonal (+x, -y): the mixed-sign corner case
        var sim = NewSim(PaintMap(OverlayKind.Ashfall, 12), rules);
        sim.PlaceBuilding(0, Bld("test_refinery"), Refinery);
        sim.SpawnVehicle(0, h, new CellCoord(6, 20));
        RunUntil(sim, () => sim.Players.Stored[0] >= 25, 6000);
        Assert.True(sim.Players.Stored[0] >= 25);
    }
}
