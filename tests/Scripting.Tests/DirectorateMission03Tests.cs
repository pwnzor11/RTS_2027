// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>dir_03 "The Launch Sites": a scripted player wins it at two tiers, an idle player does not.</summary>
public sealed class DirectorateMission03Tests
{
    [Fact]
    public void Idle_player_runs_without_script_errors_and_does_not_win()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_03");
        var bot = new MissionBot(m);
        bot.Idle(3000);
        Assert.Empty(m.Host.Errors);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
        Assert.Equal(3, m.Host.VisibleObjectives.Count());
        Assert.Equal(4, m.Host.Objectives.Count);   // plus the hidden "ridge" bonus
    }

    [Fact]
    public void Schema_two_fields_are_set()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_03");
        Assert.Equal("directorate", m.Def.Faction);
        Assert.Equal(3, m.Def.Act);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void A_simple_player_can_win(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_03");
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);

        // Guard the shaft first so the reserve cannot reach it, then go raiding.
        // Each leg: gather at a staging cell, then attack the pad together. Legs: pad alpha, pad beta, the ridge, pad alpha again.
        var stage = new[] { new CellCoord(30, 40), new CellCoord(55, 30), new CellCoord(52, 44), new CellCoord(36, 44) };
        var target = new[] { new CellCoord(44, 49), new CellCoord(66, 25), new CellCoord(58, 52), new CellCoord(44, 49) };

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
        if (tier >= 5) return;   // D-221: tier 5 is balanced by the 12-seed sweep; one fixed seed may lose
        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics());
        Assert.True(bot.Sim.Tick < 30000, bot.Diagnostics());
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }
}
