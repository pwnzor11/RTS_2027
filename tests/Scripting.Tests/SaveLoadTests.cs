// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;

namespace Rts.Scripting.Tests;

/// <summary>The mission state part saves, loads and hashes, and the real pilot mission is deterministic.</summary>
public sealed class SaveLoadTests
{
    private static Simulation Reload(Simulation sim, MissionSession template, out MissionHost host)
    {
        using var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        host = new MissionHost(template.Def, template.ScriptSource);
        return Simulation.Load(ms, MissionSession.MakeSetup(template.Def, template.Map, template.Rules, host));
    }

    [Fact]
    public void Mission_state_round_trips_with_identical_hashes_and_the_game_continues_identically()
    {
        MissionSession a = MissionSession.Create(TestData.DataDir, "auth_01");
        TestData.Run(a.Sim, 700);   // triggers, tags, messages and script state exist by now
        Assert.NotEmpty(a.Host.Messages);
        Assert.NotEmpty(a.Host.Objectives);

        Simulation b = Reload(a.Sim, a, out MissionHost hostB);
        Assert.Equal(a.Sim.Tick, b.Tick);
        Assert.Equal(a.Sim.ComputeStateHash(), b.ComputeStateHash());
        Assert.Equal(a.Host.TriggerCount, hostB.TriggerCount);
        Assert.Equal(a.Host.Objectives, hostB.Objectives);
        Assert.Equal(a.Host.Messages, hostB.Messages);

        TestData.Run(a.Sim, 2500);
        TestData.Run(b, 2500);
        Assert.Equal(a.Sim.ComputeStateHash(), b.ComputeStateHash());
        Assert.Equal(a.Host.Errors, hostB.Errors);
        Assert.Empty(hostB.Errors);
    }

    [Fact]
    public void Script_state_table_is_part_of_the_hash()
    {
        var (s1, _) = TestData.Tiny("function OnInit() state.x = 1 end");
        var (s2, _) = TestData.Tiny("function OnInit() state.x = 1 end");
        var (s3, _) = TestData.Tiny("function OnInit() state.x = 2 end");
        TestData.Run(s1, 3); TestData.Run(s2, 3); TestData.Run(s3, 3);
        Assert.Equal(s1.ComputeStateHash(), s2.ComputeStateHash());
        Assert.NotEqual(s1.ComputeStateHash(), s3.ComputeStateHash());
        Assert.Contains(s1.ComputeSubHashes(), p => p.Part == "mission");
    }

    [Fact]
    public void Loading_a_save_with_a_different_script_is_refused()
    {
        MissionSession a = MissionSession.Create(TestData.DataDir, "auth_01");
        TestData.Run(a.Sim, 10);
        using var ms = new MemoryStream();
        a.Sim.Save(ms);
        ms.Position = 0;
        var other = new MissionHost(a.Def, a.ScriptSource + "\n-- edited");
        Assert.Throws<InvalidDataException>(() => Simulation.Load(ms, MissionSession.MakeSetup(a.Def, a.Map, a.Rules, other)));
    }

    [Fact]
    public void Two_runs_of_the_pilot_mission_produce_identical_hashes_with_no_script_errors()
    {
        MissionSession a = MissionSession.Create(TestData.DataDir, "auth_01");
        MissionSession b = MissionSession.Create(TestData.DataDir, "auth_01");
        for (int i = 0; i < 4000; i++)
        {
            a.Sim.Step(TestData.NoCommands);
            b.Sim.Step(TestData.NoCommands);
            if (i % 250 == 0) Assert.Equal(a.Sim.ComputeStateHash(), b.Sim.ComputeStateHash());
        }

        Assert.Equal(a.Sim.ComputeStateHash(), b.Sim.ComputeStateHash());
        Assert.Empty(a.Host.Errors);
        Assert.Empty(b.Host.Errors);
        Assert.Equal(a.Host.Messages, b.Host.Messages);
    }
}
