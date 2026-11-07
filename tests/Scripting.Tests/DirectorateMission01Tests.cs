// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>dir_01 "The First Hatch": a scripted player wins it at two tiers, an idle player does not.</summary>
public sealed class DirectorateMission01Tests
{
    private static readonly CellCoord Site = new(30, 25);

    [Fact]
    public void Idle_player_runs_without_script_errors_and_does_not_win()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_01");
        var bot = new MissionBot(m);
        bot.Idle(3000);
        Assert.Empty(m.Host.Errors);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
        Assert.Equal(2, m.Host.VisibleObjectives.Count());
        Assert.Equal(3, m.Host.Objectives.Count);   // plus the hidden "picket" bonus
    }

    [Fact]
    public void Schema_two_fields_are_set()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_01");
        Assert.Equal("directorate", m.Def.Faction);
        Assert.Equal(1, m.Def.Act);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void A_simple_player_can_win(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_01");
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);

        var vents = new[] { new CellCoord(27, 24), new CellCoord(27, 26), new CellCoord(23, 29), new CellCoord(20, 25) };
        Assert.True(bot.BuildAndPlace("dir_reactor_vent", vents), bot.Diagnostics());
        var hatchSpots = new List<CellCoord>();
        for (int y = 23; y <= 27; y++) for (int x = 26; x <= 31; x++) hatchSpots.Add(new CellCoord(x, y));
        Assert.True(bot.BuildAndPlace("dir_hatch", hatchSpots), bot.Diagnostics());
        Assert.True(bot.RunUntilObjective("hatch", 1000), bot.Diagnostics());

        var muster = new[] { new CellCoord(20, 29), new CellCoord(27, 20), new CellCoord(20, 21), new CellCoord(17, 25), new CellCoord(23, 17) };
        Assert.True(bot.BuildAndPlace("dir_muster_hatch", muster), bot.Diagnostics());
        for (int i = 0; i < 14; i++) bot.Train("dir_conscript");
        for (int i = 0; i < 4; i++) bot.Train("dir_ashthrower");
        var pill = new[] { new CellCoord(30, 23), new CellCoord(30, 27), new CellCoord(28, 22), new CellCoord(28, 28) };
        bot.BuildAndPlace("dir_pillbox", pill, 1500);

        for (int guard = 0; guard < 20000 && !bot.Sim.GameOver; guard++)
        {
            if (guard % 100 == 0) bot.OrderAll(CommandKind.AttackMove, Site);
            bot.Idle();
        }

        Assert.Empty(m.Host.Errors);
        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics());
        Assert.True(bot.Sim.Tick < 20000, bot.Diagnostics());
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }
}
