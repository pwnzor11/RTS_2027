// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Game.Tests.Saves;
using Rts.Scripting;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S18;

/// <summary>
/// S18 MIS-01 / MIS-05: the mission screen ends on the script's outcome or the human's defeat (<see cref="MissionGame.IsOver"/>), never on the
/// sim's elimination rule alone. The game screens read exactly these two members.
/// </summary>
public sealed class CampaignMissionEndTests
{
    private static void RunUntilOutcome(MissionGame game, int limit)
    {
        for (int i = 0; i < limit && game.Host.Outcome == MissionOutcome.None; i++) game.Sim.Step(Array.Empty<Command>());
    }

    [Fact]
    public void Losing_auth03_to_the_tide_reaches_the_Defeat_flow()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "auth_03");
        for (int i = 0; i < 20; i++) game.Sim.Step(Array.Empty<Command>());
        CampaignTestKit.RunScriptCallback(game.Host, "TideTurns");   // the 9000-tick tide timer's own callback (Mission.Lose(0))
        game.Sim.Step(Array.Empty<Command>());
        Assert.Empty(game.Host.Errors);
        AssertDefeatFlow(game);
    }

    [Fact]
    public void Losing_lb03_when_idle_reaches_the_Defeat_flow()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "lb_03");
        RunUntilOutcome(game, 40000);
        AssertDefeatFlow(game);
    }

    private static void AssertDefeatFlow(MissionGame game)
    {
        Assert.Equal(MissionOutcome.Lost, game.Host.Outcome);
        Assert.False(game.Sim.GameOver, "the finding's premise: the sim alone never ends this mission");
        Assert.True(game.IsOver);
        Assert.False(game.LocalWon);
    }

    [Fact]
    public void Razing_exp01s_only_enemy_building_does_not_end_the_mission()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "exp_01");
        for (int i = 0; i < 5; i++) game.Sim.Step(Array.Empty<Command>());
        int power = SimRules.IndexOf(game.Sim.Setup.Rules.Buildings, "auth_power", b => b.Id);
        int enemyBuildings = 0;
        for (int i = 1; i < game.Sim.Buildings.Capacity; i++)
        {
            if (!game.Sim.Buildings.Slots.IsIndexAlive(i) || game.Sim.Buildings.Player[i] != 1) continue;
            enemyBuildings++;
            Assert.Equal(power, game.Sim.Buildings.TypeIndex[i]);
            game.Sim.Buildings.Health[i] = 0;   // test-only direct kill
        }

        Assert.Equal(1, enemyBuildings);
        for (int i = 0; i < 200; i++) game.Sim.Step(Array.Empty<Command>());
        Assert.True(game.Sim.GameOver, "the finding's premise: the sim's elimination rule fires");
        Assert.Equal(MissionOutcome.None, game.Host.Outcome);
        Assert.False(game.IsOver);
        Assert.Empty(game.Host.Errors);
    }

    [Fact]
    public void A_scripted_win_ends_the_mission_with_Victory()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "exp_01");
        game.Sim.Step(Array.Empty<Command>());
        Assert.False(game.IsOver);
        CampaignTestKit.ForceWin(game.Host, game.Sim);
        Assert.True(game.IsOver);
        Assert.True(game.LocalWon);
    }
}
