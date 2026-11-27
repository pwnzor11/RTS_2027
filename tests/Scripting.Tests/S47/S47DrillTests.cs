// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Xunit.Abstractions;

namespace Rts.Scripting.Tests.S47;

/// <summary>S47 U-12: the three drill missions load, run without script errors, cannot be won by idling, and a scripted win reaches the medal logic.</summary>
public sealed class S47DrillTests
{
    private static readonly string[] Ids = { "drill_build", "drill_hold", "drill_raid" };
    private readonly ITestOutputHelper _out;

    public S47DrillTests(ITestOutputHelper output) => _out = output;

    [Theory]
    [InlineData("drill_build")]
    [InlineData("drill_hold")]
    [InlineData("drill_raid")]
    public void Each_drill_loads_and_runs_idle_without_errors(string id)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, id);
        Assert.NotNull(m.Def.Drill);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.Equal("authority", m.Def.Faction);
        Assert.Equal(0, m.Def.ParSeconds);
        TestData.Run(m.Sim, 600);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.None, m.Host.Outcome);
        Assert.All(m.Host.Objectives, o => Assert.NotEqual(ObjectiveStatus.Completed, o.Status));
    }

    [Fact]
    public void Thresholds_are_read_from_data_and_ordered()
    {
        var orders = new List<int>();
        foreach (string id in Ids)
        {
            DrillDef d = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, id)).Drill!;
            Assert.True(d.Gold <= d.Silver && d.Silver <= d.Bronze, id);
            orders.Add(d.Order);
        }

        Assert.Equal(new[] { 1, 2, 3 }, orders);
    }

    [Fact]
    public void Medal_logic_picks_the_best_medal_whose_limit_and_keep_minimum_are_met()
    {
        var time = new DrillDef(1, "seconds", 100, 150, 200, 6, 5, 3);
        Assert.Equal(DrillMedal.Gold, DrillScoring.Medal(time, new DrillRun(100, 0, 6)));
        Assert.Equal(DrillMedal.Silver, DrillScoring.Medal(time, new DrillRun(100, 0, 5)));   // fast, but a unit short for gold
        Assert.Equal(DrillMedal.Silver, DrillScoring.Medal(time, new DrillRun(150, 0, 6)));
        Assert.Equal(DrillMedal.Bronze, DrillScoring.Medal(time, new DrillRun(200, 0, 3)));
        Assert.Equal(DrillMedal.None, DrillScoring.Medal(time, new DrillRun(201, 0, 6)));
        Assert.Equal(DrillMedal.None, DrillScoring.Medal(time, new DrillRun(50, 0, 2)));
        var lost = new DrillDef(2, "lost", 0, 3, 6);
        Assert.Equal(DrillMedal.Gold, DrillScoring.Medal(lost, new DrillRun(999, 0, 1)));
        Assert.Equal(DrillMedal.Silver, DrillScoring.Medal(lost, new DrillRun(1, 3, 9)));
        Assert.Equal(DrillMedal.None, DrillScoring.Medal(lost, new DrillRun(1, 7, 9)));
    }

    [Fact]
    public void Build_order_win_path_reaches_a_medal()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "drill_build");
        var bot = new MissionBot(m);
        CellCoord[] spots = Around(new CellCoord(3, 3), 9);
        Assert.True(bot.BuildAndPlace("auth_power", spots), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("auth_refinery", spots), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("auth_factory", spots), bot.Diagnostics());
        bot.Train("auth_scraper");
        Assert.True(bot.RunUntil(() => m.Host.Outcome == MissionOutcome.Won, 20000, 5), bot.Diagnostics());
        Finish(m, "drill_build");
    }

    [Fact]
    public void Hold_the_line_passive_defence_ends_at_bronze_or_fails()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "drill_hold");
        var bot = new MissionBot(m);
        bot.RunUntil(() => m.Host.Outcome != MissionOutcome.None, 20000, 20);
        _out.WriteLine("passive: " + Alive(m) + " " + DrillScoring.Measure(m.Sim, 0));
        if (m.Host.Outcome == MissionOutcome.Won)
            Assert.True(DrillScoring.Medal(m.Def.Drill!, DrillScoring.Measure(m.Sim, 0)) <= DrillMedal.Bronze);
        else
            Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
    }

    [Fact]
    public void Hold_the_line_active_defence_reaches_gold()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "drill_hold");
        var bot = new MissionBot(m);
        // Active play: spend the cash on four turrets at the gate, then riflemen from the barracks.
        CellCoord[] spots = { new(21, 14), new(21, 22), new(20, 15), new(20, 21), new(19, 14), new(19, 22), new(22, 15), new(22, 21) };
        for (int k = 0; k < 4; k++) Assert.True(bot.BuildAndPlace("auth_turret", spots), bot.Diagnostics());
        for (int k = 0; k < 8; k++) bot.Train("auth_rifleman");
        bot.RunUntil(() => m.Host.Outcome != MissionOutcome.None, 20000, 20);
        _out.WriteLine($"active: tick {m.Sim.Tick} units lost {m.Sim.StatsOf(0).UnitsLost} structs lost {m.Sim.StatsOf(0).StructuresLost} turrets {bot.Count("auth_turret")} rifles {bot.Count("auth_rifleman")} cash {bot.Cash}");
        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics() + " " + Alive(m));
        Finish(m, "drill_hold");
        Assert.Equal(DrillMedal.Gold, DrillScoring.Medal(m.Def.Drill!, DrillScoring.Measure(m.Sim, 0)));
    }

    [Fact]
    public void Raid_win_path_reaches_a_medal()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "drill_raid");
        var bot = new MissionBot(m);
        for (int guard = 0; guard < 4000 && m.Host.Outcome == MissionOutcome.None; guard += 20)
        {
            bot.OrderAll(CommandKind.AttackMove, new CellCoord(27, 3));
            bot.Idle(19);
        }

        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics() + " " + Alive(m));
        Finish(m, "drill_raid");
    }

    private static string Alive(MissionSession m) =>
        $"p0 rifle {Count(m, 0, "auth_rifleman")} tank {Count(m, 0, "auth_tank")} jeep {Count(m, 0, "auth_jeep")} | p1 rifle {Count(m, 1, "auth_rifleman")} scraper {Count(m, 1, "auth_scraper")} lost {m.Sim.StatsOf(0).UnitsLost}";

    private static int Count(MissionSession m, byte p, string type) => new MissionBot(m, p).Count(type);

    private void Finish(MissionSession m, string id)
    {
        Assert.Empty(m.Host.Errors);
        DrillRun run = DrillScoring.Measure(m.Sim, 0);
        DrillMedal medal = DrillScoring.Medal(m.Def.Drill!, run);
        _out.WriteLine($"{id}: {run} -> {medal}");
        Assert.True(run.Seconds >= 1);
        Assert.True(medal >= DrillMedal.Bronze, $"{id}: the scripted win should earn at least bronze, got {run} -> {medal}");
    }

    private static CellCoord[] Around(CellCoord origin, int radius)
    {
        var list = new List<CellCoord>();
        for (int r = 3; r <= radius; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && origin.X + dx >= 0 && origin.Y + dy >= 0)
                        list.Add(new CellCoord(origin.X + dx, origin.Y + dy));
        return list.ToArray();
    }
}
