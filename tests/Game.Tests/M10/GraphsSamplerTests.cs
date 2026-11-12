// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Stats;
using Rts.Game.Ui.Spectator;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M10;

public sealed class GraphsSamplerTests
{
    private static SpectatorRow Row(int player, int army, int harvested, int kills, int structures, int upgrades, int shipment = 0) =>
        new(player, player, Faction.Authority, 1, 0, 0, 0, army, structures, 0, 0,
            new PlayerStats(0, 0, 0, 0, kills, harvested, 0, shipment), upgrades, false);

    private static IReadOnlyList<SpectatorRow> Rows(int k) => new[] { Row(0, 100 * k, 50 * k, k, k, 0, 10), Row(1, 200 * k, 30 * k, 2 * k, 1, k) };

    [Fact]
    public void Samples_at_tick_zero_then_every_ten_seconds()
    {
        var sampler = new StatsSampler();
        int calls = 0;
        for (int tick = 0; tick <= 3 * StatsSampler.IntervalTicks; tick += SimConstants.TicksPerSecond)
            sampler.Observe(tick, () => { calls++; return Rows(1); });

        Assert.Equal(4, calls);
        Assert.Equal(new[] { 0, 200, 400, 600 }, sampler.Timeline!.Ticks);
        Assert.Equal(200, StatsSampler.IntervalTicks);
    }

    [Fact]
    public void Collect_is_not_called_between_samples()
    {
        var sampler = new StatsSampler();
        sampler.Observe(0, () => Rows(1));
        sampler.Observe(199, () => throw new InvalidOperationException("not due"));
        Assert.Equal(1, sampler.Timeline!.Count);
    }

    [Fact]
    public void A_big_jump_takes_one_sample_and_realigns()
    {
        var sampler = new StatsSampler();
        sampler.Observe(0, () => Rows(1));
        sampler.Observe(450, () => Rows(2));
        sampler.Observe(500, () => throw new InvalidOperationException("not due"));
        sampler.Observe(600, () => Rows(3));
        Assert.Equal(new[] { 0, 450, 600 }, sampler.Timeline!.Ticks);
    }

    [Fact]
    public void Values_map_to_the_metrics()
    {
        var sampler = new StatsSampler();
        sampler.Observe(0, () => Rows(2));
        MatchTimeline t = sampler.Timeline!;
        Assert.Equal(new[] { 0, 1 }, t.Slots);
        Assert.Equal(200, t.Value(0, 0, GraphMetric.ArmyValue));
        Assert.Equal(110, t.Value(0, 0, GraphMetric.Income));   // harvested 100 + shipment 10
        Assert.Equal(2, t.Value(0, 0, GraphMetric.Kills));
        Assert.Equal(2, t.Value(0, 0, GraphMetric.Buildings));
        Assert.Equal(2, t.Value(0, 1, GraphMetric.Upgrades));
        Assert.Equal(400, t.Max(GraphMetric.ArmyValue));
    }

    [Fact]
    public void Finish_adds_a_last_point_once()
    {
        var sampler = new StatsSampler();
        sampler.Observe(0, () => Rows(1));
        sampler.Finish(130, () => Rows(2));
        sampler.Finish(130, () => throw new InvalidOperationException("already sampled"));
        Assert.Equal(new[] { 0, 130 }, sampler.Timeline!.Ticks);
    }

    [Fact]
    public void Going_back_in_time_starts_a_fresh_timeline()
    {
        var sampler = new StatsSampler();
        sampler.Observe(0, () => Rows(1));
        sampler.Observe(400, () => Rows(2));
        sampler.Observe(0, () => Rows(5));
        Assert.Equal(new[] { 0 }, sampler.Timeline!.Ticks);
        Assert.Equal(1000, sampler.Timeline.Max(GraphMetric.ArmyValue));
    }

    [Fact]
    public void Peak_army_is_the_highest_sample()
    {
        var sampler = new StatsSampler();
        sampler.Observe(0, () => Rows(3));
        sampler.Observe(200, () => Rows(1));
        Assert.Equal(300, sampler.Timeline!.PeakArmy(0));
        Assert.Equal(1, sampler.Timeline.ColumnOf(1));
        Assert.Equal(-1, sampler.Timeline.ColumnOf(7));
    }

    [Fact]
    public void Axis_top_rounds_up_to_1_2_5()
    {
        Assert.Equal(10, GraphGeometry.AxisTop(0));
        Assert.Equal(10, GraphGeometry.AxisTop(10));
        Assert.Equal(20, GraphGeometry.AxisTop(11));
        Assert.Equal(50, GraphGeometry.AxisTop(21));
        Assert.Equal(100, GraphGeometry.AxisTop(51));
        Assert.Equal(500, GraphGeometry.AxisTop(300));
        Assert.Equal(2000, GraphGeometry.AxisTop(1001));
        Assert.Equal(int.MaxValue, GraphGeometry.AxisTop(int.MaxValue));
    }

    [Fact]
    public void Line_runs_from_the_left_edge_to_the_right_and_bottom_to_top()
    {
        var sampler = new StatsSampler();
        sampler.Observe(0, () => new[] { Row(0, 0, 0, 0, 0, 0) });
        sampler.Observe(200, () => new[] { Row(0, 50, 0, 0, 0, 0) });
        sampler.Observe(400, () => new[] { Row(0, 100, 0, 0, 0, 0) });
        var plot = new Rectangle(10, 20, 400, 100);
        Point[] line = GraphGeometry.Line(sampler.Timeline!, 0, GraphMetric.ArmyValue, plot, 100);
        Assert.Equal(new[] { new Point(10, 120), new Point(210, 70), new Point(410, 20) }, line);
    }

    [Fact]
    public void Line_clamps_above_the_axis_and_handles_one_sample()
    {
        var sampler = new StatsSampler();
        sampler.Observe(0, () => new[] { Row(0, 500, 0, 0, 0, 0) });
        Point[] one = GraphGeometry.Line(sampler.Timeline!, 0, GraphMetric.ArmyValue, new Rectangle(0, 0, 100, 100), 100);
        Assert.Equal(new[] { new Point(0, 0) }, one);
    }

    [Fact]
    public void Clock_text_is_minutes_and_seconds()
    {
        Assert.Equal("0:00", GraphGeometry.ClockText(0));
        Assert.Equal("1:05", GraphGeometry.ClockText(65 * SimConstants.TicksPerSecond));
    }

    [Fact]
    public void Add_ignores_old_ticks_and_wrong_row_lengths()
    {
        var t = new MatchTimeline(new[] { 0, 1 });
        t.Add(10, new PlayerSample[2]);
        t.Add(10, new PlayerSample[2]);
        t.Add(5, new PlayerSample[2]);
        t.Add(20, new PlayerSample[1]);
        Assert.Equal(1, t.Count);
    }
}
