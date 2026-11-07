// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>dir_02 "The Convoy Roads": a scripted player wins it at two tiers, an idle player does not.</summary>
public sealed class DirectorateMission02Tests
{
    [Fact]
    public void Idle_player_runs_without_script_errors_and_does_not_win()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_02");
        var bot = new MissionBot(m);
        bot.Idle(3000);
        Assert.Empty(m.Host.Errors);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
        Assert.Equal(2, m.Host.VisibleObjectives.Count());
        Assert.Equal(3, m.Host.Objectives.Count);   // plus the hidden "depot" bonus
    }

    [Fact]
    public void Schema_two_fields_are_set()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_02");
        Assert.Equal("directorate", m.Def.Faction);
        Assert.Equal(2, m.Def.Act);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void A_simple_player_can_win(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_02");
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);

        // Guard the shaft first so the reserve cannot reach it, then go raiding.
        // Each leg: gather at a staging cell, then attack the post together (a stream of conscripts loses to riflemen).
        var stage = new[] { new CellCoord(35, 21), new CellCoord(48, 36), new CellCoord(15, 33), new CellCoord(28, 40) };
        var target = new[] { new CellCoord(38, 12), new CellCoord(55, 31), new CellCoord(9, 36), new CellCoord(29, 49) };

        for (int guard = 0; guard < 30000 && !bot.Sim.GameOver; guard++)
        {
            if (guard % 200 == 0 && bot.Cash > 800)
            {   // the queue holds only a few items: keep topping it up
                for (int i = 0; i < 3; i++) bot.Train("dir_conscript");
                for (int i = 0; i < 2; i++) bot.Train("dir_conscript");
                if (guard % 1000 == 0) bot.Train("dir_sapper");
            }
            if (guard >= 1500 && guard % 100 == 0)
            {
                int leg = ((guard - 1500) / 2400) % stage.Length, into = (guard - 1500) % 2400;
                if (into == 0) bot.OrderAll(CommandKind.AttackMove, stage[leg]);
                else if (into == 1000) bot.OrderAll(CommandKind.AttackMove, target[leg]);
                else if (into > 1000 && into % 400 == 0) bot.OrderAll(CommandKind.AttackMove, target[leg]);
            }
            bot.Idle();
            if (m.Host.Outcome == MissionOutcome.Won) break;
        }

        Assert.Empty(m.Host.Errors);
        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics());
        Assert.True(bot.Sim.Tick < 30000, bot.Diagnostics());
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }
}
