// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Pathing;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit (SIMDET-03): a group path result whose member dropped out while the request was still queued is never released;
/// it stays in the hashed and saved "paths" part.</summary>
public sealed class PathLeakAuditTests
{
    private static int Bytes(PathService s)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        s.Write(w);
        w.Flush();
        return (int)ms.Length;
    }

    // S18 audit: pins current behaviour of SIMDET-03; flip the assertion when fixed.
    [Fact]
    public void Audit_Service_PollOfAQueuedGroupEntry_ReleasesNothing_SoTheResultStays()
    {
        var s = new PathService(PathServiceTests.Flat(20, 20), PathServiceTests.Table());
        int empty = Bytes(s);
        int id = s.RequestGroup(new[] { new CellCoord(1, 1), new CellCoord(1, 3) }, new CellCoord(15, 2), Locomotion.Tracked);
        Assert.Equal(PathStatus.Pending, s.Poll(id, 0, out _));   // what MoveMission.DropRequest does for a group member: no effect while queued
        s.Process();
        Assert.Equal(PathStatus.Found, s.Poll(id, 1, out _));     // the other member takes its path
        Assert.True(Bytes(s) > empty + 14 * 8, "member 0's 14-cell path is still stored");
        for (int t = 0; t < 1000; t++) s.Process();
        Assert.True(Bytes(s) > empty + 14 * 8);                   // and nothing ever ages it out
    }

    /// <summary>Two tanks are ordered to one cell while the path service is paying off a debt, so their group request waits in the queue;
    /// on the next tick tank A is (or is not) re-ordered elsewhere. Returns the size of the paths part once everybody has arrived.</summary>
    private static (int Bytes, int Empty) GroupMove(bool reorderWhileQueued)
    {
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(128, 128), 2, Loaded.Sim, Loaded.RulesHash) { AllowDebug = true });
        EntityId hq = sim.PlaceBuilding(1, Bld("test_hq"), new CellCoord(110, 110));
        EntityId a = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        EntityId b = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 12));
        int empty = Bytes(sim.Paths);
        // One whole-map search (goal = a building cell, see SP-01) overdraws the 4000-expansion budget for several ticks.
        int probe = sim.Paths.Request(new CellCoord(5, 5), sim.Buildings.Cell[hq.Index], Locomotion.Tracked);
        sim.Step(None);
        Assert.NotEqual(PathStatus.Pending, sim.Paths.Poll(probe, 0, out _));   // solved and released
        Assert.Equal(empty, Bytes(sim.Paths));
        sim.Step(new[] { Move(sim, 0, ActorKind.Vehicle, a, 30, 11), Move(sim, 0, ActorKind.Vehicle, b, 30, 11) });
        Assert.True(sim.Units.PathIsGroup[a.Index] && sim.Units.PathId[a.Index] > 0);
        // Still queued: Poll answers Pending for a valid index only while the request is in the queue.
        Assert.Equal(PathStatus.Pending, sim.Paths.Poll(sim.Units.PathId[b.Index], sim.Units.PathIndex[b.Index], out _));
        if (reorderWhileQueued) sim.Step(new[] { Move(sim, 0, ActorKind.Vehicle, a, 10, 30) });
        for (int t = 0; t < 1500; t++) sim.Step(None);
        Assert.Equal(new CellCoord(reorderWhileQueued ? 10 : 30, reorderWhileQueued ? 30 : 11), sim.Units.Position[a.Index].ToCell());
        Assert.Equal(0, sim.Units.PathId[a.Index]);
        Assert.Equal(0, sim.Units.PathId[b.Index]);
        return (Bytes(sim.Paths), empty);
    }

    [Fact]
    public void Control_GroupMove_LeavesThePathsPartEmpty()
    {
        var (bytes, empty) = GroupMove(reorderWhileQueued: false);
        Assert.Equal(empty, bytes);
    }

    // S18 audit: pins current behaviour of SIMDET-03; flip the assertion when fixed.
    [Fact]
    public void Audit_Sim_ReorderingAGroupMemberWhileItsRequestIsQueued_LeaksItsResultForGood()
    {
        var (bytes, empty) = GroupMove(reorderWhileQueued: true);
        Assert.True(bytes > empty, $"paths part is {bytes} bytes against {empty} when empty");
    }

    [Fact(Skip = "S18 SIMDET-03: awaiting owner decision")]
    public void Desired_Sim_ReorderingAGroupMember_LeavesNothingBehind()
    {
        var (bytes, empty) = GroupMove(reorderWhileQueued: true);
        Assert.Equal(empty, bytes);
    }
}
