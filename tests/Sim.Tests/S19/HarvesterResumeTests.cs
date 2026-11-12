// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Tests.Economy;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.S19;

/// <summary>M13-11 (SIM-RULES-13, D-205): an idle harvester resumes after a refinery is rebuilt, unless the player parked it with Stop.</summary>
public sealed class HarvesterResumeTests
{
    private static (Simulation Sim, EntityId H) GaveUp(bool playerStop)
    {
        var (sim, h) = EconomyFixture.Basic(silo: true);
        EconomyFixture.RunUntil(sim, () => sim.Economy.Cargo[h.Index] > 0);
        EntityId refinery = sim.Map.BuildingAt(EconomyFixture.Refinery);
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Debug, refinery, default, default, (int)DebugOp.Kill, (int)ActorKind.Building) });
        EconomyFixture.RunUntil(sim, () => sim.Units.Mission[h.Index] == MissionKind.Guard);
        if (playerStop) EconomyFixture.Run(sim, 5);
        if (playerStop) sim.Step(new[] { new Command(sim.Tick, 0, 2, CommandKind.Stop, h, default, default, (int)ActorKind.Vehicle) });
        EconomyFixture.Run(sim, 5);
        return (sim, h);
    }

    private static void Rebuild(Simulation sim)
    {
        sim.Step(new[] { new Command(sim.Tick, 0, 3, CommandKind.Place, default, default, new CellCoord(20, 20), Bld("test_refinery")) });
        Assert.False(sim.Map.BuildingAt(new CellCoord(20, 20)).IsNone);
    }

    [Fact]
    public void GaveUpHarvester_ResumesWhenARefineryReturns()
    {
        var (sim, h) = GaveUp(playerStop: false);
        Assert.True(sim.Economy.NoRefinery[h.Index] == 1, $"m={sim.Units.Mission[h.Index]} n={sim.Units.NextMission[h.Index]}");
        Rebuild(sim);
        EconomyFixture.Run(sim, 5);
        Assert.Equal(0, sim.Economy.NoRefinery[h.Index]);
        Assert.NotEqual(MissionKind.Guard, sim.Units.Mission[h.Index]);
    }

    [Fact]
    public void HarvesterParkedByStop_StaysParked()
    {
        var (sim, h) = GaveUp(playerStop: true);
        Rebuild(sim);
        EconomyFixture.Run(sim, 300);
        Assert.Equal(0, sim.Economy.NoRefinery[h.Index]);
        Assert.Equal(MissionKind.Guard, sim.Units.Mission[h.Index]);
    }
}
