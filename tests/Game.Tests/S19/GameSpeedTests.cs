// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Game.Tests.S19;

/// <summary>M13-15 item 1 (D-205): single-player game speed 0.5x .. 2x. The sim step is fixed; only steps per real second change.</summary>
public sealed class GameSpeedTests
{
    private const long Tick = SimHost.TickMicroseconds;

    private static SimHost NewHost()
    {
        string rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        RuleSet rules = RulesLoader.Load(rulesDir);
        MapData map = MapFile.Load(Path.Combine(rulesDir, "..", "maps", SandboxScenario.MapFileName));
        return new SimHost(new Simulation(new SimSetup(1, map, 2, rules.Sim, rules.RulesHash)));
    }

    [Fact]
    public void The_speeds_are_half_three_quarters_one_one_and_a_half_and_two()
    {
        Assert.Equal(new[] { 50, 75, 100, 150, 200 }, GameSpeed.Percents);
        Assert.Equal(new[] { "0.5x", "0.75x", "1x", "1.5x", "2x" }, GameSpeed.Percents.Select(GameSpeed.Label));
        Assert.Equal(100, Settings.Default.GameSpeedPercent);
    }

    [Fact]
    public void Step_moves_one_place_and_stops_at_the_ends()
    {
        Assert.Equal(150, GameSpeed.Step(100, 1));
        Assert.Equal(75, GameSpeed.Step(100, -1));
        Assert.Equal(200, GameSpeed.Step(200, 1));
        Assert.Equal(50, GameSpeed.Step(50, -1));
        Assert.Equal(100, GameSpeed.Snap(97));
    }

    [Fact]
    public void A_lan_game_is_locked_to_normal_speed()
    {
        Assert.Equal(100, GameSpeed.Effective(50, networked: true));
        Assert.Equal(50, GameSpeed.Effective(50, networked: false));
    }

    [Theory]
    [InlineData(50, 10, 5)]
    [InlineData(75, 20, 15)]
    [InlineData(100, 20, 20)]
    [InlineData(150, 20, 30)]
    [InlineData(200, 20, 40)]
    public void Steps_per_real_time_follow_the_speed_while_one_step_stays_one_step(int percent, int realTicks, int expectedSteps)
    {
        SimHost host = NewHost();
        host.SpeedPercent = percent;
        int steps = 0;
        for (int i = 0; i < realTicks; i++) steps += host.Advance(Tick);
        Assert.Equal(expectedSteps, steps);
        Assert.Equal(expectedSteps, host.Tick);
    }

    [Fact]
    public void A_slow_game_reaches_the_same_state_hash_at_the_same_tick()
    {
        SimHost slow = NewHost(), normal = NewHost();
        slow.SpeedPercent = 50;
        while (slow.Tick < 20) slow.Advance(Tick / 3);
        normal.StepTicks(slow.Tick);
        Assert.Equal(normal.Sim.ComputeStateHash(), slow.Sim.ComputeStateHash());
    }

    [Fact]
    public void Speed_percent_is_a_setting_that_round_trips_and_falls_back_to_normal()
    {
        Assert.Equal(75, Settings.Parse((Settings.Default with { GameSpeedPercent = 75 }).ToToml()).GameSpeedPercent);
        Assert.Equal(100, Settings.Parse("version = 2").GameSpeedPercent);
        Assert.Equal(200, Settings.Parse("game_speed_percent = 9999").GameSpeedPercent);
    }

    [Fact]
    public void The_speed_keys_are_bound_without_a_conflict()
    {
        Assert.Empty(KeyBindings.Default.Conflicts());
        Assert.NotEqual(KeyBindings.Default.Get(GameAction.GameSpeedUp), KeyBindings.Default.Get(GameAction.GameSpeedDown));
    }
}
