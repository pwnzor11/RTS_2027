// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests.M11_30;

/// <summary>M11-30: a tag target keeps the last cell its actor was seen on, and that cell is saved and hashed like the rest of the target.</summary>
public sealed class ActorTargetMemoryTests
{
    private const string Lua = "function OnInit() Actor.Create('auth_rifleman', 1, {x=40, y=40}, 'v'); Objective.Add('o1', 'x', true); Objective.SetTarget('o1', 'v') end";

    private static MissionObjective O1(MissionHost h) => h.Objectives.First(o => o.Id == "o1");

    [Fact]
    public void The_marker_stays_on_the_last_cell_after_the_tagged_actor_dies()
    {
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 2);
        Assert.True(host.TryObjectiveCell(O1(host), out CellCoord c));
        Assert.Equal(new CellCoord(40, 40), c);
        sim.Infantry.Health[sim.Infantry.Slots.IdOf(1).Index] = 0;
        TestData.Run(sim, 3);
        Assert.True(host.TryObjectiveCell(O1(host), out CellCoord last));
        Assert.Equal(new CellCoord(40, 40), last);
    }

    [Fact]
    public void The_remembered_cell_survives_a_save_and_the_hash_agrees_afterwards()
    {
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 2);
        sim.Infantry.Health[sim.Infantry.Slots.IdOf(1).Index] = 0;
        TestData.Run(sim, 3);

        var loadedHost = new MissionHost(host.Def, Lua);
        Simulation loaded = M8_03.SaveLoad.Reload(sim, host.Def, Lua, loadedHost);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(new ObjectiveTarget(ObjectiveTargetKind.Actor, "v", 40, 40), O1(loadedHost).Target);
        TestData.Run(sim, 20);
        TestData.Run(loaded, 20);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }
}
