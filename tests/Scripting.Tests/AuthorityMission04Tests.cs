// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>Authority 04 "The Sunrise Device" (the finale) loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> wins it.</summary>
public sealed class AuthorityMission04Tests
{
    private const string Mission = "auth_04";
    private const int TickLimit = 16000;

    private static readonly CellCoord[] TurretSpots =
    {
        new(52, 12), new(52, 14), new(48, 17), new(44, 18), new(52, 16), new(53, 10), new(46, 17),
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
        Assert.Equal(3, m.Host.VisibleObjectives.Count());   // the vent is hidden until the device is seen
        Assert.Equal("authority", m.Def.Faction);
        Assert.Equal(4, m.Def.Act);
        Assert.Empty(m.Def.Next);
        Assert.Single(m.Host.Timers);
    }

    [Fact]
    public void Idle_player_loses_when_the_sunrise_fires_not_by_stalling()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.RunUntil(() => m.Host.Outcome != MissionOutcome.None, 20000, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
    }

    [Fact]
    public void Starting_tech_keeps_every_earlier_unlock()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal(8000, m.Sim.Players.Cash[0]);
        var rules = m.Sim.Setup.Rules;
        int battery = SimRules.IndexOf(rules.Buildings, "auth_atomic_battery", b => b.Id);
        Assert.False(m.Sim.IsUnlocked(0, ProductionCategory.Structure, battery));
        Assert.Contains("auth_turret", m.Def.StartsWith);
        Assert.Contains("auth_engineering", m.Def.StartsWith);
        Assert.Contains("auth_howitzer", m.Def.StartsWith);
        Assert.Equal(new[] { "auth_atomic_battery" }, m.Def.UnlocksOnWin);
        Assert.Equal(Faction.Directorate, m.Sim.Setup.FactionOf(1));
    }

    [Fact]
    public void A_bridgehead_of_six_on_the_south_bank_completes_the_ford_and_calls_the_reserve()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        Assert.DoesNotContain(m.Host.VisibleObjectives, o => o.Id == "vent");
        bot.OrderAll(CommandKind.Move, new CellCoord(47, 63));
        Assert.True(bot.RunUntilObjective("ford", 4000), bot.Diagnostics());
        Assert.Contains(m.Host.Messages, x => x.Text.Contains("answering"));
        Assert.Empty(m.Host.Errors);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void A_bot_crosses_the_river_and_destroys_the_device(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);
        Simulation s = m.Sim;

        // build a strike force and two Turrets first, then the assault group is every unit that exists; later units stay home for the raids.
        for (int i = 0; i < 5; i++) bot.Train("auth_tank");
        for (int i = 0; i < 6; i++) bot.Train("auth_rifleman");
        Assert.True(bot.BuildAndPlace("auth_turret", TurretSpots, 900), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("auth_turret", TurretSpots, 900), bot.Diagnostics());
        Assert.True(bot.RunUntil(() => bot.Count("auth_tank") >= 8 && bot.Count("auth_rifleman") >= 12, 3000, 10),
            bot.Diagnostics() + " bulwarks " + bot.Count("auth_bulwark") + " tanks " + bot.Count("auth_tank") + " rifles " + bot.Count("auth_rifleman"));

        var group = new List<(EntityId Id, ActorKind Kind)>();
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0) group.Add((s.Units.Slots.IdOf(i), ActorKind.Vehicle));
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0) group.Add((s.Infantry.Slots.IdOf(i), ActorKind.Infantry));

        int stage = 0;
        void March()
        {
            CellCoord target = stage == 0 ? new CellCoord(47, 64) : stage == 1 ? new CellCoord(47, 78) : new CellCoord(47, 86);
            foreach ((EntityId id, ActorKind kind) in group)
            {
                bool alive = kind == ActorKind.Vehicle ? s.Units.Slots.IsAlive(id) : s.Infantry.Slots.IsAlive(id);
                if (alive) bot.Send(CommandKind.AttackMove, id, target, (int)kind);
            }
        }

        // cross to a staging point on the south bank, regroup, push to the outer line, then the device.
        March();
        for (int i = 0; i < 6; i++) bot.Train("auth_rifleman");
        bot.Train("auth_tank");
        int marched = s.Tick;
        for (int t = 0; t < TickLimit && !s.GameOver; t += 100)
        {
            int since = s.Tick - marched;
            int want = since > 2600 ? 2 : since > 1500 ? 1 : 0;
            if (want != stage) stage = want;
            if (t % 200 == 0) March();
            if (t % 900 == 0) bot.Train("auth_rifleman");
            bot.Idle(99);
        }

        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics() + " bulwarks " + bot.Count("auth_bulwark") + " tanks " + bot.Count("auth_tank") + " rifles " + bot.Count("auth_rifleman") + " yard " + bot.Count("auth_conyard"));
        Assert.Empty(m.Host.Errors);
        Assert.True(s.Tick < TickLimit, bot.Diagnostics());
        Assert.Equal(0, s.WinningTeam);
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }
}
