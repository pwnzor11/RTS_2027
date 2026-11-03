// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>Triggers fire on the exact tick and keep their rules (one-shot vs repeating).</summary>
public sealed class TriggerTests
{
    private const string Prelude = "function Mark(name) state[name] = Mission.Tick() end\n";

    [Fact]
    public void After_delay_fires_exactly_n_ticks_after_registration()
    {
        // OnInit runs in the step with Tick 0, so a delay of 5 fires in the step with Tick 5.
        var (sim, host) = TestData.Tiny(Prelude + "function Fire() Mark('a') end\nfunction OnInit() Trigger.AfterDelay(5, Fire) end");
        TestData.Run(sim, 4);
        Assert.Equal("nil", TestAccess.StateValue(host, "a"));
        TestData.Run(sim, 1);   // the step at Tick 4 has run; Tick 5 not yet
        Assert.Equal("nil", TestAccess.StateValue(host, "a"));
        TestData.Run(sim, 1);
        Assert.Equal("5", TestAccess.StateValue(host, "a"));
        TestData.Run(sim, 20);
        Assert.Equal("5", TestAccess.StateValue(host, "a"));   // one shot
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void Every_repeats_on_its_period()
    {
        var (sim, host) = TestData.Tiny(Prelude + "function Beat() state.n = (state.n or 0) + 1; state.last = Mission.Tick() end\nfunction OnInit() Trigger.Every(10, Beat) end");
        TestData.Run(sim, 35);   // fires at ticks 10, 20, 30
        Assert.Equal("3", TestAccess.StateValue(host, "n"));
        Assert.Equal("30", TestAccess.StateValue(host, "last"));
    }

    [Fact]
    public void On_enter_fires_when_a_unit_of_the_player_is_in_the_zone()
    {
        string lua = Prelude + "function In() Mark('in') end\nfunction OnInit()\n Actor.Create('auth_rifleman', 0, {x = 3, y = 3}, 'r')\n Trigger.OnEnter('z', 0, In)\n Trigger.AfterDelay(3, Go)\nend\n"
            + "function Go() Actor.Create('auth_rifleman', 0, {x = 12, y = 12}) end";
        var (sim, host) = TestData.Tiny(lua);
        TestData.Run(sim, 3);
        Assert.Equal("nil", TestAccess.StateValue(host, "in"));
        TestData.Run(sim, 3);   // Go ran in the step at Tick 3; In fires in the next step (Tick 4)
        Assert.Equal("4", TestAccess.StateValue(host, "in"));
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void On_destroyed_and_all_destroyed_fire_once_the_actor_is_gone()
    {
        string lua = Prelude + "function Dead() Mark('dead') end\nfunction Wiped() Mark('wiped') end\nfunction Kill() end\n"
            + "function OnInit()\n Actor.Create('auth_rifleman', 1, {x = 40, y = 40}, 'victim')\n Trigger.OnDestroyed('victim', Dead)\n Trigger.OnAllDestroyed(1, Wiped)\nend";
        var (sim, host) = TestData.Tiny(lua);
        TestData.Run(sim, 5);
        Assert.Equal("nil", TestAccess.StateValue(host, "dead"));
        // Kill the victim through a debug-style direct health write (test only), then let cleanup remove it.
        EntityId id = sim.Infantry.Slots.IdOf(1);
        Assert.True(sim.Infantry.Slots.IsAlive(id));
        sim.Infantry.Health[id.Index] = 0;
        TestData.Run(sim, 3);
        Assert.NotEqual("nil", TestAccess.StateValue(host, "dead"));
        Assert.NotEqual("nil", TestAccess.StateValue(host, "wiped"));
    }

    [Fact]
    public void Errors_in_a_callback_are_collected_and_do_not_stop_the_tick()
    {
        var (sim, host) = TestData.Tiny("function Boom() error('bang') end\nfunction Fine() state.ok = 1 end\nfunction OnInit() Trigger.AfterDelay(1, Boom); Trigger.AfterDelay(1, Fine) end");
        TestData.Run(sim, 4);
        Assert.Single(host.Errors);
        Assert.Contains("bang", host.Errors[0]);
        Assert.Equal("1", TestAccess.StateValue(host, "ok"));
    }

    [Fact]
    public void Mission_win_and_lose_set_game_over_and_winner()
    {
        var (sim, host) = TestData.Tiny("function OnInit() Actor.Create('auth_conyard', 0, {x = 20, y = 20}); Actor.Create('auth_conyard', 1, {x = 40, y = 40}); Trigger.AfterDelay(3, W) end\nfunction W() Mission.Win(0) end");
        TestData.Run(sim, 2);
        Assert.False(sim.GameOver);
        TestData.Run(sim, 4);
        Assert.True(sim.GameOver);
        Assert.Equal(0, sim.WinningTeam);
        Assert.Equal(MissionOutcome.Won, host.Outcome);

        var (sim2, host2) = TestData.Tiny("function OnInit() Actor.Create('auth_conyard', 0, {x = 20, y = 20}); Actor.Create('auth_conyard', 1, {x = 40, y = 40}); Trigger.AfterDelay(3, L) end\nfunction L() Mission.Lose(0) end");
        TestData.Run(sim2, 6);
        Assert.True(sim2.GameOver);
        Assert.Equal(1, sim2.WinningTeam);
        Assert.Equal(MissionOutcome.Lost, host2.Outcome);
    }

    [Fact]
    public void Reinforce_edge_spawns_at_the_edge_and_returns_tags()
    {
        var (sim, host) = TestData.Tiny("function OnInit() state.t = Reinforce.Edge(1, {'auth_rifleman', 'auth_jeep'}, {x = 63, y = 20}, {x = 30, y = 30}) end");
        TestData.Run(sim, 2);
        Assert.Empty(host.Errors);
        Assert.Equal(2, TestAccess.StateValue(host, "t").Split(',').Length);
        Assert.Equal(1, sim.Units.Slots.Count);
        Assert.Equal(1, sim.Infantry.Slots.Count);
        Assert.True(sim.Units.Position[1].ToCell().X >= 60);
    }
}
