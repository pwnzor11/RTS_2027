// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>The second Expedition mission (exp_02, The Supply Line) loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> docks, holds and breaks the garrison.</summary>
public sealed class ExpeditionMission02Tests
{
    private const string Mission = "exp_02";
    private const int TickLimit = 30000;

    [Fact]
    public void Idle_player_does_not_win_and_the_script_runs_clean()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(3000);
        Assert.Empty(m.Host.Errors);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
        Assert.Equal("expedition", m.Def.Faction);
        Assert.Equal(2, m.Def.Act);
        Assert.Equal(2, m.Def.Schema);
        Assert.Equal(3, m.Host.VisibleObjectives.Count());
        Assert.Equal(4, m.Host.Objectives.Count);   // plus the hidden "line" bonus
        Assert.False(bot.Done("dock"));
    }

    [Fact]
    public void Idle_player_for_a_long_time_never_wins()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        new MissionBot(m).Idle(TickLimit);
        Assert.Empty(m.Host.Errors);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void A_bot_docks_holds_and_breaks_the_garrison(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);
        CellCoord depot = ExpeditionMissionTestKit.BuildingCell(m.Sim, "exp_landing_depot");

        // 1. The Anchorage on the shallows of the north shore.
        Assert.True(bot.BuildAndPlace("exp_anchorage", ExpeditionMissionTestKit.DockSpots(m.Sim, depot, 9)), bot.Diagnostics());
        Assert.True(bot.RunUntilObjective("dock", TickLimit), bot.Diagnostics());

        // 2. A seawall, two provision stores (supply cap) and a second billet beside the depot, then Glide Tanks (marines barely scratch emplacements) and marines; the army waits on the landing and clears the road once the columns are beaten.
        CellCoord[] spots = ExpeditionMissionTestKit.Spots(m.Sim, depot, 4, 9, c => c.Y >= 10);
        Assert.True(bot.BuildAndPlace("exp_pillbox", spots), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("exp_provision_store", spots), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("exp_provision_store", spots), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("exp_provision_store", spots), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("exp_provision_store", spots), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("exp_billet", spots), bot.Diagnostics());
        var hold = new CellCoord(20, 14);
        var target = new CellCoord(41, 25);
        bool pushed = false;
        for (int guard = 0; m.Sim.Tick < TickLimit && !m.Sim.GameOver; guard++)
        {
            if (guard % 40 == 0) bot.Train(bot.Count("exp_glide_tank") < 8 || bot.Count("exp_marine") >= 14 ? "exp_glide_tank" : "exp_marine");
            if (guard % 100 == 0)
            {
                if (bot.Done("hold") && bot.Count("exp_glide_tank") >= 6) pushed = true;
                ExpeditionMissionTestKit.OrderLand(m.Sim, CommandKind.AttackMove, pushed ? target : hold);
            }
            else bot.Idle();
        }

        Assert.Empty(m.Host.Errors);
        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics());
        Assert.True(m.Sim.Tick < TickLimit, bot.Diagnostics());
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }
}
