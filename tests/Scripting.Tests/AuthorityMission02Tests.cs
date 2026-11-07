// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>Authority 02 "The Junction" loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> wins it.</summary>
public sealed class AuthorityMission02Tests
{
    private const string Mission = "auth_02";
    private const int TickLimit = 30000;

    private static readonly CellCoord[] TurretSpots =
    {
        new(37, 17), new(37, 20), new(36, 18), new(36, 20), new(37, 18), new(37, 19), new(36, 19), new(36, 15), new(37, 16),
    };

    [Fact]
    public void Idle_player_gets_no_errors_and_does_not_win()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(3000);
        Assert.Empty(m.Host.Errors);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
        Assert.Equal(3, m.Host.Objectives.Count);
        Assert.Equal(2, m.Host.VisibleObjectives.Count());   // the mast bonus is hidden until seen
        Assert.Equal("authority", m.Def.Faction);
        Assert.Equal(2, m.Def.Act);
        Assert.Equal("auth_03", m.Def.Next[0]);
        Assert.Single(m.Host.Timers);
    }

    [Fact]
    public void Idle_player_does_not_win_even_when_the_relief_timer_runs_out()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.RunUntil(() => m.Host.Outcome != MissionOutcome.None, TickLimit, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
    }

    [Fact]
    public void Starting_tech_and_unlocks_follow_the_climb()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal(4000, m.Sim.Players.Cash[0]);
        var rules = m.Sim.Setup.Rules;
        int engineering = SimRules.IndexOf(rules.Buildings, "auth_engineering", b => b.Id);
        Assert.False(m.Sim.IsUnlocked(0, ProductionCategory.Structure, engineering));
        Assert.Contains("auth_turret", m.Def.StartsWith);
        Assert.Contains("auth_engineering", m.Def.UnlocksOnWin);
        Assert.Equal(Faction.Directorate, m.Sim.Setup.FactionOf(1));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void A_bot_fortifies_holds_and_wins(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);

        Assert.True(bot.BuildAndPlace("auth_turret", TurretSpots, 600), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("auth_turret", TurretSpots, 600), bot.Diagnostics());
        for (int i = 0; i < 6; i++) bot.Train("auth_rifleman");
        bot.Train("auth_tank");
        bot.Train("auth_tank");
        for (int t = 0; t < TickLimit && !m.Sim.GameOver; t += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, new CellCoord(39, 18));
            if (t % 600 == 0) bot.Train("auth_rifleman");
            bot.Idle(99);
        }

        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics());
        Assert.Empty(m.Host.Errors);
        Assert.True(m.Sim.Tick < TickLimit, bot.Diagnostics());
        Assert.Equal(0, m.Sim.WinningTeam);
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }

    [Fact]
    public void The_hidden_mast_bonus_shows_when_seen_and_completes_when_it_falls()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        Assert.DoesNotContain(m.Host.VisibleObjectives, o => o.Id == "mast");
        bot.OrderAll(CommandKind.AttackMove, new CellCoord(44, 13));
        Assert.True(bot.RunUntil(() => m.Host.VisibleObjectives.Any(o => o.Id == "mast"), 1500, 5), bot.Diagnostics());
        Assert.True(bot.RunUntilObjective("mast", 3000), bot.Diagnostics());
        Assert.Empty(m.Host.Errors);
    }
}
