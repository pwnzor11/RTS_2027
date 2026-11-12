// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>The second Left Behind mission (lb_02, "Road Posts") loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> wins it.</summary>
public sealed class LeftBehindMission02Tests
{
    private const string Mission = "lb_02";
    private const int TickLimit = 30000;

    [Fact]
    public void Idle_player_gets_no_errors_and_does_not_win()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(2000);
        Assert.Empty(m.Host.Errors);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
        Assert.Equal(4, m.Host.Objectives.Count);
        Assert.Equal(3, m.Host.VisibleObjectives.Count());   // the stash bonus is hidden
        Assert.Equal("left_behind", m.Def.Faction);
        Assert.Equal(2, m.Def.Act);
        Assert.Equal(2, m.Def.Schema);
        Assert.Equal("lb_03", m.Def.Next[0]);
        Assert.Single(m.Host.Timers);
    }

    [Fact]
    public void Idle_player_eventually_loses_without_a_stall()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.RunUntil(() => m.Host.Outcome != MissionOutcome.None, TickLimit, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
    }

    [Fact]
    public void Starting_tech_carries_lb_01_unlock_and_unlocks_the_tier_two_gate()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal(2400, m.Sim.Players.Cash[0]);
        Assert.Contains("lb_garage_rig", m.Def.StartsWith);
        Assert.Contains("lb_lookout", m.Def.StartsWith);
        Assert.Contains("lb_bench", m.Def.UnlocksOnWin);
        var rules = m.Sim.Setup.Rules;
        int bench = SimRules.IndexOf(rules.Buildings, "lb_bench", b => b.Id);
        Assert.False(m.Sim.IsUnlocked(0, ProductionCategory.Structure, bench));
    }

    [Fact]
    public void The_hidden_stash_pays_cash_and_two_buggies_when_found()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        Assert.DoesNotContain(m.Host.VisibleObjectives, o => o.Id == "stash");
        bot.Idle(5);
        int cash = bot.Cash;
        int buggies = bot.Count("lb_buggy");
        bot.OrderAll(CommandKind.Move, new CellCoord(49, 4));
        Assert.True(bot.RunUntilObjective("stash", 3000), bot.Diagnostics());
        Assert.Contains(m.Host.VisibleObjectives, o => o.Id == "stash");
        Assert.Equal(buggies + 2, bot.Count("lb_buggy"));
        Assert.True(bot.Cash >= cash + 600 - 5);
        Assert.Empty(m.Host.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    // S21 (D-221): tier 5 is measured over 12 seeds (SeedSweepTests); its fixed seed may lose, so no tier-5 case here.
    public void A_bot_builds_a_garage_strips_the_posts_and_wins(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);

        // 1. a Garage Rig, then slingers (they are what breaks turrets).
        var ring = new[] { new CellCoord(32, 17), new CellCoord(36, 17), new CellCoord(28, 17), new CellCoord(40, 17), new CellCoord(28, 25), new CellCoord(28, 21) };
        Assert.True(bot.BuildAndPlace("lb_garage_rig", ring), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("lb_lookout", ring.Select(c => new CellCoord(c.X + 3, c.Y)).Concat(ring).ToArray()), bot.Diagnostics());
        for (int i = 0; i < 3; i++) bot.Train("lb_slinger");
        bot.RunUntil(() => m.Sim.Tick >= 3600, 3600, 10);
        Assert.True(bot.Done("gate"), bot.Diagnostics());
        Assert.True(bot.Count("lb_slinger") >= 12, bot.Diagnostics());

        // 2. post 1 (north road); its fall calls the reprisal column to the camp, so come home and beat it.
        // Each turret runs off its own plant 8+ cells back: kill the plant first, then walk up on the dark turret.
        Assert.True(HoldUntil(m, bot, new CellCoord(41, 19), () => PowerDown(m) >= 1, 3000), bot.Diagnostics());
        Assert.True(HoldUntil(m, bot, new CellCoord(38, 13), () => PostsDown(m) >= 1, 3000), bot.Diagnostics());
        var camp = new CellCoord(35, 20);
        Assert.True(HoldUntil(m, bot, camp, () => bot.Done("hold"), 6000), bot.Diagnostics());

        // 3. posts 2 and 3.
        Assert.True(HoldUntil(m, bot, new CellCoord(18, 29), () => PowerDown(m) >= 2, 5000), bot.Diagnostics());
        Assert.True(HoldUntil(m, bot, new CellCoord(10, 32), () => PostsDown(m) >= 2, 4000), bot.Diagnostics());
        Assert.True(HoldUntil(m, bot, new CellCoord(21, 32), () => PowerDown(m) >= 3, 5000), bot.Diagnostics());
        Assert.True(HoldUntil(m, bot, new CellCoord(12, 41), () => PostsDown(m) >= 3 || m.Sim.GameOver, 8000), bot.Diagnostics());
        bot.RunUntil(() => m.Host.Outcome != MissionOutcome.None, TickLimit, 10);

        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics() + Dump(m));
        Assert.Empty(m.Host.Errors);
        Assert.True(m.Sim.Tick < TickLimit, bot.Diagnostics());
        Assert.Equal(0, m.Sim.WinningTeam);
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }

    // Moves the whole force to a staging cell and waits for it to arrive.
    private static void Gather(MissionSession m, MissionBot bot, CellCoord cell)
    {
        for (int t = 0; t < 900 && !m.Sim.GameOver; t += 100)
        {
            bot.OrderAll(CommandKind.Move, cell);
            bot.Idle(99);
        }
    }

    // Attack-moves the whole force onto a cell every 100 ticks until the condition holds; false after the tick budget.
    private static bool HoldUntil(MissionSession m, MissionBot bot, CellCoord cell, Func<bool> done, int budget)
    {
        int end = m.Sim.Tick + budget;
        while (m.Sim.Tick < end && !m.Sim.GameOver)
        {
            if (done()) return true;
            bot.OrderAll(CommandKind.AttackMove, cell);
            bot.Idle(99);
        }
        return done();
    }

    private static string Dump(MissionSession m)
    {
        Simulation s = m.Sim;
        string r = " DUMP";
        for (int i = 1; i < s.Infantry.Capacity; i++) if (s.Infantry.Slots.IsIndexAlive(i)) { var c = s.Infantry.Position[i].ToCell(); r += $" I{s.Infantry.Player[i]}t{s.Infantry.TypeIndex[i]}({c.X},{c.Y})h{s.Infantry.Health[i]}"; }
        for (int i = 1; i < s.Units.Capacity; i++) if (s.Units.Slots.IsIndexAlive(i)) { var c = s.Units.Position[i].ToCell(); r += $" V{s.Units.Player[i]}t{s.Units.TypeIndex[i]}({c.X},{c.Y})h{s.Units.Health[i]}"; }
        for (int i = 1; i < s.Buildings.Capacity; i++) if (s.Buildings.Slots.IsIndexAlive(i)) { var c = s.Buildings.Cell[i]; r += $" B{s.Buildings.Player[i]}t{s.Buildings.TypeIndex[i]}({c.X},{c.Y})h{s.Buildings.Health[i]}"; }
        return r;
    }

    private static int PowerDown(MissionSession m) => m.Host.Messages.Count(x => x.Text.Contains("power plant"));

    private static int PostsDown(MissionSession m) => m.Host.Messages.Count(x => x.Text.Contains("road post is down"));
}
