// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>The finale (lb_04, "The Banner") loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> beats the counter-landing, darkens the batteries from the east and breaks the depot.</summary>
public sealed class LeftBehindMission04Tests
{
    private const string Mission = "lb_04";
    private const int TickLimit = 30000;
    private static ushort _seq = 40000;

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
        Assert.Equal(3, m.Host.VisibleObjectives.Count());   // the wreck bonus is hidden
        Assert.Equal("left_behind", m.Def.Faction);
        Assert.Equal(4, m.Def.Act);
        Assert.Equal(2, m.Def.Schema);
        Assert.Empty(m.Def.Next);
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
    public void The_expedition_plays_by_its_own_rules_and_the_tech_climb_reaches_the_superweapon()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Contains("lb_rotor_pad", m.Def.StartsWith);
        Assert.Contains("lb_rolling_fort", m.Def.StartsWith);
        Assert.Contains("lb_second_drift", m.Def.StartsWith);
        var rules = m.Sim.Setup.Rules;
        int drift = SimRules.IndexOf(rules.Buildings, "lb_second_drift", b => b.Id);
        Assert.True(m.Sim.IsUnlocked(0, ProductionCategory.Structure, drift));
        Assert.Equal(Faction.Expedition, m.Sim.Setup.FactionOf(1));
    }

    [Fact]
    public void The_hidden_wreck_pays_cash_and_two_tinkers_when_found()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        Assert.DoesNotContain(m.Host.VisibleObjectives, o => o.Id == "wreck");
        bot.Idle(5);
        int cash = bot.Cash;
        int tinkers = bot.Count("lb_tinker");
        OrderTypes(m, new[] { "lb_hook_runner", "lb_buggy" }, CommandKind.Move, new CellCoord(81, 49));
        Assert.True(bot.RunUntilObjective("wreck", 4000), bot.Diagnostics());
        Assert.Contains(m.Host.VisibleObjectives, o => o.Id == "wreck");
        Assert.Equal(tinkers + 2, bot.Count("lb_tinker"));
        Assert.True(bot.Cash >= cash + 700 - 5);
        Assert.Empty(m.Host.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void A_bot_holds_the_camp_flanks_from_the_east_and_wins(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);
        bot.Idle(5);

        // 1. hold the camp until the counter-landing is broken.
        var camp = new CellCoord(47, 24);
        for (int t = 0; t < 9000 && !m.Sim.GameOver && !bot.Done("counter"); t += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, NearestFoe(m, camp, 30) ?? camp);
            bot.Idle(99);
        }
        Assert.True(bot.Done("counter"), bot.Diagnostics());

        // 2. swing round to the east flank, out of the batteries' reach, and take the pile houses from range.
        for (int t = 0; t < 2400 && !m.Sim.GameOver; t += 100)
        {
            bot.OrderAll(CommandKind.Move, new CellCoord(72, 70));
            bot.Idle(99);
        }
        for (int t = 0; t < 5000 && !m.Sim.GameOver && !bot.Done("power"); t += 100)
        {
            EntityId? pile = FindBuilding(m, "exp_pile_house");
            if (pile is null) break;
            OrderAttack(m, new[] { "lb_rolling_fort", "lb_harpoon_wagon", "lb_organ" }, pile.Value);
            OrderTypes(m, new[] { "lb_slinger", "lb_drifter_brute", "lb_scavenger", "lb_hook_runner", "lb_veilwalker", "lb_tinker", "lb_buggy" }, CommandKind.AttackMove, new CellCoord(70, 76));
            bot.Idle(99);
        }
        Assert.True(bot.Done("power"), bot.Diagnostics());

        // 3. dark batteries are walls: break the depot.
        var depot = new CellCoord(47, 82);
        for (int t = 0; t < 12000 && !m.Sim.GameOver; t += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, depot);
            bot.Idle(99);
        }

        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics());
        Assert.Empty(m.Host.Errors);
        Assert.True(m.Sim.Tick < TickLimit, bot.Diagnostics());
        Assert.Equal(0, m.Sim.WinningTeam);
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }

    private static EntityId? FindBuilding(MissionSession m, string typeId)
    {
        Simulation s = m.Sim;
        int type = SimRules.IndexOf(s.Setup.Rules.Buildings, typeId, b => b.Id);
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.TypeIndex[i] == type && s.Buildings.Player[i] != 0) return s.Buildings.Slots.IdOf(i);
        return null;
    }

    // The nearest enemy soldier or vehicle within <paramref name="reach"/> cells (Manhattan) of the cell.
    private static CellCoord? NearestFoe(MissionSession m, CellCoord from, int reach)
    {
        Simulation s = m.Sim;
        CellCoord? best = null;
        int bestD = reach + 1;
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

    // One tick with an order for every own unit or soldier of the listed types.
    private static void OrderTypes(MissionSession m, string[] typeIds, CommandKind kind, CellCoord cell)
    {
        Simulation s = m.Sim;
        var rules = s.Setup.Rules;
        var cmds = new List<Command>();
        foreach (string id in typeIds)
        {
            int v = SimRules.IndexOf(rules.Vehicles, id, x => x.Id);
            if (v >= 0)
                for (int i = 1; i < s.Units.Capacity; i++)
                    if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0 && s.Units.TypeIndex[i] == v)
                        cmds.Add(new Command(s.Tick, 0, _seq++, kind, s.Units.Slots.IdOf(i), default, cell, (int)ActorKind.Vehicle));
            int f = SimRules.IndexOf(rules.Infantry, id, x => x.Id);
            if (f >= 0)
                for (int i = 1; i < s.Infantry.Capacity; i++)
                    if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0 && s.Infantry.TypeIndex[i] == f)
                        cmds.Add(new Command(s.Tick, 0, _seq++, kind, s.Infantry.Slots.IdOf(i), default, cell, (int)ActorKind.Infantry));
        }
        s.Step(cmds);
    }

    // One tick with an Attack order on a building for every own unit of the listed vehicle types.
    private static void OrderAttack(MissionSession m, string[] typeIds, EntityId target)
    {
        Simulation s = m.Sim;
        var cmds = new List<Command>();
        foreach (string id in typeIds)
        {
            int v = SimRules.IndexOf(s.Setup.Rules.Vehicles, id, x => x.Id);
            for (int i = 1; i < s.Units.Capacity; i++)
                if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0 && s.Units.TypeIndex[i] == v)
                    cmds.Add(new Command(s.Tick, 0, _seq++, CommandKind.Attack, s.Units.Slots.IdOf(i), target, default, (int)ActorKind.Vehicle | ((int)ActorKind.Building << 8)));
        }
        s.Step(cmds);
    }
}
