// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game;
using Rts.Game.Host;
using Rts.Game.Input;
using Rts.Game.Sandbox;
using Rts.Game.Ui.Spectator;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Game.Tests.Spectator;

public sealed class SpectatorHostTests
{
    private const long Tick = SimHost.TickMicroseconds;

    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/rules not found");

    private static SimHost SpectatorHost(ulong seed = 7)
    {
        SkirmishGame game = SpectatorSetup.Create(RulesDir, seed, 1);
        return SpectatorSetup.CreateHost(game);
    }

    private static SimHost PlainHost(int players)
    {
        RuleSet rules = RulesLoader.Load(RulesDir);
        MapData map = MapFile.Load(Path.Combine(RulesDir, "..", "maps", SandboxScenario.MapFileName));
        return new SimHost(new Simulation(new SimSetup(1, map, players, rules.Sim, rules.RulesHash)));
    }

    private static InputFrame Pressed(params Keys[] keys) =>
        new(new KeyboardState(keys), new KeyboardState(), new MouseState(), new MouseState());

    // ---- viewer ----

    [Fact]
    public void Viewer_cycles_omniscient_then_every_player_for_two_players()
    {
        SimHost host = PlainHost(2);
        Assert.Equal(0, host.Viewer);
        var seen = new List<int>();
        for (int i = 0; i < 6; i++) { host.CycleViewer(); seen.Add(host.Viewer); }
        Assert.Equal(new[] { 1, -1, 0, 1, -1, 0 }, seen);
    }

    [Fact]
    public void Viewer_cycle_covers_all_four_players()
    {
        SimHost host = PlainHost(4);
        host.SetViewer(-1);
        var seen = new List<int>();
        for (int i = 0; i < 5; i++) { host.CycleViewer(); seen.Add(host.Viewer); }
        Assert.Equal(new[] { 0, 1, 2, 3, -1 }, seen);
    }

    [Fact]
    public void SetViewer_clamps_to_the_valid_range_and_rebuilds_the_snapshot()
    {
        SimHost host = PlainHost(2);
        host.SetViewer(9);
        Assert.Equal(1, host.Viewer);
        host.SetViewer(-7);
        Assert.Equal(-1, host.Viewer);
        Assert.Equal(-1, host.Current.Viewer);
    }

    [Fact]
    public void Changing_the_viewer_does_not_change_the_state_hash()
    {
        SimHost steady = SpectatorHost();
        SimHost cycling = SpectatorHost();
        for (int block = 0; block < 20; block++)
        {
            steady.StepTicks(25);
            cycling.StepTicks(25);
            cycling.CycleViewer();
            if (block % 3 == 0) cycling.SetViewer(-1);
            Assert.Equal(steady.Sim.ComputeStateHash(), cycling.Sim.ComputeStateHash());
        }

        Assert.Equal(steady.Sim.RecentHashes, cycling.Sim.RecentHashes);
    }

    [Fact]
    public void Full_vision_sees_at_least_as_much_as_a_players_fog()
    {
        SimHost host = SpectatorHost();
        host.StepTicks(200);
        host.SetViewer(0);
        int fogged = host.Current.Units.Count + host.Current.Buildings.Count;
        host.SetViewer(-1);
        int full = host.Current.Units.Count + host.Current.Buildings.Count;
        Assert.True(full >= fogged);
        Assert.True(full > 0);
    }

    // ---- speed ----

    [Fact]
    public void Speed_steps_are_one_two_four_eight()
    {
        SimHost host = PlainHost(2);
        Assert.Equal(new[] { 1, 2, 4, 8 }, SimHost.SpeedSteps);
        for (int i = 0; i < 4; i++)
        {
            host.SetSpeedStep(i);
            Assert.Equal(SimHost.SpeedSteps[i], host.SpeedMultiplier);
            Assert.Equal(SimHost.SpeedSteps[i], host.Advance(Tick));
        }
    }

    [Fact]
    public void Eight_times_speed_and_headless_stepping_give_equal_hashes()
    {
        SimHost fast = SpectatorHost();
        fast.SetSpeedStep(3);
        while (fast.Tick < 1600) fast.Advance(Tick);
        SimHost headless = SpectatorHost();
        headless.StepTicks(fast.Tick);

        Assert.Equal(1600, fast.Tick);
        Assert.Equal(headless.Sim.RecentHashes, fast.Sim.RecentHashes);
        Assert.Equal(headless.Sim.ComputeStateHash(), fast.Sim.ComputeStateHash());
    }

    [Fact]
    public void Changing_speed_and_pausing_mid_game_does_not_change_the_hash()
    {
        SimHost varied = SpectatorHost();
        int[] intervals = { 100, 50, 25, 25 };   // 100 + 100 + 100 + 200 ticks
        for (int step = 0; step < 4; step++)
        {
            varied.SetSpeedStep(step);
            for (int i = 0; i < intervals[step]; i++)
            {
                varied.Advance(Tick);
                if (i % 7 == 0) { varied.Paused = true; varied.Advance(5 * Tick); varied.Paused = false; }
            }
        }

        SimHost headless = SpectatorHost();
        headless.StepTicks(varied.Tick);
        Assert.Equal(500, varied.Tick);
        Assert.Equal(headless.Sim.RecentHashes, varied.Sim.RecentHashes);
        Assert.Equal(headless.Sim.ComputeStateHash(), varied.Sim.ComputeStateHash());
    }

