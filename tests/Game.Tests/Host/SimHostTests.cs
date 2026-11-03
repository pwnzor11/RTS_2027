// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Game.Tests.Host;

public sealed class SimHostTests
{
    private const long Tick = SimHost.TickMicroseconds;

    private static SimHost NewHost()
    {
        string rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("data/rules not found");
        RuleSet rules = RulesLoader.Load(rulesDir);
        MapData map = MapFile.Load(Path.Combine(rulesDir, "..", "maps", SandboxScenario.MapFileName));
        var setup = new SimSetup(1, map, 2, rules.Sim, rules.RulesHash);
        return new SimHost(new Simulation(setup));
    }

    private static UnitKey Nobody => new(ActorKind.Vehicle, EntityId.None);

    [Fact]
    public void Advance_steps_whole_ticks_and_keeps_the_remainder()
    {
        SimHost host = NewHost();
        Assert.Equal(0, host.Advance(Tick - 1));
        Assert.Equal(0, host.Tick);
        Assert.Equal(1, host.Advance(1));
        Assert.Equal(1, host.Tick);
        Assert.Equal(2, host.Advance(2 * Tick));
        Assert.Equal(3, host.Tick);
    }

    [Fact]
    public void Advance_caps_the_catch_up_after_a_stall()
    {
        SimHost host = NewHost();
        int stepped = host.Advance(60_000_000);
        Assert.Equal((int)(SimHost.MaxAccumulatedMicroseconds / Tick), stepped);
        Assert.Equal(stepped, host.Tick);
    }

    [Fact]
    public void Paused_host_does_not_step_and_drops_accumulated_time()
    {
        SimHost host = NewHost();
        host.Advance(Tick / 2);
        host.Paused = true;
        Assert.Equal(0, host.Advance(10 * Tick));
        Assert.Equal(0, host.Tick);
        Assert.Equal(1f, host.Alpha);
        host.Paused = false;
        Assert.Equal(0, host.Advance(Tick / 2)); // the half tick before the pause was dropped, not kept
    }

    [Fact]
    public void Speed_multiplier_runs_extra_ticks_per_interval()
    {
        SimHost host = NewHost();
        host.SpeedMultiplier = 3;
        Assert.Equal(3, host.Advance(Tick));
        Assert.Equal(3, host.Tick);
        host.SpeedMultiplier = 0; // clamped to 1
        Assert.Equal(1, host.SpeedMultiplier);
    }

    [Fact]
    public void StepOnce_steps_exactly_one_tick_and_moves_the_snapshots()
    {
        SimHost host = NewHost();
        host.Paused = true;
        host.StepOnce();
        Assert.Equal(1, host.Tick);
        Assert.Equal(1, host.Current.Tick);
        Assert.Equal(0, host.Previous.Tick);
    }

    [Fact]
    public void Issue_stamps_exec_tick_with_the_command_delay()
    {
        SimHost host = NewHost();
        Command first = host.IssueOrder(0, CommandKind.Stop, Nobody);
        Assert.Equal(0 + SimHost.CommandDelayTicks, first.ExecTick);

        host.StepTicks(5);
        Command later = host.IssueOrder(0, CommandKind.Stop, Nobody);
        Assert.Equal(5 + SimHost.CommandDelayTicks, later.ExecTick);

        Command scripted = host.IssueOrder(1, CommandKind.Stop, Nobody, execTick: 40);
        Assert.Equal(40, scripted.ExecTick);

        Command tooEarly = host.Issue(new Command(2, 0, 0, CommandKind.Stop, EntityId.None, EntityId.None, default, 0));
        Assert.Equal(5 + SimHost.CommandDelayTicks, tooEarly.ExecTick);
    }

    [Fact]
    public void Sequence_numbers_count_per_player()
    {
        SimHost host = NewHost();
        ushort[] seq =
        {
            host.IssueOrder(0, CommandKind.Stop, Nobody, execTick: 50).Sequence,
            host.IssueOrder(0, CommandKind.Stop, Nobody, execTick: 50).Sequence,
            host.IssueOrder(1, CommandKind.Stop, Nobody, execTick: 50).Sequence,
            host.IssueOrder(0, CommandKind.Stop, Nobody, execTick: 50).Sequence,
            host.IssueOrder(1, CommandKind.Stop, Nobody, execTick: 50).Sequence,
        };
        Assert.Equal(new ushort[] { 0, 1, 0, 2, 1 }, seq);
    }

    [Fact]
    public void Commands_leave_the_pending_list_once_their_tick_has_run()
    {
        SimHost host = NewHost();
        host.IssueOrder(0, CommandKind.Stop, Nobody);
        host.IssueOrder(0, CommandKind.Stop, Nobody, execTick: 10);
        Assert.Equal(2, host.Pending.Count);
        host.StepTicks(2);
        Assert.Single(host.Pending);
        host.StepTicks(9);
        Assert.Empty(host.Pending);
    }
}
