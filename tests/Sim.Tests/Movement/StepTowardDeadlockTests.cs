// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Movement;

/// <summary>M13-05 (S18 SP-03, the StepToward half): two own tanks head-on in a one-cell corridor, each driven by StepToward toward
/// the other's side (what two harvesters do at a dock). Neither can pass. After 13 blocked waits StepToward used to answer 1 (keep
/// going) on every later call, so callers never learnt that the way was shut and the pair stood still for good (seen in AI games:
/// the income stopped, the AI went broke and sold its base).</summary>
public sealed class StepTowardDeadlockTests
{
    [Fact]
    public void HeadOnInACorridor_StepTowardReportsNoWay_SoTheCallerCanReact()
    {
        var map = MoveWorld.Grid(12, 3, (land, w) => { for (int x = 0; x < w; x++) { land[x] = LandType.Rock; land[2 * w + x] = LandType.Rock; } });
        var sim = MoveWorld.NewSim(map);
        EntityId a = sim.SpawnVehicle(0, 0, new CellCoord(4, 1));
        EntityId b = sim.SpawnVehicle(0, 0, new CellCoord(6, 1));
        // A busy mission with no handler of its own (like a harvester's, they are not idle): the sim neither shoves nor drives them,
        // the test is the only caller.
        sim.Units.Mission[a.Index] = MissionKind.Submerge;
        sim.Units.Mission[b.Index] = MissionKind.Submerge;
        int noWayA = 0, noWayB = 0, firstNoWay = -1;
        for (int t = 0; t < 800; t++)
        {
            int ra = MoveMission.StepToward(sim, sim.Units, ActorKind.Vehicle, a.Index, new CellCoord(10, 1));
            int rb = MoveMission.StepToward(sim, sim.Units, ActorKind.Vehicle, b.Index, new CellCoord(1, 1));
            if (ra >= MoveMission.WaitTicks) noWayA++;
            if (rb >= MoveMission.WaitTicks) noWayB++;
            if (firstNoWay < 0 && (ra >= MoveMission.WaitTicks || rb >= MoveMission.WaitTicks)) firstNoWay = t;
            sim.Step(MoveWorld.None);
        }
        Assert.True(noWayA + noWayB >= 2, $"no-way answers: A {noWayA}, B {noWayB}");   // and again after counting afresh
        Assert.InRange(firstNoWay, MoveMission.WaitTicks * MoveMission.MaxStuck, 799);   // measured 563: repaths and yields come first
    }

    /// <summary>M13-16: the goal cell itself is held for good (an attack-move target where another unit stands). The mover keeps
    /// waiting next to it, awake: a no-way answer made attack-movers sleep 10 ticks out of every 140 with their guns idle, and the
    /// campaign bots lost their hold fights (auth_01, dir_04).</summary>
    [Fact]
    public void GoalCellHeld_StepTowardKeepsWaitingAwake()
    {
        var map = MoveWorld.Grid(12, 3, (land, w) => { for (int x = 0; x < w; x++) { land[x] = LandType.Rock; land[2 * w + x] = LandType.Rock; } });
        var sim = MoveWorld.NewSim(map);
        EntityId a = sim.SpawnVehicle(0, 0, new CellCoord(2, 1));
        EntityId b = sim.SpawnVehicle(0, 0, new CellCoord(6, 1));
        sim.Units.Mission[a.Index] = MissionKind.Submerge;
        sim.Units.Mission[b.Index] = MissionKind.Submerge;
        int noWay = 0;
        for (int t = 0; t < 800; t++)
        {
            if (MoveMission.StepToward(sim, sim.Units, ActorKind.Vehicle, a.Index, new CellCoord(6, 1)) >= MoveMission.WaitTicks) noWay++;
            sim.Step(MoveWorld.None);
        }
        Assert.Equal(new CellCoord(5, 1), MoveWorld.CellOf(sim, a));
        Assert.Equal(0, noWay);
    }
}
