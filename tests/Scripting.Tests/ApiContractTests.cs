// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;

namespace Rts.Scripting.Tests;

/// <summary>M8-02: every function of docs/missions/api.md is registered, validates its arguments, and behaves as its status says.</summary>
public sealed class ApiContractTests
{
    // (function, good arguments, bad arguments or null). Lua source text; 'z' is the test zone, 't1' a player-0 actor, 'o1' an objective,
    // player 1 is the AI, CbA a global function.
    private static readonly (string Fn, string Good, string? Bad)[] Cases =
    {
        ("Trigger.OnLeave", "'z', 0, 'CbA'", "'nozone', 0, 'CbA'"),
        ("Trigger.OnHold", "'z', 0, 10, 'CbA'", "'z', 0, 0, 'CbA'"),
        ("Trigger.OnSeen", "'t1', 1, 'CbA'", "'nope', 1, 'CbA'"),
        ("Trigger.OnIdle", "'t1', 20, 'CbA'", "'t1', 0, 'CbA'"),
        ("Objective.AddHidden", "'h1', 'Text', true", "'h1', 'Text', 'yes'"),
        ("Objective.Reveal", "'o1'", "'zz'"),
        ("Objective.Status", "'o1'", "'zz'"),
        ("Mission.Timer", "'tm', 100, 'CbA'", "'tm', 100, 'NoSuchFn'"),
        ("Mission.Timer", "'tm', 100, 'CbA', 'Label'", "'tm', 1.5, 'CbA'"),
        ("Mission.TimerCancel", "'tm'", "5"),
        ("Mission.TimerLeft", "'tm'", "5"),
        ("Mission.AllPrimaryDone", "", null),
        ("Mission.AnyPrimaryFailed", "", null),
        ("Mission.Difficulty", "", null),
        ("Tech.GrantMany", "0, {'auth_wall'}", "0, {'no_such_type'}"),
        ("Tech.List", "0", "9"),
        ("Reinforce.Zone", "0, {'auth_rifleman'}, 'z', {x=30, y=30}", "0, {'auth_rifleman'}, 'nozone', {x=30, y=30}"),
        ("Reinforce.Transport", "0, {'auth_rifleman'}, {x=0, y=20}, {x=20, y=20}", "0, {'auth_rifleman'}, {x=5, y=20}, {x=20, y=20}"),
        ("Reinforce.Delayed", "50, 0, {'auth_rifleman'}, {x=0, y=20}, {x=20, y=20}", "0, 0, {'auth_rifleman'}, {x=0, y=20}, {x=20, y=20}"),
        ("Reinforce.Naval", "0, {'exp_landing_craft'}, {x=0, y=20}, {x=20, y=20}", "0, {}, {x=0, y=20}, {x=20, y=20}"),
        ("Reinforce.Rig", "0, {x=0, y=20}, {x=20, y=20}", "0, {x=5, y=20}, {x=20, y=20}"),
        ("Actor.CreateGroup", "{'auth_rifleman'}, 0, {x=30, y=30}, {formation='line', rank=1}", "{'auth_rifleman'}, 0, {x=30, y=30}, {formation='blob'}"),
        ("Actor.CreateGroup", "{'auth_rifleman'}, 0, {x=30, y=30}", "{'auth_rifleman'}, 0, {x=30, y=30}, {rank=9}"),
        ("Dialogue.Say", "'strand', 'strand_neutral', 'm1.line1'", "'strand', 'strand_neutral', 'has space'"),
        ("Dialogue.Say", "'strand', 'strand_neutral', 'm1.line1', 'voice/strand/m1.line1.ogg'", "'strand', 'strand_neutral', 'm1.line1', 'sounds/x.wav'"),
        ("Dialogue.Clear", "", null),
        ("Camera.Focus", "'z', 50", "'z', 0"),
        ("Camera.Focus", "'t1', 50", "'nope', 50"),
        ("Media.MessageKey", "'m1.key'", "5"),
        ("AI.Attack", "1, 'z'", "0, 'z'"),
        ("AI.Defend", "1, 'z'", "1, 'nozone'"),
        ("AI.SetTier", "1, 3", "1, 6"),
        ("AI.Wave", "1, 'w1', 'z'", "1, 'w 1', 'z'"),
        ("AI.Clear", "1", "0"),
    };

    /// <summary>A script with one callback per case (Case_N) so that each call's error is reported separately under its own name.</summary>
    private static string Script(bool good)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("function CbA() end");
        for (int i = 0; i < Cases.Length; i++)
        {
            string? args = good ? Cases[i].Good : Cases[i].Bad;
            if (args != null) sb.AppendLine($"function Case_{i}() {Cases[i].Fn}({args}) end");
        }