    [Fact]
    public void Catch_up_cap_still_applies_at_eight_times()
    {
        SimHost host = SpectatorHost();
        host.SetSpeedStep(3);
        int cappedIntervals = (int)(SimHost.MaxAccumulatedMicroseconds / Tick);
        Assert.Equal(cappedIntervals * 8, host.Advance(60_000_000));
    }

    // ---- input lock ----

    [Fact]
    public void Spectator_host_refuses_orders_and_commands()
    {
        SimHost host = SpectatorHost();
        Assert.True(host.Spectator);
        var unit = new UnitKey(ActorKind.Vehicle, new EntityId(1, 1));
        host.IssueOrder(0, CommandKind.Move, unit, new CellCoord(3, 3));
        host.Issue(new Command(0, 1, 0, CommandKind.Stop, EntityId.None, EntityId.None, default, 0, 0));
        Assert.Empty(host.Pending);
        Assert.Equal(2, host.RefusedCommands);
    }

    [Fact]
    public void Refused_orders_leave_the_game_identical_to_one_never_asked()
    {
        SimHost asked = SpectatorHost();
        SimHost quiet = SpectatorHost();
        for (int i = 0; i < 100; i++)
        {
            asked.IssueOrder(0, CommandKind.Move, new UnitKey(ActorKind.Infantry, new EntityId(1, 1)), new CellCoord(5, 5));
            asked.StepTicks(5);
            quiet.StepTicks(5);
        }

        Assert.Equal(quiet.Sim.RecentHashes, asked.Sim.RecentHashes);
        Assert.Equal(quiet.Sim.ComputeStateHash(), asked.Sim.ComputeStateHash());
    }

    [Fact]
    public void A_normal_host_still_accepts_orders()
    {
        SimHost host = PlainHost(2);
        Assert.False(host.Spectator);
        host.IssueOrder(0, CommandKind.Stop, new UnitKey(ActorKind.Vehicle, EntityId.None));
        Assert.Single(host.Pending);
        Assert.Equal(0, host.RefusedCommands);
    }

    // ---- keys ----

    [Fact]
    public void Keys_drive_viewer_vision_speed_and_pause()
    {
        SimHost host = SpectatorHost();
        var c = new SpectatorController(host);
        Assert.Equal(-1, host.Viewer);

        c.Handle(Pressed(Keys.V));
        Assert.Equal(0, host.Viewer);
        c.Handle(Pressed(Keys.V));
        Assert.Equal(1, host.Viewer);

        c.Handle(Pressed(Keys.F2));              // fog -> full vision, remembering player 1
        Assert.Equal(-1, host.Viewer);
        c.Handle(Pressed(Keys.F2));              // full vision -> the remembered fog
        Assert.Equal(1, host.Viewer);

        c.Pick(0);
        c.Handle(Pressed(Keys.F2));
        c.Handle(Pressed(Keys.F2));
        Assert.Equal(1, host.Viewer);            // leaving fog 1 remembered 1 again
        c.Handle(Pressed(Keys.V)); c.Handle(Pressed(Keys.V));
        Assert.Equal(0, host.Viewer);

        c.Handle(Pressed(Keys.D3));
        Assert.Equal(4, host.SpeedMultiplier);
        c.Handle(Pressed(Keys.D4));
        Assert.Equal(8, host.SpeedMultiplier);
        c.Handle(Pressed(Keys.D1));
        Assert.Equal(1, host.SpeedMultiplier);
        c.Handle(Pressed(Keys.D2));
        Assert.Equal(2, host.SpeedMultiplier);

        c.Handle(Pressed(Keys.Space));
        Assert.True(host.Paused);
        Assert.Equal("PAUSED", c.SpeedLabel);
        c.Handle(Pressed(Keys.Space));
        Assert.False(host.Paused);
        Assert.Equal("2x", c.SpeedLabel);
    }

    [Fact]
    public void Picking_a_row_sets_the_fog_that_F2_shows()
    {
        SimHost host = SpectatorHost();
        var c = new SpectatorController(host);
        c.Pick(1);
        c.ToggleVision();
        Assert.Equal(1, host.Viewer);
        Assert.Equal("P2 fog", c.ViewLabel);
        c.ToggleVision();
        Assert.Equal("full vision", c.ViewLabel);
    }

    // ---- a headless two-AI game ----

    [Fact]
    public void Two_ai_spectator_game_runs_thousands_of_ticks_and_is_deterministic()
    {
        SimHost a = SpectatorHost(11);
        SimHost b = SpectatorHost(11);
        SimHost idle = SpectatorHost(11);
        idle.Ai = null;
        a.StepTicks(3000);
        b.StepTicks(3000);
        idle.StepTicks(3000);

        Assert.Equal(3000, a.Tick);
        Assert.Equal(a.Sim.ComputeStateHash(), b.Sim.ComputeStateHash());
        Assert.Equal(a.Sim.RecentHashes, b.Sim.RecentHashes);
        Assert.NotEmpty(a.Sim.RecentHashes);
        Assert.NotEqual(idle.Sim.ComputeStateHash(), a.Sim.ComputeStateHash());   // the AIs did something
        Assert.Equal(0, a.RefusedCommands);

        IReadOnlyList<SpectatorRow> rows = SpectatorTable.Collect(a.Sim);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.True(r.Structures > 0 || r.Defeated));
    }

    [Fact]
    public void Both_players_have_an_ai_and_no_human()
    {
        SkirmishGame game = SpectatorSetup.Create(RulesDir, 3, 1);
        Assert.Equal(2, game.Ai.Count);
    }
}
