// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>Authority 03 "The Coast Road" loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> wins it.</summary>
public sealed class AuthorityMission03Tests
{
    private const string Mission = "auth_03";
    private const int TickLimit = 30000;

    private static readonly CellCoord[] TurretSpots =
    {
        new(19, 13), new(19, 15), new(18, 17), new(18, 11), new(19, 17), new(19, 12), new(18, 18),
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
        Assert.Equal(4, m.Host.Objectives.Count);
        Assert.Equal(3, m.Host.VisibleObjectives.Count());   // the cache is hidden
        Assert.Equal("authority", m.Def.Faction);
        Assert.Equal(3, m.Def.Act);
        Assert.Equal("auth_04", m.Def.Next[0]);
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
    public void Starting_tech_keeps_act_two_unlocks_and_the_enemies_have_their_factions()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal(6000, m.Sim.Players.Cash[0]);
        var rules = m.Sim.Setup.Rules;
        int hq = SimRules.IndexOf(rules.Buildings, "auth_signals_hq", b => b.Id);
        Assert.False(m.Sim.IsUnlocked(0, ProductionCategory.Structure, hq));
        Assert.Contains("auth_engineering", m.Def.StartsWith);
        Assert.Contains("auth_signals_hq", m.Def.UnlocksOnWin);
        Assert.Equal(Faction.Directorate, m.Sim.Setup.FactionOf(1));
        Assert.Equal(Faction.LeftBehind, m.Sim.Setup.FactionOf(2));
    }

    [Fact]
    public void The_hidden_cache_pays_when_found()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        Assert.DoesNotContain(m.Host.VisibleObjectives, o => o.Id == "cache");
        int cash = bot.Cash;
        bot.OrderAll(CommandKind.Move, new CellCoord(31, 36));
        Assert.True(bot.RunUntilObjective("cache", 3000), bot.Diagnostics());
        Assert.Contains(m.Host.VisibleObjectives, o => o.Id == "cache");
        Assert.True(bot.Cash >= cash + 600 - 5);
        Assert.Empty(m.Host.Errors);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void A_bot_escorts_the_convoy_clears_the_picket_and_wins(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);
        Simulation s = m.Sim;

        // build a strike force and two Turrets first, then the convoy is every unit that exists; later units stay home for the raids.
        for (int i = 0; i < 4; i++) bot.Train("auth_tank");
        for (int i = 0; i < 5; i++) bot.Train("auth_rifleman");
        Assert.True(bot.BuildAndPlace("auth_turret", TurretSpots, 900), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("auth_turret", TurretSpots, 900), bot.Diagnostics());
        Assert.True(bot.RunUntil(() => bot.Count("auth_tank") >= 6 && bot.Count("auth_rifleman") >= 8, 4200, 10), bot.Diagnostics() + " tanks " + bot.Count("auth_tank") + " rifles " + bot.Count("auth_rifleman"));

        var carriers = new List<EntityId>();
        var escort = new List<(EntityId Id, ActorKind Kind)>();
        int carrierType = SimRules.IndexOf(s.Setup.Rules.Vehicles, "auth_carrier", v => v.Id);
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0)
            {
                if (s.Units.TypeIndex[i] == carrierType) carriers.Add(s.Units.Slots.IdOf(i));
                else escort.Add((s.Units.Slots.IdOf(i), ActorKind.Vehicle));
            }
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0) escort.Add((s.Infantry.Slots.IdOf(i), ActorKind.Infantry));

        bool assault = false;
        void March()
        {
            foreach (EntityId c in carriers)
                if (s.Units.Slots.IsAlive(c)) bot.Send(CommandKind.AttackMove, c, assault ? new CellCoord(56, 82) : new CellCoord(52, 74), (int)ActorKind.Vehicle);
            foreach ((EntityId id, ActorKind kind) in escort)
            {
                bool alive = kind == ActorKind.Vehicle ? s.Units.Slots.IsAlive(id) : s.Infantry.Slots.IsAlive(id);
                if (alive) bot.Send(CommandKind.AttackMove, id, assault ? new CellCoord(64, 81) : new CellCoord(52, 76), (int)kind);
            }
        }

        // march to a staging point north of the beach, regroup, then assault the picket together.
        March();
        for (int i = 0; i < 6; i++) bot.Train("auth_rifleman");
        bot.Train("auth_tank");
        bot.Train("auth_tank");
        int marched = s.Tick;
        for (int t = 0; t < TickLimit && !s.GameOver; t += 100)
        {
            if (!assault && s.Tick - marched > 1400) assault = true;
            if (t % 300 == 0) March();
            if (t % 900 == 0) bot.Train("auth_rifleman");
            bot.Idle(99);
        }

        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics() + " tanks " + bot.Count("auth_tank") + " rifles " + bot.Count("auth_rifleman") + " carriers " + bot.Count("auth_carrier") + " yard " + bot.Count("auth_conyard"));
        Assert.Empty(m.Host.Errors);
        Assert.True(s.Tick < TickLimit, bot.Diagnostics());
        Assert.Equal(0, s.WinningTeam);
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }
}
