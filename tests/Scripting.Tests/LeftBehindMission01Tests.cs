// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>The first Left Behind mission (lb_01, "The Refused") loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> wins it.</summary>
public sealed class LeftBehindMission01Tests
{
    private const string Mission = "lb_01";
    private const int TickLimit = 20000;

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
        Assert.Equal(3, m.Host.VisibleObjectives.Count());   // the cache bonus is hidden
        Assert.Equal("left_behind", m.Def.Faction);
        Assert.Equal(1, m.Def.Act);
        Assert.Equal(2, m.Def.Schema);
        Assert.Single(m.Host.Timers);
    }

    // S21 (D-219): the start force now engages the convoy in sight, so an idle player stops it and the mission waits for the Mother Rig
    // to be deployed instead of ending in a loss. An idle player still never wins, and the script stays clean.
    [Fact]
    public void Idle_player_never_wins_and_the_script_stays_clean()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.RunUntil(() => m.Host.Outcome != MissionOutcome.None, TickLimit, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
    }

    [Fact]
    public void Starting_tech_is_restricted_to_the_mission_list()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal(2000, m.Sim.Players.Cash[0]);
        var rules = m.Sim.Setup.Rules;
        int garage = SimRules.IndexOf(rules.Buildings, "lb_garage_rig", b => b.Id);
        Assert.False(m.Sim.IsUnlocked(0, ProductionCategory.Structure, garage));
        Assert.Contains("lb_muster_rig", m.Def.StartsWith);
        Assert.Equal(new[] { "lb_garage_rig" }, m.Def.UnlocksOnWin);
    }

    [Fact]
    public void The_hidden_cache_pays_a_scraper_and_cash_when_found()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        Assert.DoesNotContain(m.Host.VisibleObjectives, o => o.Id == "cache");
        int cash = bot.Cash;
        bot.Idle(5);
        bot.OrderAll(CommandKind.Move, new CellCoord(5, 46));
        Assert.True(bot.RunUntilObjective("cache", 3000), bot.Diagnostics());
        Assert.Contains(m.Host.VisibleObjectives, o => o.Id == "cache");
        Assert.Equal(1, bot.Count("auth_scraper"));
        Assert.True(bot.Cash >= cash + 600 - 5);
        Assert.Empty(m.Host.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void A_bot_unpacks_ambushes_and_wins(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);

        // 1. the packed Mother Rig parks on the ash; the player unpacks it on clear ground.
        var site = new CellCoord(30, 22);
        Assert.True(bot.RunUntil(() => PackedRig(m) is not null, 1500, 5), bot.Diagnostics());
        bot.RunUntil(() => m.Sim.Tick >= 600, 700, 5);
        bot.Send(CommandKind.Pack, PackedRig(m)!.Value, site, 0);
        Assert.True(bot.RunUntilObjective("deploy", 3000), bot.Diagnostics());

        // 2. a camp: power, a Muster Rig, then hands.
        var ring = new[] { new CellCoord(34, 22), new CellCoord(26, 26), new CellCoord(30, 26), new CellCoord(34, 26), new CellCoord(38, 22), new CellCoord(30, 29), new CellCoord(34, 29) };
        Assert.True(bot.BuildAndPlace("lb_dynamo_rig", ring), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("lb_muster_rig", ring), bot.Diagnostics());
        for (int i = 0; i < 4; i++) bot.Train("lb_hook_runner");
        for (int i = 0; i < 8; i++) bot.Train("lb_scavenger");

        // 3. take position on the road before the convoy leaves the depot, then chase until the scrapers and the relief are down.
        bot.RunUntil(() => m.Sim.Tick >= 2300, 2300, 10);
        bot.OrderAll(CommandKind.AttackMove, new CellCoord(34, 16));
        Assert.True(bot.RunUntil(() => m.Host.Timers.Count == 0, 4500, 10), bot.Diagnostics());
        var crossing = new CellCoord(34, 16);
        for (int t = 0; t < TickLimit && !m.Sim.GameOver; t += 100)
        {
            // hold the crossing for the scrapers; once they are down, attack-move onto the nearest soldier left of the convoy or relief.
            CellCoord? foe = bot.Done("scrapers") ? NearestFoe(m, crossing) : null;
            bot.OrderAll(CommandKind.AttackMove, foe ?? crossing);
            bot.Idle(99);
        }

        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics() + " FOES" + Foes(m) + " own inf " + m.Sim.Infantry.Slots.Count);
        Assert.Empty(m.Host.Errors);
        Assert.True(m.Sim.Tick < TickLimit, bot.Diagnostics());
        Assert.Equal(0, m.Sim.WinningTeam);
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }

    private static CellCoord? NearestFoe(MissionSession m, CellCoord from)
    {
        Simulation s = m.Sim;
        CellCoord? best = null;
        int bestD = int.MaxValue;
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 1) Consider(s.Units.Position[i].ToCell());
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 1) Consider(s.Infantry.Position[i].ToCell());
        return best;

        void Consider(CellCoord c)
        {
            int d = Math.Abs(c.X - from.X) + Math.Abs(c.Y - from.Y);
            if (d < bestD) { bestD = d; best = c; }
        }
    }

    private static string Foes(MissionSession m)
    {
        Simulation s = m.Sim;
        string e = "";
        for (int i = 1; i < s.Units.Capacity; i++) if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 1) { var c = s.Units.Position[i].ToCell(); e += $" V{s.Units.TypeIndex[i]}({c.X},{c.Y})h{s.Units.Health[i]}"; }
        for (int i = 1; i < s.Infantry.Capacity; i++) if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 1) { var c = s.Infantry.Position[i].ToCell(); e += $" I({c.X},{c.Y})h{s.Infantry.Health[i]}"; }
        return e;
    }

    private static EntityId? PackedRig(MissionSession m)
    {
        Simulation s = m.Sim;
        int type = SimRules.IndexOf(s.Setup.Rules.Vehicles, "lb_mother_rig_packed", v => v.Id);
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0 && s.Units.TypeIndex[i] == type) return s.Units.Slots.IdOf(i);
        return null;
    }
}
