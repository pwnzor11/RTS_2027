// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Reflection;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>
/// M8-02: the "mission" part carries timers, hidden objectives, trigger extras and AI hook flags in a version-2 tail that is written only
/// when one of them is set (so schema-1 missions keep their M3-M7 bytes and hashes). This is the hash-coverage list for the host's fields
/// (Sim.Tests/HashCoverageTests cannot see this assembly).
/// </summary>
public sealed class MissionStateContractTests
{
    /// <summary>Instance fields of MissionHost that are in the part (written by WriteState).</summary>
    private static readonly string[] Hashed =
    {
        "_initDone", "_halted", "_outcome", "_rng", "_counter", "_messageSeq", "_tags", "_triggers", "_objectives", "_messages",
        "_restricted", "_granted", "_timers", "_aiHooks", "_difficulty",
    };

    /// <summary>Instance fields that are deliberately not state, each with the reason.</summary>
    private static readonly Dictionary<string, string> NotHashed = new()
    {
        ["_source"] = "immutable script text; its hash is SourceHash, which is written",
        ["_script"] = "the interpreter; its only saved data is the state table, written by WriteState",
        ["_part"] = "the part adapter itself",
        ["_budget"] = "instruction counter, reset before every callback",
        ["_sim"] = "back-reference set in OnSetup",
        ["_callbackDepth"] = "non-zero only inside a callback; zero whenever a hash or save is taken",
        ["_callbacksThisTick"] = "per-tick scratch, reset at the start of OnTick",
        ["_errors"] = "diagnostics (deterministic but not state)",
        ["_printLog"] = "diagnostics",
        ["_fn"] = "name of the API function being called, for error text",
        ["_outputSeq"] = "M8-05 sequence number for game-layer outputs only; never saved, hashed or read by the sim",
        ["_dialogue"] = "M8-05 output for the game layer (plan 3.7): never read by the sim",
        ["_cameraHint"] = "M8-05 output for the game layer (plan 3.7): never read by the sim",
    };

    private static byte[] Bytes(ISimStatePart part)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) part.Write(w);
        return ms.ToArray();
    }

    [Fact]
    public void Every_instance_field_of_the_host_is_either_hashed_or_justified()
    {
        var fields = typeof(MissionHost).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(f => !f.Name.StartsWith('<')).Select(f => f.Name).ToHashSet();
        var known = Hashed.Concat(NotHashed.Keys).ToHashSet();
        Assert.Empty(fields.Except(known));   // a new field must be added to one of the two lists
        Assert.Empty(known.Except(fields));   // and a stale entry must be removed
    }

    [Fact]
    public void A_game_without_v2_features_writes_the_version_one_layout()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_01");
        TestData.Run(m.Sim, 700);
        Assert.Equal(1, BitConverter.ToInt32(Bytes(m.Host.State), 0));
    }

    [Fact]
    public void Timers_hidden_objectives_trigger_extras_and_ai_flags_each_change_the_bytes_and_survive_a_save()
    {
        var (sim, host) = TestData.Tiny("function OnInit() Objective.Add('o1', 'x', true); Trigger.Every(50, Cb) end\nfunction Cb() end");
        TestData.Run(sim, 2);
        byte[] baseline = Bytes(host.State);
        Assert.Equal(1, BitConverter.ToInt32(baseline, 0));

        void Check(Action set, Action reset)
        {
            set();
            byte[] changed = Bytes(host.State);
            Assert.Equal(2, BitConverter.ToInt32(changed, 0));
            Assert.False(baseline.AsSpan().SequenceEqual(changed));
            reset();
            Assert.True(baseline.AsSpan().SequenceEqual(Bytes(host.State)));
        }

        Check(() => host.TimerStore.Add(new MissionTimer("t", "Label", 500, "Cb", false)), () => host.TimerStore.Clear());
        Check(() => host.TimerStore.Add(new MissionTimer("t", "", 500, "Cb", true)), () => host.TimerStore.Clear());
        MissionObjective o = host.ObjectiveStore[0];
        Check(() => host.ObjectiveStore[0] = o with { Hidden = true }, () => host.ObjectiveStore[0] = o);
        Check(() => host.TriggerStore[0].B = 7, () => host.TriggerStore[0].B = 0);
        Check(() => host.TriggerStore[0].Counter = 3, () => host.TriggerStore[0].Counter = 0);

        // Round trip through a real save with everything set (via the Lua flags where they exist).
        const string Lua = "function OnInit() Objective.Add('o1', 'x', true); Trigger.Every(50, Cb); AI.Wave(1, 'w1', 'z'); AI.SetTier(1, 2) end\nfunction Cb() end";
        var (sim2, host2) = TestData.Tiny(Lua);
        TestData.Run(sim2, 2);
        host2.TimerStore.Add(new MissionTimer("t", "Label", 500, "Cb", false));
        host2.ObjectiveStore[0] = o with { Hidden = true };
        host2.TriggerStore[0].B = 7; host2.TriggerStore[0].Counter = 3;

        using var ms = new MemoryStream();
        sim2.Save(ms);
        ms.Position = 0;
        var loadedHost = new MissionHost(host2.Def, Lua);
        var loaded = Simulation.Load(ms, MissionSession.MakeSetup(host2.Def, Rts.Data.MapFile.Load(Path.Combine(TestData.DataDir, "maps", host2.Def.MapFile)),
            Rts.Data.RulesLoader.Load(Path.Combine(TestData.DataDir, "rules")), loadedHost));
        Assert.Equal(sim2.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(host2.Timers, loadedHost.Timers);
        Assert.Equal(host2.Objectives, loadedHost.Objectives);
        Assert.True(loadedHost.Objectives[0].Hidden);
        Assert.Empty(loadedHost.VisibleObjectives);
        Assert.Equal(7, loadedHost.TriggerStore[0].B);
        Assert.Equal(3, loadedHost.TriggerStore[0].Counter);
        Assert.Equal(host2.AiHookOf(1), loadedHost.AiHookOf(1));
        Assert.Equal(1, loadedHost.AiHookOf(1).WaveSeq);
    }

    [Fact]
    public void Difficulty_tier_is_written_only_when_set_and_survives_a_save()
    {
        const string Lua = "function OnInit() state.d = Mission.Difficulty() end";
        var (simA, hostA) = TestData.Tiny(Lua);
        TestData.Run(simA, 2);
        Assert.Equal(1, BitConverter.ToInt32(Bytes(hostA.State), 0));   // default tier: old layout
        Assert.Equal("3", TestAccess.StateValue(hostA, "d"));

        var (sim, host) = TestData.Tiny(Lua);
        host.SetDifficulty(5);
        TestData.Run(sim, 2);
        Assert.Equal(3, BitConverter.ToInt32(Bytes(host.State), 0));
        Assert.Equal("5", TestAccess.StateValue(host, "d"));
        Assert.NotEqual(simA.ComputeStateHash(), sim.ComputeStateHash());

        var loadedHost = new MissionHost(host.Def, Lua);
        var loaded = M8_03.SaveLoad.Reload(sim, host.Def, Lua, loadedHost);
        Assert.Equal(5, loadedHost.DifficultyTier);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Throws<ArgumentOutOfRangeException>(() => host.SetDifficulty(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => host.SetDifficulty(6));
    }
}
