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
        "_restricted", "_granted", "_timers", "_aiHooks", "_arrivals",
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
    public void Queued_reinforcements_write_a_version_three_tail_that_changes_the_bytes_and_leaves_the_old_prefix_alone()
    {
        const string Lua = "function OnInit() Reinforce.Delayed(500, 1, { 'auth_jeep' }, { x = 39, y = 15 }, { x = 20, y = 15 }) end";
        var (sim, host, _) = Rts.Scripting.Tests.M8_04.World.Build(Lua);
        TestData.Run(sim, 2);
        byte[] withQueue = Bytes(host.State);
        Assert.Equal(3, BitConverter.ToInt32(withQueue, 0));

        var (sim2, host2, _) = Rts.Scripting.Tests.M8_04.World.Build("function OnInit() end");
        TestData.Run(sim2, 2);
        byte[] none = Bytes(host2.State);
        Assert.Equal(1, BitConverter.ToInt32(none, 0));   // no queue: the M3-M7 layout, byte for byte

        // The queue is hashed: another delay gives other bytes.
        var (sim3, host3, _) = Rts.Scripting.Tests.M8_04.World.Build(Lua.Replace("500", "501"));
        TestData.Run(sim3, 2);
        Assert.False(withQueue.AsSpan().SequenceEqual(Bytes(host3.State)));
        // After the arrival the part is back to the version-1 layout.
        TestData.Run(sim, 600);
        Assert.Equal(0, host.PendingArrivalCount);
        Assert.Equal(1, BitConverter.ToInt32(Bytes(host.State), 0));
    }
}
