// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>The third Left Behind mission (lb_03, "Hatch Field") loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> wins it on two fronts.</summary>
public sealed class LeftBehindMission03Tests
{
    private const string Mission = "lb_03";
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
        Assert.Equal(3, m.Host.VisibleObjectives.Count());   // the mast bonus is hidden
        Assert.Equal("left_behind", m.Def.Faction);
        Assert.Equal(3, m.Def.Act);
        Assert.Equal(2, m.Def.Schema);
        Assert.Equal("lb_04", m.Def.Next[0]);
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
    public void Starting_tech_carries_lb_02_unlocks_and_unlocks_the_tier_three_gate()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Contains("lb_harpoon_wagon", m.Def.StartsWith);
        Assert.Contains("lb_bench", m.Def.StartsWith);
        Assert.Contains("lb_rotor_pad", m.Def.UnlocksOnWin);
        var rules = m.Sim.Setup.Rules;
        int pad = SimRules.IndexOf(rules.Buildings, "lb_rotor_pad", b => b.Id);
        Assert.False(m.Sim.IsUnlocked(0, ProductionCategory.Structure, pad));
        int wagon = SimRules.IndexOf(rules.Vehicles, "lb_harpoon_wagon", v => v.Id);
        Assert.True(m.Sim.IsUnlocked(0, ProductionCategory.Vehicle, wagon));
    }

    [Fact]
    public void The_hidden_mast_pays_cash_when_it_falls()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        Assert.DoesNotContain(m.Host.VisibleObjectives, o => o.Id == "mast");
        bot.Idle(5);
        int cash = bot.Cash;
        // Kill the vent from range first, then the mast (it is dark and has no gun).
        Assert.True(KillVentAndMast(m, bot), bot.Diagnostics());
        Assert.Contains(m.Host.VisibleObjectives, o => o.Id == "mast");
        Assert.True(bot.Cash >= cash + 500 - 400, bot.Diagnostics());
        Assert.Empty(m.Host.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5, Skip = "S19 known shift (owner, D-211): tier 5 loses the Mother Rig ~tick 4041 after the M13 batch, not ash-related; re-tune in the campaign retune M13-17")]
    public void A_bot_splits_its_force_and_wins_on_both_fronts(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);
        bot.Idle(5);

        // 1. two more wagons from the Garage Rig while the strike team forms up.
        bot.Train("lb_harpoon_wagon");
        bot.Train("lb_harpoon_wagon");
        bot.RunUntil(() => m.Sim.Tick >= 1500, 1500, 10);

        // 2. the strike team (wagons, slingers and brutes) takes the vent from range; the rest guard the camp.
        Assert.True(KillVentAndMast(m, bot), bot.Diagnostics());

        // 3. close the hatches, with the strike team on attack-move and the home guard at the camp.
        var hatches = new[] { new CellCoord(57, 37), new CellCoord(63, 36), new CellCoord(60, 43) };
        for (int t = 0; t < 12000 && !m.Sim.GameOver && !bot.Done("hatches"); t += 100)
        {
            CellCoord target = hatches.FirstOrDefault(h => EnemyAt(m, h, 2));
            if (target.X == 0) target = new CellCoord(60, 40);
            OrderTypes(m, Strike, CommandKind.AttackMove, target);
            OrderTypes(m, Home, CommandKind.AttackMove, new CellCoord(30, 22));
            bot.Idle(99);
        }
        Assert.True(bot.Done("hatches"), bot.Diagnostics());

        // 4. everyone home to finish the column.
        for (int t = 0; t < 12000 && !m.Sim.GameOver; t += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, NearestFoe(m, new CellCoord(30, 22)) ?? new CellCoord(30, 22));
            bot.Idle(99);
        }

        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics());
        Assert.Empty(m.Host.Errors);
        Assert.True(m.Sim.Tick < TickLimit, bot.Diagnostics());
        Assert.Equal(0, m.Sim.WinningTeam);
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }

    private static readonly string[] Strike = { "lb_harpoon_wagon", "lb_slinger", "lb_drifter_brute" };
    private static readonly string[] Home = { "lb_scavenger", "lb_hook_runner", "lb_buggy", "lb_tinker" };

    // The wagons stand off at five cells and hit the vent; the rest of the strike team screens them. Then the dark field is walked into.
    private static bool KillVentAndMast(MissionSession m, MissionBot bot)
    {
        EntityId? vent = FindBuilding(m, "dir_reactor_vent");
        Assert.NotNull(vent);
        for (int t = 0; t < 4000 && !m.Sim.GameOver && !bot.Done("vent"); t += 100)
        {
            OrderTypes(m, new[] { "lb_slinger", "lb_drifter_brute" }, CommandKind.AttackMove, new CellCoord(54, 40));
            OrderTypes(m, Home, CommandKind.AttackMove, new CellCoord(30, 22));
            OrderAttack(m, new[] { "lb_harpoon_wagon" }, vent!.Value);
            bot.Idle(99);
        }
        if (!bot.Done("vent")) return false;
        EntityId? mast = FindBuilding(m, "dir_mast");
        for (int t = 0; t < 3000 && !m.Sim.GameOver && mast is not null && m.Host.Objectives.Any(o => o.Id == "mast" && o.Status != ObjectiveStatus.Completed); t += 100)
        {
            OrderTypes(m, Strike, CommandKind.AttackMove, new CellCoord(65, 40));
            bot.Idle(99);
        }
        return m.Host.Objectives.Any(o => o.Id == "mast" && o.Status == ObjectiveStatus.Completed);
    }

    private static EntityId? FindBuilding(MissionSession m, string typeId)
    {
        Simulation s = m.Sim;
        int type = SimRules.IndexOf(s.Setup.Rules.Buildings, typeId, b => b.Id);
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.TypeIndex[i] == type && s.Buildings.Player[i] != 0) return s.Buildings.Slots.IdOf(i);
        return null;
    }

    private static bool EnemyAt(MissionSession m, CellCoord cell, int player)
    {
        Simulation s = m.Sim;
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == player && s.Buildings.Cell[i].X == cell.X && s.Buildings.Cell[i].Y == cell.Y) return true;
        return false;
    }

    private static CellCoord? NearestFoe(MissionSession m, CellCoord from)
    {
        Simulation s = m.Sim;
        CellCoord? best = null;
        int bestD = int.MaxValue;
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] != 0 && s.Units.Player[i] < 3) Consider(s.Units.Position[i].ToCell());
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] != 0 && s.Infantry.Player[i] < 3) Consider(s.Infantry.Position[i].ToCell());
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
