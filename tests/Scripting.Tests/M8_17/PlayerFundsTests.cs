// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>M8-17: <c>Player.Funds</c> (Cash + Stored, read-only) and the script-win-on-the-elimination-tick finding.</summary>
public sealed class PlayerFundsTests
{
    [Fact]
    public void Funds_includes_stored_credits_and_is_an_integer()
    {
        var (sim, host) = TestData.Tiny("function Probe() state.f = Player.Funds(0); state.c = Player.Cash(0) end\n"
            + "function OnInit() Actor.Create('auth_refinery', 0, {x = 20, y = 20}); Trigger.AfterDelay(300, Probe) end");
        TestData.Run(sim, 290);
        sim.Players.Stored[0] = 1234;
        TestData.Run(sim, 15);   // the probe runs in one of these steps (Stored stays put with no harvesting)
        Assert.Empty(host.Errors);
        int stored = sim.Players.Stored[0];
        Assert.True(stored > 0, "the refinery should hold some stored credits");
        int cash = int.Parse(TestAccess.StateValue(host, "c"));
        Assert.Equal((cash + stored).ToString(), TestAccess.StateValue(host, "f"));
        Assert.Equal(sim.Funds(0), cash + stored);
    }

    [Fact]
    public void Funds_does_not_change_hashed_sim_state()
    {
        // Read-only: two identical runs that both call Funds every few ticks hash the same, and Funds leaves Cash/Stored alone.
        const string Lua = "function Beat() state.n = (state.n or 0) + 1; local x = Player.Funds(0) + Player.Funds(1) end\nfunction OnInit() Trigger.Every(5, Beat) end\n";
        var (a, ha) = TestData.Tiny(Lua);
        var (b, hb) = TestData.Tiny(Lua);
        TestData.Run(a, 40);
        TestData.Run(b, 40);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Assert.Equal(5000, a.Players.Cash[0]);
        Assert.Equal(0, a.Players.Stored[0]);
        Assert.Empty(ha.Errors);
        Assert.Empty(hb.Errors);
    }

    [Fact]
    public void Funds_rejects_a_bad_player_and_has_no_setter()
    {
        var (sim, host) = TestData.Tiny("function Bad() Player.Funds(99) end\nfunction Set() Player.Funds(0, 5) end\nfunction OnInit() Trigger.AfterDelay(1, Bad); Trigger.AfterDelay(2, Set) end");
        TestData.Run(sim, 4);
        Assert.Single(host.Errors);   // the bad slot errors; extra args are ignored like the other getters, nothing is written
        Assert.Equal(5000, sim.Players.Cash[0]);
    }

    [Fact]
    public void Script_win_runs_on_the_same_tick_the_last_enemy_dies()
    {
        string lua = "function OnInit()\n Actor.Create('auth_conyard', 0, {x = 20, y = 20})\n Actor.Create('auth_conyard', 1, {x = 40, y = 40}, 'hq')\n"
            + " Actor.Create('auth_rifleman', 1, {x = 44, y = 44}, 'foe')\n Trigger.OnAllDestroyed(1, Wiped)\nend\n"
            + "function Wiped() state.at = Mission.Tick(); Mission.Win(0) end";
        var (sim, host) = TestData.Tiny(lua);
        TestData.Run(sim, 5);
        Assert.Equal(MissionOutcome.None, host.Outcome);
        Assert.False(sim.GameOver);

        sim.Buildings.Health[sim.Buildings.Slots.IdOf(2).Index] = 0;   // the enemy conyard (slot 2: player 0's is slot 1)
        sim.Infantry.Health[sim.Infantry.Slots.IdOf(1).Index] = 0;
        int before = sim.Tick;
        sim.Step(TestData.NoCommands);   // one step: elimination happens here

        Assert.Empty(host.Errors);
        Assert.Equal(before.ToString(), TestAccess.StateValue(host, "at"));   // the trigger ran on the elimination tick
        Assert.Equal(MissionOutcome.Won, host.Outcome);
        Assert.True(sim.GameOver);
        Assert.Equal(0, sim.WinningTeam);
    }

    [Fact]
    public void Script_win_from_on_destroyed_on_the_elimination_tick()
    {
        string lua = "function OnInit()\n Actor.Create('auth_conyard', 0, {x = 20, y = 20})\n Actor.Create('auth_conyard', 1, {x = 40, y = 40}, 'hq')\n"
            + " Trigger.OnDestroyed('hq', Wiped)\nend\n"
            + "function Wiped() state.at = Mission.Tick(); Mission.Win(0) end";
        var (sim, host) = TestData.Tiny(lua);
        TestData.Run(sim, 5);
        sim.Buildings.Health[sim.Buildings.Slots.IdOf(2).Index] = 0;
        int before = sim.Tick;
        sim.Step(TestData.NoCommands);
        Assert.Empty(host.Errors);
        Assert.Equal(before.ToString(), TestAccess.StateValue(host, "at"));
        Assert.Equal(MissionOutcome.Won, host.Outcome);
        Assert.True(sim.GameOver);
    }
}
