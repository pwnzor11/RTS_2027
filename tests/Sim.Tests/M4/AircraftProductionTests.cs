// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.M4.RulesTierFixture;

namespace Rts.Sim.Tests.M4;

/// <summary>M4-04: the Aircraft production lane (design 08 §6): Arg2 = an Air vehicle index, built at a produces = "aircraft"
/// building; the Vehicle lane refuses Air types and the Aircraft lane refuses ground types.</summary>
public sealed class AircraftProductionTests
{
    private static int AliveOfType(Simulation sim, int type)
    {
        int n = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == 0 && sim.Units.TypeIndex[i] == type) n++;
        return n;
    }

    [Fact]
    public void Aircraft_QueueAndLeaveFromTheAirfield()
    {
        Simulation sim = NewSim(("test_airfield", 16, 4));
        int plane = Veh("test_plane");
        Assert.True(sim.CanBuild(0, ProductionCategory.Aircraft, plane));
        Assert.Contains((ProductionCategory.Aircraft, plane), sim.HudFor(0).Buildable);
        Assert.Contains(sim.HudFor(0).Options, o => o.Category == ProductionCategory.Aircraft && o.TypeIndex == plane && o.Cost == 300);
        int cash = sim.Players.Cash[0];
        Run(sim, 1, Build(sim, ProductionCategory.Aircraft, plane));
        Assert.Equal(1, sim.Production.QueueLength(0, ProductionCategory.Aircraft));
        for (int i = 0; i < 300 && AliveOfType(sim, plane) == 0; i++) sim.Step(None);
        Assert.Equal(1, AliveOfType(sim, plane));
        Assert.Equal(cash - 300, sim.Players.Cash[0]);
        Assert.Equal(0, sim.Production.QueueLength(0, ProductionCategory.Aircraft));
        // it left from the airfield's exit (16 + 3/2, 4 + 3) or next to it
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.TypeIndex[i] == plane)
            {
                CellCoord c = sim.Units.Position[i].ToCell();
                Assert.InRange(c.X, 16, 20);
                Assert.InRange(c.Y, 6, 9);
            }
    }

    [Fact]
    public void AirType_InTheVehicleLane_IsRefused_AndGroundType_InTheAircraftLane()
    {
        Simulation sim = NewSim(("test_airfield", 16, 4), ("test_factory", 4, 9));
        int plane = Veh("test_plane"), tank = Veh("test_tank");
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Vehicle, plane));
        Assert.Equal(QueueBlockReason.NotBuildable, sim.QueueBlockReason(0, ProductionCategory.Vehicle, plane));
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Aircraft, tank));
        Run(sim, 1, Build(sim, ProductionCategory.Vehicle, plane), Build(sim, ProductionCategory.Aircraft, tank));
        Assert.Equal(0, sim.Production.QueueLength(0, ProductionCategory.Vehicle));
        Assert.Equal(0, sim.Production.QueueLength(0, ProductionCategory.Aircraft));
        Assert.DoesNotContain((ProductionCategory.Vehicle, plane), sim.HudFor(0).Buildable);
        Assert.DoesNotContain((ProductionCategory.Aircraft, tank), sim.HudFor(0).Buildable);
    }

    [Fact]
    public void Aircraft_NeedAnAircraftFactory()
    {
        Simulation sim = NewSim(("test_factory", 4, 9));
        int plane = Veh("test_plane");
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Aircraft, plane));   // prerequisite test_airbase missing
        EntityId field = Place(sim, 0, "test_airfield", 16, 4);
        Assert.True(sim.IsUnlocked(0, ProductionCategory.Aircraft, plane));
        Assert.Equal(QueueBlockReason.None, sim.QueueBlockReason(0, ProductionCategory.Aircraft, plane));
        Run(sim, 2, Kill(sim, field));
        Assert.Equal(QueueBlockReason.NoFactory, sim.QueueBlockReason(0, ProductionCategory.Aircraft, plane));
    }

    [Fact]
    public void AircraftLane_SurvivesSaveMidBuild()
    {
        Simulation sim = NewSim(("test_airfield", 16, 4));
        int plane = Veh("test_plane");
        Run(sim, 10, Build(sim, ProductionCategory.Aircraft, plane));
        Simulation loaded = Roundtrip(sim);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Run(sim, 60); Run(loaded, 60);
        Assert.Equal(1, AliveOfType(loaded, plane));
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }
}
