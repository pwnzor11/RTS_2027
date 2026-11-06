// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting.Tests.M8_03;

/// <summary>Objective.AddHidden / Reveal / Status, Mission.AllPrimaryDone / AnyPrimaryFailed, Tech.GrantMany / List, Mission.Difficulty.</summary>
public sealed class ObjectiveTests
{
    [Fact]
    public void Hidden_objectives_stay_off_the_visible_list_until_revealed()
    {
        var (sim, host) = TestData.Tiny("function OnInit() Objective.Add('a', 'A', true); Objective.AddHidden('b', 'B', false); state.s = Objective.Status('b') end");
        TestData.Run(sim, 2);
        Assert.Equal(2, host.Objectives.Count);
        Assert.Equal(new[] { "a" }, host.VisibleObjectives.Select(o => o.Id).ToArray());
        Assert.Equal("active", TestAccess.StateValue(host, "s"));
        Assert.Empty(host.Errors);

        var (sim2, host2) = TestData.Tiny("function OnInit() Objective.AddHidden('b', 'B', false); Trigger.AfterDelay(2, Rev) end\nfunction Rev() Objective.Reveal('b'); Objective.Fail('b'); state.s = Objective.Status('b') end");
        TestData.Run(sim2, 1);
        Assert.Empty(host2.VisibleObjectives);
        TestData.Run(sim2, 4);
        Assert.Single(host2.VisibleObjectives);
        Assert.Equal("failed", TestAccess.StateValue(host2, "s"));
    }

    [Fact]
    public void Add_hidden_rejects_a_used_id_and_a_non_boolean()
    {
        var (sim, host) = TestData.Tiny("function OnInit() Objective.Add('a', 'A', true); Objective.AddHidden('a', 'B', true) end");
        TestData.Run(sim, 1);
        Assert.Contains(host.Errors, e => e.Contains("Objective.AddHidden") && e.Contains("already exists"));
        var (sim2, host2) = TestData.Tiny("function OnInit() Objective.AddHidden('a', 'A', 1) end");
        TestData.Run(sim2, 1);
        Assert.Contains(host2.Errors, e => e.Contains("Objective.AddHidden"));
    }

    [Fact]
    public void Primary_helpers_ignore_secondary_objectives_and_need_at_least_one_primary()
    {
        const string Lua = @"function OnInit()
  state.none = Mission.AllPrimaryDone()
  Objective.Add('p1', 'P1', true); Objective.Add('p2', 'P2', true); Objective.Add('s1', 'S1', false)
  Trigger.AfterDelay(1, Step1); Trigger.AfterDelay(2, Step2); Trigger.AfterDelay(3, Step3)
end
function Step1() Objective.Complete('p1'); state.a = Mission.AllPrimaryDone() end
function Step2() Objective.Fail('s1'); state.b = Mission.AnyPrimaryFailed(); Objective.Complete('p2'); state.c = Mission.AllPrimaryDone() end
function Step3() Objective.Fail('p2'); state.d = Mission.AnyPrimaryFailed(); state.e = Mission.AllPrimaryDone() end";
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 5);
        Assert.Empty(host.Errors);
        Assert.Equal("false", TestAccess.StateValue(host, "none"));
        Assert.Equal("false", TestAccess.StateValue(host, "a"));
        Assert.Equal("false", TestAccess.StateValue(host, "b"));
        Assert.Equal("true", TestAccess.StateValue(host, "c"));
        Assert.Equal("true", TestAccess.StateValue(host, "d"));
        Assert.Equal("false", TestAccess.StateValue(host, "e"));
    }

    [Fact]
    public void Tech_grant_many_and_list_are_sorted_and_per_player()
    {
        const string Lua = @"function OnInit()
  Tech.GrantMany(0, { 'auth_tank', 'auth_rifleman', 'auth_tank' })
  local l = Tech.List(0)
  state.l = l
  state.n1 = #Tech.List(1)
  state.has = Tech.Has(0, 'auth_tank')
end";
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        Assert.Equal("auth_rifleman,auth_tank", TestAccess.StateValue(host, "l"));
        Assert.Equal("0", TestAccess.StateValue(host, "n1"));
        Assert.Equal("true", TestAccess.StateValue(host, "has"));
    }

    [Fact]
    public void Difficulty_defaults_to_normal_and_follows_the_host_setter()
    {
        var (sim, host) = TestData.Tiny("function OnInit() state.d = Mission.Difficulty() end");
        TestData.Run(sim, 1);
        Assert.Equal("3", TestAccess.StateValue(host, "d"));
        var (sim2, host2) = TestData.Tiny("function OnInit() state.d = Mission.Difficulty() end");
        host2.SetDifficulty(1);
        TestData.Run(sim2, 1);
        Assert.Equal("1", TestAccess.StateValue(host2, "d"));
    }
}
