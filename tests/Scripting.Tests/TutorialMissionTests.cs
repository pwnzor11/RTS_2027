// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>The shared tutorial (tut_01) loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> walks every lesson to a win.</summary>
public sealed class TutorialMissionTests
{
    private const string Mission = "tut_01";
    private const int TickLimit = 20000;

    [Fact]
    public void Idle_player_gets_no_script_errors_no_loss_and_only_the_first_lesson()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(3000);
        Assert.Empty(m.Host.Errors);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.Equal(MissionOutcome.None, m.Host.Outcome);
        Assert.Equal(7, m.Host.Objectives.Count);
        Assert.Single(m.Host.VisibleObjectives);
        Assert.Equal("authority", m.Def.Faction);
        Assert.Equal(2, m.Def.Schema);
    }

    [Fact]
    public void A_bot_completes_every_lesson_in_order_and_wins()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        var yard = new CellCoord(3, 3);

        // 1. select and move: walk the squad onto the rally ground.
        bot.OrderAll(CommandKind.Move, new CellCoord(12, 5));
        Assert.True(bot.RunUntilObjective("move", TickLimit), bot.Diagnostics());

        // 2. attack: attack-move onto the practice power plant.
        bot.OrderAll(CommandKind.AttackMove, new CellCoord(18, 5));
        Assert.True(bot.RunUntilObjective("attack", TickLimit), bot.Diagnostics());

        // 3 and 4. power plant, then barracks, each placed beside the yard.
        CellCoord[] spots = Around(yard, 8);
        Assert.True(bot.BuildAndPlace("auth_power", spots), bot.Diagnostics());
        Assert.True(bot.RunUntilObjective("power", TickLimit), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("auth_barracks", spots), bot.Diagnostics());
        Assert.True(bot.RunUntilObjective("barracks", TickLimit), bot.Diagnostics());

        // 5. train infantry.
        bot.Train("auth_rifleman");
        Assert.True(bot.RunUntilObjective("train", TickLimit), bot.Diagnostics());
        bot.Train("auth_rifleman");

        // 6. refinery (free scraper) and the first delivery.
        Assert.True(bot.BuildAndPlace("auth_refinery", spots), bot.Diagnostics());
        Assert.True(bot.RunUntilObjective("harvest", TickLimit), bot.Diagnostics());

        // 7. defeat the camp.
        for (int guard = 0; guard < TickLimit && !m.Sim.GameOver && !bot.Done("fight"); guard += 100)
        {
            bot.OrderAll(CommandKind.AttackMove, new CellCoord(26, 3));
            bot.Idle(99);
        }

        Assert.Empty(m.Host.Errors);
        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics());
        Assert.True(m.Sim.Tick < TickLimit, bot.Diagnostics());
        Assert.All(m.Host.Objectives, o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }

    /// <summary>Cells in rings around a 3x3 building at <paramref name="origin"/>, nearest first, two cells apart.</summary>
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
