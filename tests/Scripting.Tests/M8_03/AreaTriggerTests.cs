// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting.Tests.M8_03;

/// <summary>Trigger.OnLeave / OnHold / OnSeen / OnIdle. Zone "z" is cells 10..14 x 10..14.</summary>
public sealed class AreaTriggerTests
{
    private const string Prelude = "function Mark(name) state[name] = Mission.Tick() end\n";

    [Fact]
    public void On_hold_fires_after_n_continuous_ticks_and_only_once()
    {
        const string Lua = Prelude + @"function Held() Mark('held'); state.n = (state.n or 0) + 1 end
function OnInit() Actor.Create('auth_rifleman', 0, {x = 12, y = 12}, 'r'); Trigger.OnHold('z', 0, 20, Held) end";
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 19);   // counting starts in the tick OnInit runs (tick 0): ticks 0..18 are 19 ticks
        Assert.Equal("nil", TestAccess.StateValue(host, "held"));
        TestData.Run(sim, 1);
        Assert.Equal("19", TestAccess.StateValue(host, "held"));   // the 20th continuous tick
        TestData.Run(sim, 50);
        Assert.Equal("1", TestAccess.StateValue(host, "n"));
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void On_hold_restarts_when_the_zone_empties()
    {
        // The rifleman is ordered away at tick 10 and a second one arrives later: the count must start again, not carry on.
        const string Lua = Prelude + @"function Held() Mark('held') end
function Leave() Actor.Order('r', 'move', {x = 3, y = 3}) end
function Arrive() Actor.Create('auth_rifleman', 0, {x = 12, y = 12}) end
function OnInit()
  Actor.Create('auth_rifleman', 0, {x = 12, y = 12}, 'r')
  Trigger.OnHold('z', 0, 60, Held)
  Trigger.AfterDelay(2, Leave)
  Trigger.AfterDelay(150, Arrive)
end";
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 140);
        Assert.Equal("nil", TestAccess.StateValue(host, "held"));   // would have fired at tick 59 without the break
        TestData.Run(sim, 100);
        int at = int.Parse(TestAccess.StateValue(host, "held"));
        Assert.True(at >= 150 + 60 - 1, "held fired at " + at);
    }

    [Fact]
    public void Save_and_load_in_the_middle_of_a_hold_reaches_the_same_final_hash()
    {
        const string Lua = Prelude + @"function Held() Mark('held'); Actor.Create('auth_rifleman', 0, {x = 3, y = 3}) end
function OnInit() Actor.Create('auth_rifleman', 0, {x = 12, y = 12}, 'r'); Trigger.OnHold('z', 0, 40, Held) end";
        var (straight, reloaded, h1, h2) = SaveLoad.Compare(Lua, saveAt: 25, total: 90);
        Assert.Equal(straight, reloaded);
        Assert.NotEqual("nil", TestAccess.StateValue(h1, "held"));
        Assert.Equal(TestAccess.StateValue(h1, "held"), TestAccess.StateValue(h2, "held"));
        Assert.Empty(h2.Errors);
    }

    [Fact]
    public void On_leave_waits_for_the_player_to_be_inside_then_fires_once_when_the_zone_empties()
    {
        const string Lua = Prelude + @"function Gone() Mark('gone'); state.n = (state.n or 0) + 1 end
function Go() Actor.Order('r', 'move', {x = 40, y = 40}) end
function OnInit()
  Trigger.OnLeave('z', 0, Gone)
  Actor.Create('auth_rifleman', 0, {x = 12, y = 12}, 'r')
  Trigger.AfterDelay(20, Go)
end";
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 15);
        Assert.Equal("nil", TestAccess.StateValue(host, "gone"));   // never fires just because the zone is empty at the start
        TestData.Run(sim, 120);
        Assert.NotEqual("nil", TestAccess.StateValue(host, "gone"));
        Assert.Equal("1", TestAccess.StateValue(host, "n"));
        Assert.True(int.Parse(TestAccess.StateValue(host, "gone")) > 20);
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void On_seen_fires_when_a_zone_becomes_visible_to_the_player()
    {
        // Player 0's rifleman starts far away; a second one is created inside the zone later and sees it.
        const string Lua = Prelude + @"function Seen() Mark('seen') end
function Make() Actor.Create('auth_rifleman', 0, {x = 12, y = 12}) end
function OnInit()
  Actor.Create('auth_rifleman', 0, {x = 50, y = 50})
  Trigger.OnSeen('z', 0, Seen)
  Trigger.AfterDelay(10, Make)
end";
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 10);
        Assert.Equal("nil", TestAccess.StateValue(host, "seen"));
        TestData.Run(sim, 10);
        int at = int.Parse(TestAccess.StateValue(host, "seen") == "nil" ? "-1" : TestAccess.StateValue(host, "seen"));
        Assert.InRange(at, 11, 14);
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void On_seen_on_an_enemy_tag_fires_when_the_enemy_comes_into_sight_and_not_for_the_other_player()
    {
        const string Lua = Prelude + @"function Seen() Mark('seen') end
function SeenByOther() Mark('other') end
function OnInit()
  Actor.Create('auth_rifleman', 0, {x = 50, y = 50})
  Actor.Create('auth_rifleman', 1, {x = 20, y = 20}, 'e')
  Trigger.OnSeen('e', 0, Seen)
  Trigger.OnSeen('e', 1, SeenByOther)
end";
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 20);
        Assert.Equal("nil", TestAccess.StateValue(host, "seen"));    // far away from player 0's rifleman
        Assert.NotEqual("nil", TestAccess.StateValue(host, "other")); // player 1 sees its own unit at once
    }

    [Fact]
    public void On_idle_fires_for_a_standing_actor_and_not_while_it_has_an_order()
    {
        const string Lua = Prelude + @"function Idle() Mark('idle') end
function Busy() Mark('busy') end
function OnInit()
  Actor.Create('auth_rifleman', 0, {x = 12, y = 12}, 'still')
  Actor.Create('auth_rifleman', 0, {x = 3, y = 3}, 'walker')
  Actor.Order('walker', 'move', {x = 60, y = 60})
  Trigger.OnIdle('still', 15, Idle)
  Trigger.OnIdle('walker', 15, Busy)
end";
        var (sim, host) = TestData.Tiny(Lua);
        TestData.Run(sim, 40);
        Assert.NotEqual("nil", TestAccess.StateValue(host, "idle"));
        Assert.Equal("nil", TestAccess.StateValue(host, "busy"));
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void Save_and_load_mid_idle_count_reaches_the_same_hash()
    {
        const string Lua = Prelude + @"function Idle() Mark('idle'); Actor.Create('auth_rifleman', 0, {x = 3, y = 3}) end
function OnInit() Actor.Create('auth_rifleman', 0, {x = 12, y = 12}, 's'); Trigger.OnIdle('s', 50, Idle); Trigger.OnLeave('z', 0, Idle) end";
        var (straight, reloaded, h1, h2) = SaveLoad.Compare(Lua, saveAt: 20, total: 90);
        Assert.Equal(straight, reloaded);
        Assert.Equal(TestAccess.StateValue(h1, "idle"), TestAccess.StateValue(h2, "idle"));
        Assert.NotEqual("nil", TestAccess.StateValue(h2, "idle"));
    }
}
