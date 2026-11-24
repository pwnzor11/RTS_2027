// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Xunit;

namespace Rts.Game.Tests.A43Q2;

/// <summary>QOL-10: the order-point accessor behind the selected-unit lines, and the setting that turns them on and off.</summary>
public sealed class OrderLinesTests
{
    private readonly string _rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    private readonly SimSnapshotSource _source;

    public OrderLinesTests()
    {
        _source = new SimSnapshotSource(SkirmishGame.Create(_rulesDir, 0, 1), _rulesDir);
    }

    [Fact]
    public void An_idle_unit_has_no_order_line()
    {
        CellCoord start = _source.Map.StartPositions[0];
        EntityId id = _source.Sim.SpawnInfantry(0, 0, new CellCoord(start.X + 3, start.Y + 6));
        _source.StepTicks(1);
        Assert.False(_source.Sim.TryOrderPoint(new ActorRef(ActorKind.Infantry, id), out _, out _));
    }

    [Fact]
    public void A_moving_unit_points_at_its_destination_as_a_move_line()
    {
        CellCoord start = _source.Map.StartPositions[0];
        EntityId id = _source.Sim.SpawnInfantry(0, 0, new CellCoord(start.X + 3, start.Y + 6));
        _source.StepTicks(1);
        _source.Order(CommandKind.Move, new UnitKey(ActorKind.Infantry, id), new CellCoord(start.X + 8, start.Y + 6));
        _source.StepTicks(2);
        bool has = _source.Sim.TryOrderPoint(new ActorRef(ActorKind.Infantry, id), out LeptonCoord point, out bool attack);
        Assert.True(has);
        Assert.False(attack);
        Assert.NotEqual(default, point);
    }

    [Fact]
    public void Order_lines_are_on_by_default_and_survive_a_save()
    {
        Assert.True(Settings.Default.ShowOrderLines);
        string path = Path.Combine(Path.GetTempPath(), "a43q2_order_lines_" + Guid.NewGuid().ToString("N") + ".toml");
        try
        {
            (Settings.Default with { ShowOrderLines = false }).Save(path);
            Assert.False(Settings.Load(path).ShowOrderLines);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