        sb.AppendLine("function OnInit()");
        sb.AppendLine("  Actor.Create('auth_rifleman', 0, {x=12, y=12}, 't1')");
        sb.AppendLine("  Objective.Add('o1', 'x', true)");
        for (int i = 0; i < Cases.Length; i++)
            if ((good ? Cases[i].Good : Cases[i].Bad) != null) sb.AppendLine($"  Trigger.AfterDelay(1, Case_{i})");
        sb.AppendLine("end");
        return sb.ToString();
    }

    private static string? ErrorOf(MissionHost host, int i) =>
        host.Errors.FirstOrDefault(e => e.Contains(": Case_" + i + ": ", StringComparison.Ordinal));

    [Fact]
    public void Every_listed_function_is_registered_and_every_registered_function_is_listed()
    {
        var (_, host) = TestData.Tiny("function OnInit() end");
        var script = (Script)typeof(MissionHost).GetField("_script", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(host)!;
        var registered = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var g in script.Globals.Pairs)
        {
            if (g.Key.Type != DataType.String || g.Value.Type != DataType.Table) continue;
            foreach (var f in g.Value.Table.Pairs)
                if (f.Value.Type == DataType.ClrFunction && f.Key.Type == DataType.String && g.Key.String is not ("_G" or "string" or "math" or "table" or "bit32"))
                    registered.Add(g.Key.String + "." + f.Key.String);
        }

        var listed = new SortedSet<string>(MissionApiContract.Entries.Select(e => e.Name), StringComparer.Ordinal);
        Assert.Equal(MissionApiContract.Entries.Count, listed.Count);
        Assert.Empty(listed.Except(registered));
        Assert.Empty(registered.Except(listed));
    }

    [Fact]
    public void Good_and_bad_arguments_behave_as_the_contract_says()
    {
        var statusOf = MissionApiContract.Entries.ToDictionary(e => e.Name);

        var (goodSim, good) = TestData.Tiny(Script(good: true));
        TestData.Run(goodSim, 5);
        for (int i = 0; i < Cases.Length; i++)
        {
            ApiEntry entry = statusOf[Cases[i].Fn];
            string? err = ErrorOf(good, i);
            // M8-04: the pilot map has no sea, so a ship has no water to appear on; that is a world problem, not an argument problem.
            if (Cases[i].Fn == "Reinforce.Naval" && err != null && err.Contains("no free cell near")) err = null;
            if (entry.Status is ApiStatus.NoOp or ApiStatus.Implemented) Assert.True(err == null, Cases[i].Fn + " should accept good arguments: " + err);
            else
            {
                Assert.NotNull(err);
                Assert.Contains("not implemented (" + entry.Package + ")", err);
            }
        }

        var (badSim, bad) = TestData.Tiny(Script(good: false));
        TestData.Run(badSim, 5);
        for (int i = 0; i < Cases.Length; i++)
        {
            if (Cases[i].Bad == null) continue;
            string? err = ErrorOf(bad, i);
            Assert.True(err != null, Cases[i].Fn + "(" + Cases[i].Bad + ") should be rejected");
            Assert.DoesNotContain("not implemented", err);
            Assert.Contains(Cases[i].Fn, err);   // the message names the function
        }

        var covered = Cases.Select(c => c.Fn).ToHashSet();
        foreach (ApiEntry e in MissionApiContract.Entries.Where(e => e.Status != ApiStatus.Exists))
            Assert.Contains(e.Name, covered);
    }

    [Fact]
    public void Ai_hooks_only_set_hashed_flags_that_the_ai_can_read()
    {
        var (s1, h1) = TestData.Tiny("function OnInit() AI.Attack(1, 'z'); AI.SetTier(1, 4); AI.Wave(1, 'w1', 'z') end");
        var (s2, _) = TestData.Tiny("function OnInit() end");
        TestData.Run(s1, 2);
        TestData.Run(s2, 2);
        Assert.Empty(h1.Errors);
        AiHookState hook = h1.AiHookOf(1);
        Assert.Equal(new AiHookState("z", "", 4, 1, "w1", "z"), hook);
        Assert.Equal(AiHookState.None, h1.AiHookOf(0));
        Assert.NotEqual(s2.ComputeStateHash(), s1.ComputeStateHash());
    }

    [Fact]
    public void Pilot_functions_are_all_still_there()
    {
        foreach (ApiEntry e in MissionApiContract.Entries.Where(e => e.Status == ApiStatus.Exists))
        {
            var (sim, host) = TestData.Tiny("function OnInit() state.t = type(" + e.Name + ") end");
            TestData.Run(sim, 1);
            Assert.Equal("function", TestAccess.StateValue(host, "t"));
        }
    }
}
