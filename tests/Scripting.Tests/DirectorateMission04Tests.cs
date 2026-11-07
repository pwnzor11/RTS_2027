// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>dir_04 "The Sunrise": a scripted player holds the tunnel head, fires the Device and wins; an idle player does not.</summary>
public sealed class DirectorateMission04Tests
{
    private static readonly CellCoord Head = new(60, 57);
    private static readonly CellCoord Ridge = new(60, 65);

    /// <summary>The id of the player's Sunrise Device building.</summary>
    private static EntityId Device(Simulation s)
    {
        int type = SimRules.IndexOf(s.Setup.Rules.Buildings, "dir_sunrise", x => x.Id);
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == 0 && s.Buildings.TypeIndex[i] == type)
                return s.Buildings.Slots.IdOf(i);
        return default;
    }

    [Fact]
    public void Idle_player_runs_without_script_errors_and_does_not_win()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_04");
        var bot = new MissionBot(m);
        bot.Idle(3000);
        Assert.Empty(m.Host.Errors);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
        Assert.Equal(2, m.Host.VisibleObjectives.Count());
        Assert.Equal(3, m.Host.Objectives.Count);   // plus the hidden "west" bonus
    }

    [Fact]
    public void Idle_player_who_never_fires_does_not_win_even_when_the_device_is_charged()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_04");
        var bot = new MissionBot(m);
        bot.Idle(3400);
        Assert.Empty(m.Host.Errors);
        Assert.True(bot.Done("hold") || m.Host.Outcome == MissionOutcome.Lost, bot.Diagnostics());
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
    }

    [Fact]
    public void Schema_two_fields_are_set()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_04");
        Assert.Equal("directorate", m.Def.Faction);
        Assert.Equal(4, m.Def.Act);
        Assert.Empty(m.Def.Next);   // the finale has no next mission
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void A_simple_player_can_win(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_04");
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);

        for (int guard = 0; guard < 30000 && !bot.Sim.GameOver; guard++)
        {
            if (guard % 200 == 0 && bot.Cash > 800)
                for (int i = 0; i < 5; i++) bot.Train("dir_conscript");   // the queue holds only a few items: keep topping it up
            if (guard % 100 == 0 && guard >= 600) bot.OrderAll(CommandKind.AttackMove, Head);
            if (guard >= 3100 && guard % 50 == 0)
            {
                EntityId device = Device(bot.Sim);
                if (device != default) bot.Send(CommandKind.Special, device, Ridge, 0);
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
