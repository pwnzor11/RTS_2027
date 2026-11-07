// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests.M11_05;

/// <summary>M11-05 (G1): an objective's optional location: API, resolution, hashing and save/load.</summary>
public sealed class ObjectiveTargetTests
{
    private const string Head = "function OnInit() Actor.Create('auth_rifleman', 0, {x=20, y=22}, 't1'); Objective.Add('o1', 'x', true); ";

    private static byte[] Bytes(ISimStatePart part)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) part.Write(w);
        return ms.ToArray();
    }

    private static MissionObjective O1(MissionHost h) => h.Objectives.First(o => o.Id == "o1");

    [Fact]
    public void Cell_zone_and_actor_targets_resolve_to_cells()
    {
        var (sim, host) = TestData.Tiny(Head + "Objective.AddHidden('o2', 'y', false); Objective.AddHidden('o3', 'z', false); Objective.SetTarget('o1', {x=5, y=6}); Objective.SetTarget('o2', 'z'); Objective.SetTarget('o3', 't1') end");
        TestData.Run(sim, 2);
        Assert.True(host.TryObjectiveCell(O1(host), out CellCoord c));
        Assert.Equal(new CellCoord(5, 6), c);
        Assert.True(host.TryObjectiveCell(host.Objectives[1], out c));
        Assert.Equal(new CellCoord(12, 12), c);   // middle of zone z (10,10)-(14,14)
        Assert.True(host.TryObjectiveCell(host.Objectives[2], out c));
        Assert.Equal(new CellCoord(20, 22), c);
    }

    [Fact]
    public void An_actor_target_follows_the_actor_and_ends_when_it_dies()
    {
        var (sim, host) = TestData.Tiny("function OnInit() Actor.Create('auth_rifleman', 1, {x=40, y=40}, 'v'); Objective.Add('o1', 'x', true); Objective.SetTarget('o1', 'v') end");
        TestData.Run(sim, 2);
        Assert.True(host.TryObjectiveCell(O1(host), out CellCoord c));
        Assert.Equal(new CellCoord(40, 40), c);
        sim.Infantry.Health[sim.Infantry.Slots.IdOf(1).Index] = 0;   // test-only direct kill, as TriggerTests does
        TestData.Run(sim, 3);
        Assert.False(host.TryObjectiveCell(O1(host), out _));
    }

    [Fact]
    public void No_target_means_no_cell_and_clear_removes_it()
    {
        var (sim, host) = TestData.Tiny(Head + "end function Later() Objective.ClearTarget('o1') end");
        TestData.Run(sim, 2);
        Assert.False(O1(host).Target.IsSet);
        Assert.False(host.TryObjectiveCell(O1(host), out _));
    }

    [Fact]
    public void A_target_writes_a_version_five_tail_changes_the_hash_and_survives_a_save()
    {
        string lua = Head + "Objective.SetTarget('o1', {x=5, y=6}) end";
        var (simPlain, hostPlain) = TestData.Tiny(Head + "end");
        TestData.Run(simPlain, 2);
        Assert.Equal(1, BitConverter.ToInt32(Bytes(hostPlain.State), 0));   // no target: the old layout, byte for byte

        var (sim, host) = TestData.Tiny(lua);
        TestData.Run(sim, 2);
        Assert.Equal(5, BitConverter.ToInt32(Bytes(host.State), 0));
        Assert.NotEqual(simPlain.ComputeStateHash(), sim.ComputeStateHash());

        var (simOther, _) = TestData.Tiny(Head + "Objective.SetTarget('o1', {x=5, y=7}) end");
        TestData.Run(simOther, 2);
        Assert.NotEqual(sim.ComputeStateHash(), simOther.ComputeStateHash());

        var loadedHost = new MissionHost(host.Def, lua);
        Simulation loaded = M8_03.SaveLoad.Reload(sim, host.Def, lua, loadedHost);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(new ObjectiveTarget(ObjectiveTargetKind.Cell, "", 5, 6), O1(loadedHost).Target);
        TestData.Run(sim, 50);
        TestData.Run(loaded, 50);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }

    [Fact]
    public void Zone_and_tag_targets_survive_a_save_too()
    {
        string lua = Head + "Objective.AddHidden('o2', 'y', false); Objective.SetTarget('o1', 'z'); Objective.SetTarget('o2', 't1') end";
        var (sim, host) = TestData.Tiny(lua);
        TestData.Run(sim, 2);
        var loadedHost = new MissionHost(host.Def, lua);
        M8_03.SaveLoad.Reload(sim, host.Def, lua, loadedHost);
        Assert.Equal(new ObjectiveTarget(ObjectiveTargetKind.Zone, "z"), O1(loadedHost).Target);
        Assert.Equal(new ObjectiveTarget(ObjectiveTargetKind.Actor, "t1"), loadedHost.Objectives[1].Target);
    }
}

/// <summary>The tutorial is the working example of objective targets (M11-05).</summary>
public sealed class TutorialTargetTests
{
    [Fact]
    public void Every_tutorial_objective_has_a_target_that_resolves_to_a_cell()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "tut_01");
        TestData.Run(m.Sim, 2);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(7, m.Host.Objectives.Count);
        foreach (MissionObjective o in m.Host.Objectives)
        {
            Assert.True(o.Target.IsSet, o.Id);
            Assert.True(m.Host.TryObjectiveCell(o, out _), o.Id);
        }

        Assert.True(m.Host.TryObjectiveCell(m.Host.Objectives[0], out CellCoord rally));
        Assert.Equal(new CellCoord(12, 5), rally);   // middle of the "rally" zone (11,4)-(13,6)
        Assert.Equal(ObjectiveTargetKind.Cell, m.Host.Objectives[5].Target.Kind);
    }
}
