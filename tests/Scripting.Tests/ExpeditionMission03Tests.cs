// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>The third Expedition mission (exp_03, The Fleet Reaches the Coast) loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> raises the air arm, brings the carrier in and holds both fronts.</summary>
public sealed class ExpeditionMission03Tests
{
    private const string Mission = "exp_03";
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
        Assert.Equal(3, m.Def.Act);
        Assert.Equal(2, m.Def.Schema);
        Assert.Equal(4, m.Host.VisibleObjectives.Count());   // the "fleet" bonus is revealed once the carrier is in
        Assert.Equal(4, m.Host.Objectives.Count);   // three primary plus the "fleet" bonus
        Assert.False(bot.Done("deck"));
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
    public void A_bot_raises_the_air_arm_and_holds_the_cove(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);
        CellCoord depot = ExpeditionMissionTestKit.BuildingCell(m.Sim, "exp_landing_depot");
        var hold = new CellCoord(21, 44);
        bool BuildD(MissionBot b, string id, CellCoord[] sp) => ExpeditionMissionTestKit.BuildDefending(b, id, sp, depot, hold);
        CellCoord[] spots = ExpeditionMissionTestKit.Spots(m.Sim, depot, 4, 10, c => c.X >= 14 && c.Y >= 36 && c.Y <= 55);

        for (int i = 0; i < 3; i++) { bot.Train("exp_glide_tank"); bot.Train("exp_marine"); }   // the yard and billet work while the engineers build
        // Power first: the SAM Mounts and the Flight Deck go dark on a short grid.
        Assert.True(BuildD(bot, "exp_pile_house", spots), "exp_pile_house " + bot.Diagnostics());
        Assert.True(BuildD(bot, "exp_pile_house", spots), "exp_pile_house " + bot.Diagnostics());
        Assert.True(BuildD(bot, "exp_sam", spots), "exp_sam " + bot.Diagnostics());
        Assert.True(BuildD(bot, "exp_flight_deck", spots), "exp_flight_deck " + bot.Diagnostics());
        Assert.True(BuildD(bot, "exp_sam", spots), "exp_sam " + bot.Diagnostics());
        Assert.True(BuildD(bot, "exp_sam", spots), "exp_sam " + bot.Diagnostics());
        for (int i = 0; i < 3; i++) Assert.True(BuildD(bot, "exp_provision_store", spots), "exp_provision_store " + bot.Diagnostics());
        Assert.True(BuildD(bot, "exp_pillbox", spots), "exp_pillbox " + bot.Diagnostics());
        Assert.True(BuildD(bot, "exp_billet", spots), "exp_billet " + bot.Diagnostics());

        for (int guard = 0; m.Sim.Tick < TickLimit && !m.Sim.GameOver; guard++)
        {
            if (guard % 40 == 0) bot.Train(bot.Count("exp_glide_tank") < 8 || bot.Count("exp_marine") >= 14 ? "exp_glide_tank" : "exp_marine");
            if (guard % 60 == 0) ExpeditionMissionTestKit.OrderLand(m.Sim, CommandKind.AttackMove, ExpeditionMissionTestKit.EnemyNear(m.Sim, depot, 20) ?? hold);
            else bot.Idle();
        }

        Assert.Empty(m.Host.Errors);
        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics());
        Assert.True(m.Sim.Tick < TickLimit, bot.Diagnostics());
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }
}
