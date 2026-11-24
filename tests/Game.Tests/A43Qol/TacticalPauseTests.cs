// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.A43Qol;

/// <summary>A43-QOL-08: orders given while paused wait, are listed for the screen, and run on the first ticks after unpause.</summary>
public sealed class TacticalPauseTests : IDisposable
{
    private readonly string _rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    private readonly string _replay = Path.Combine(Path.GetTempPath(), "rts_a43pause_" + Guid.NewGuid().ToString("N") + ".rpl");
    private readonly SimSnapshotSource _source;
    private readonly UnitKey _jeep;

    public TacticalPauseTests()
    {
        _source = new SimSnapshotSource(SkirmishGame.Create(_rulesDir, 0, 1), _rulesDir, _replay);
        int jeep = SimRules.IndexOf(_source.Rules.Vehicles, "auth_jeep", v => v.Id);
        CellCoord at = _source.Map.StartPositions[0];
        _jeep = new UnitKey(ActorKind.Vehicle, _source.Sim.SpawnVehicle(0, jeep, new CellCoord(at.X + 4, at.Y + 4)));
        _source.Host.RefreshSnapshots();
    }

    public void Dispose()
    {
        _source.SaveReplay();
        foreach (string f in new[] { _replay, _replay + ".prev", _replay + ".map.toml" })
            if (File.Exists(f)) File.Delete(f);
    }

    [Fact]
    public void Orders_given_while_paused_wait_and_run_after_unpause()
    {
        _source.Paused = true;
        CellCoord from = _source.Map.StartPositions[0];
        var target = new CellCoord(from.X + 10, from.Y + 4);
        _source.Order(CommandKind.Move, _jeep, target);
        int tick = _source.Tick;

        Assert.Equal(0, _source.Advance(10 * SimHost.TickMicroseconds));   // nothing runs while paused
        Assert.Equal(tick, _source.Tick);
        Command waiting = Assert.Single(_source.Host.Pending, c => c.Kind == CommandKind.Move);
        Assert.Equal(tick + SimHost.CommandDelayTicks, waiting.ExecTick);

        _source.TryGetPosition(_jeep, out LeptonCoord before);
        _source.Paused = false;
        _source.StepTicks(20);
        _source.TryGetPosition(_jeep, out LeptonCoord after);
        Assert.NotEqual(before, after);   // the held order ran
        Assert.DoesNotContain(_source.Host.Pending, c => c.Kind == CommandKind.Move);
        Assert.Equal(0, _source.Sim.LateCommandCount);
    }

    [Fact]
    public void The_pause_list_shows_the_latest_plain_order_of_each_unit_and_every_queued_waypoint()
    {
        _source.Paused = true;
        var a = new CellCoord(5, 5);
        var b = new CellCoord(9, 9);
        var c = new CellCoord(12, 3);
        _source.Order(CommandKind.Move, _jeep, a);
        _source.Order(CommandKind.AttackMove, _jeep, b);     // replaces the move
        _source.OrderQueued(_jeep, c, attackMove: false);    // adds a waypoint

        List<PausedOrder> plan = PausedOrders.Collect(_source.Host.Pending, 0);
        Assert.Equal(2, plan.Count);
        Assert.Equal((b, true), (plan[0].Cell, plan[0].Attack));
        Assert.Equal((c, false), (plan[1].Cell, plan[1].Attack));
        Assert.Empty(PausedOrders.Collect(_source.Host.Pending, 1));   // another player's orders are never listed
    }

    [Fact]
    public void In_a_LAN_game_commands_go_to_the_outbox_so_nothing_is_held_locally()
    {
        var sent = new List<Command>();
        _source.Host.Outbox = sent.Add;
        _source.Paused = true;
        _source.Order(CommandKind.Move, _jeep, new CellCoord(8, 8));
        Assert.Single(sent);
        Assert.DoesNotContain(_source.Host.Pending, c => c.Kind == CommandKind.Move);
    }
}
