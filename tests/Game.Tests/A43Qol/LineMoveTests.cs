// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.A43Qol;

/// <summary>A43-QOL-16: a right-drag line sends each selected unit an ordinary Move to its own cell along the line.</summary>
public sealed class LineMoveTests : IDisposable
{
    private readonly string _rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    private readonly string _replay = Path.Combine(Path.GetTempPath(), "rts_a43line_" + Guid.NewGuid().ToString("N") + ".rpl");
    private readonly SimSnapshotSource _source;
    private readonly Selection _selection = new();
    private readonly OrderController _orders;
    private readonly Camera _camera = new();
    private readonly List<UnitKey> _units = new();

    public LineMoveTests()
    {
        _source = new SimSnapshotSource(SkirmishGame.Create(_rulesDir, 0, 1), _rulesDir, _replay);
        _orders = new OrderController(_source, _selection);
        int jeep = SimRules.IndexOf(_source.Rules.Vehicles, "auth_jeep", v => v.Id);
        CellCoord at = _source.Map.StartPositions[0];
        for (int i = 0; i < 4; i++)
            _units.Add(new UnitKey(ActorKind.Vehicle, _source.Sim.SpawnVehicle(0, jeep, new CellCoord(at.X + 2 + i, at.Y + 5))));
        _source.Host.RefreshSnapshots();
        _selection.Replace(_units);
    }

    public void Dispose()
    {
        _source.SaveReplay();
        foreach (string f in new[] { _replay, _replay + ".prev", _replay + ".map.toml" })
            if (File.Exists(f)) File.Delete(f);
    }

    private Point Screen(CellCoord c) => _camera.ScreenFromWorld(Camera.CentreOf(c)).ToPoint();

    [Fact]
    public void A_line_drag_gives_each_unit_its_own_move_cell_along_the_line()
    {
        CellCoord a = _source.Map.StartPositions[0], b = new(a.X + 9, a.Y);
        a = new CellCoord(a.X, a.Y + 8);
        b = new CellCoord(b.X, b.Y + 8);
        int before = _source.Host.Pending.Count;

        Assert.True(_orders.LineMove(Screen(a), Screen(b), _camera));

        List<Command> moves = _source.Host.Pending.Skip(before).ToList();
        Assert.Equal(4, moves.Count);
        Assert.All(moves, m => Assert.Equal(CommandKind.Move, m.Kind));
        Assert.Equal(_units.Select(u => u.Id).OrderBy(i => i.Index), moves.Select(m => m.Subject).OrderBy(i => i.Index));
        Assert.Equal(new[] { a.X, a.X + 3, a.X + 6, a.X + 9 }, moves.Select(m => m.Cell.X).OrderBy(x => x));
        Assert.All(moves, m => Assert.Equal(a.Y, m.Cell.Y));
    }

    [Fact]
    public void A_short_drag_or_a_single_unit_is_left_to_the_plain_right_click()
    {
        CellCoord a = _source.Map.StartPositions[0];
        int before = _source.Host.Pending.Count;
        Assert.False(_orders.LineMove(Screen(a), Screen(new CellCoord(a.X + 1, a.Y)), _camera));
        _selection.Replace(new[] { _units[0] });
        Assert.False(_orders.LineMove(Screen(a), Screen(new CellCoord(a.X + 8, a.Y)), _camera));
        Assert.Equal(before, _source.Host.Pending.Count);
    }

    [Fact]
    public void Attack_move_and_queue_variants_use_the_existing_commands()
    {
        CellCoord a = _source.Map.StartPositions[0];
        int before = _source.Host.Pending.Count;
        Assert.True(_orders.LineMove(Screen(a), Screen(new CellCoord(a.X + 6, a.Y)), _camera, attackMove: true));
        Assert.All(_source.Host.Pending.Skip(before), c => Assert.Equal(CommandKind.AttackMove, c.Kind));
        before = _source.Host.Pending.Count;
        Assert.True(_orders.LineMove(Screen(a), Screen(new CellCoord(a.X + 6, a.Y)), _camera, queue: true));
        Assert.All(_source.Host.Pending.Skip(before), c => Assert.Equal(CommandKind.QueueOrder, c.Kind));
    }
}
