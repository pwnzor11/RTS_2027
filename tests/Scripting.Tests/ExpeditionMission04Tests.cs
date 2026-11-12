// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>The fourth Expedition mission (exp_04, The Fuel, the finale) loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> takes the ash field, holds the beachhead and breaks the Directorate redoubt.</summary>
public sealed class ExpeditionMission04Tests
{
    private const string Mission = "exp_04";
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
        Assert.Equal(4, m.Def.Act);
        Assert.Equal(2, m.Def.Schema);
        Assert.Empty(m.Def.Next);   // the finale has no next mission
        Assert.Equal(3, m.Host.VisibleObjectives.Count());
        Assert.Equal(4, m.Host.Objectives.Count);   // plus the hidden "ash" bonus
        Assert.False(bot.Done("fuel"));
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
    // S21 (D-221): tier 5 is measured over 12 seeds (SeedSweepTests); its fixed seed may lose, so no tier-5 case here.
    public void A_bot_takes_the_ash_holds_the_shore_and_breaks_the_redoubt(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);
        CellCoord depot = ExpeditionMissionTestKit.BuildingCell(m.Sim, "exp_landing_depot");
        var hold = new CellCoord(46, 76);
        var target = new CellCoord(46, 19);
        bool BuildD(string id, CellCoord[] sp) => ExpeditionMissionTestKit.BuildDefending(bot, id, sp, depot, hold);
        CellCoord[] spots = ExpeditionMissionTestKit.Spots(m.Sim, depot, 4, 10, c => c.Y >= 72 && c.Y <= 90 && (c.X <= 49 || c.X >= 60));
        CellCoord[] ash = ExpeditionMissionTestKit.Spots(m.Sim, depot, 4, 12, c => c.X >= 51 && c.X <= 55 && c.Y >= 79 && c.Y <= 85);

        for (int i = 0; i < 3; i++) { bot.Train("exp_glide_tank"); bot.Train("exp_marine"); }
        Assert.True(BuildD("exp_dredge_works", ash), "dredge " + bot.Diagnostics());
        Assert.True(BuildD("exp_pile_house", spots), "pile " + bot.Diagnostics());
        for (int i = 0; i < 4; i++) Assert.True(BuildD("exp_provision_store", spots), "store " + bot.Diagnostics());
        Assert.True(BuildD("exp_pillbox", spots), "pillbox " + bot.Diagnostics());
        Assert.True(BuildD("exp_billet", spots), "billet " + bot.Diagnostics());
        Assert.True(bot.Done("fuel"), bot.Diagnostics());

        bool pushed = false;
        for (int guard = 0; m.Sim.Tick < TickLimit && !m.Sim.GameOver; guard++)
        {
            if (guard % 40 == 0) bot.Train(bot.Count("exp_glide_tank") < 12 || bot.Count("exp_marine") >= 12 ? "exp_glide_tank" : "exp_marine");
            if (guard % 60 == 0)
            {
                if (bot.Done("hold") && bot.Count("exp_glide_tank") >= 10) pushed = true;
                CellCoord? near = ExpeditionMissionTestKit.EnemyNear(m.Sim, depot, 20);
                ExpeditionMissionTestKit.OrderLand(m.Sim, CommandKind.AttackMove, pushed ? target : near ?? hold);
            }
            else bot.Idle();
        }

        Assert.Empty(m.Host.Errors);
        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics());
        Assert.True(m.Sim.Tick < TickLimit, bot.Diagnostics());
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }
}
