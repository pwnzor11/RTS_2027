// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting.Tests.M8_03;

/// <summary>Mission.Timer / TimerCancel / TimerLeft.</summary>
public sealed class TimerTests
{
    private const string Prelude = "function Mark(name) state[name] = Mission.Tick() end\n";

    [Fact]
    public void A_timer_fires_exactly_once_n_ticks_after_it_was_made_and_is_listed_until_then()
    {
        var (sim, host) = TestData.Tiny(Prelude + "function Fire() Mark('t') end\nfunction OnInit() Mission.Timer('t1', 5, Fire, 'Relief') end");
        TestData.Run(sim, 3);
        MissionTimer t = Assert.Single(host.Timers);
        Assert.Equal(("t1", "Relief", 5), (t.Id, t.Text, t.EndTick));
        TestData.Run(sim, 2);   // steps at ticks 3, 4 done
        Assert.Equal("nil", TestAccess.StateValue(host, "t"));
        TestData.Run(sim, 1);
        Assert.Equal("5", TestAccess.StateValue(host, "t"));
        Assert.Empty(host.Timers);
        TestData.Run(sim, 10);
        Assert.Equal("5", TestAccess.StateValue(host, "t"));
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void Time_left_counts_down_and_is_nil_for_an_unknown_or_finished_timer()
    {
        const string Lua = @"function OnInit() Mission.Timer('t', 10, Done); state.u = Mission.TimerLeft('nope'); state.l0 = Mission.TimerLeft('t'); Trigger.AfterDelay(4, Peek) end
function Done() state.after = Mission.TimerLeft('t') end
function Peek() state.l4 = Mission.TimerLeft('t') end";
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 12);
        Assert.Equal("nil", TestAccess.StateValue(host, "u"));
        Assert.Equal("10", TestAccess.StateValue(host, "l0"));
        Assert.Equal("6", TestAccess.StateValue(host, "l4"));
        Assert.Equal("nil", TestAccess.StateValue(host, "after"));
    }

    [Fact]
    public void Cancel_stops_the_timer_and_the_same_id_restarts_it()
    {
        const string Lua = Prelude + @"function A() Mark('a') end
function B() Mark('b') end
function OnInit()
  Mission.Timer('x', 5, A); Mission.Timer('y', 5, B)
  Trigger.AfterDelay(2, Cancel); Trigger.AfterDelay(3, Again)
end
function Cancel() Mission.TimerCancel('y'); Mission.TimerCancel('missing'); state.left = Mission.TimerLeft('x') end
function Again() Mission.Timer('x', 10, A, 'again') end";
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 20);
        Assert.Empty(host.Errors);
        Assert.Equal("3", TestAccess.StateValue(host, "left"));
        Assert.Equal("nil", TestAccess.StateValue(host, "b"));
        Assert.Equal("13", TestAccess.StateValue(host, "a"));   // restarted at tick 3 for 10 ticks
    }

    [Fact]
    public void Timers_expire_in_creation_order_and_a_timer_made_in_a_callback_waits()
    {
        const string Lua = @"function First() state.log = (state.log or '') .. 'F'; Mission.Timer('n', 1, Next) end
function Second() state.log = (state.log or '') .. 'S' end
function Next() state.log = (state.log or '') .. 'N' end
function OnInit() Mission.Timer('a', 4, First); Mission.Timer('b', 4, Second) end";
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 5);
        Assert.Equal("FS", TestAccess.StateValue(host, "log"));
        TestData.Run(sim, 1);
        Assert.Equal("FSN", TestAccess.StateValue(host, "log"));
    }

    [Fact]
    public void Save_and_load_in_the_middle_of_a_timer_reaches_the_same_hash_and_still_fires()
    {
        const string Lua = Prelude + "function Fire() Mark('t'); Actor.Create('auth_rifleman', 0, {x = 3, y = 3}) end\nfunction OnInit() Mission.Timer('t1', 40, Fire, 'Relief') end";
        var (straight, reloaded, h1, h2) = SaveLoad.Compare(Lua, saveAt: 15, total: 60);
        Assert.Equal(straight, reloaded);
        Assert.Equal("40", TestAccess.StateValue(h2, "t"));
        Assert.Equal(TestAccess.StateValue(h1, "t"), TestAccess.StateValue(h2, "t"));
        Assert.Empty(h2.Errors);
    }
}
