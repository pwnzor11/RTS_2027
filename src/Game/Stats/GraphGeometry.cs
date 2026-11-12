// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Stats;

/// <summary>M10-17: the pure maths that places a metric's lines in a plot rectangle (integer pixels), shared by the screen and the tests.</summary>
public static class GraphGeometry
{
    /// <summary>A "nice" top for the y axis: the smallest of 1, 2, 5 times a power of ten that is at least <paramref name="max"/> (at least 10).</summary>
    public static int AxisTop(int max)
    {
        if (max <= 10) return 10;
        long step = 1;
        while (step * 10 < max) step *= 10;
        foreach (int m in new[] { 1, 2, 5, 10 })
            if (step * m >= max) return (int)Math.Min(int.MaxValue, step * m);
        return (int)Math.Min(int.MaxValue, step * 10);
    }

    /// <summary>
    /// The polyline of timeline column <paramref name="column"/> for <paramref name="metric"/> inside <paramref name="plot"/>: x runs over
    /// the sample ticks (first at the left edge, last at the right), y from 0 at the bottom to <paramref name="axisTop"/> at the top.
    /// A single sample gives one point at the left edge.
    /// </summary>
    public static Point[] Line(MatchTimeline timeline, int column, GraphMetric metric, Rectangle plot, int axisTop)
    {
        int n = timeline.Count;
        var points = new Point[n];
        if (n == 0) return points;
        int first = timeline.Ticks[0];
        long span = timeline.Ticks[n - 1] - first;
        int top = Math.Max(1, axisTop);
        for (int i = 0; i < n; i++)
        {
            long x = span == 0 ? 0 : (timeline.Ticks[i] - first) * plot.Width / span;
            long v = Math.Clamp(timeline.Value(i, column, metric), 0, top);
            long y = v * plot.Height / top;
            points[i] = new Point(plot.X + (int)x, plot.Bottom - (int)y);
        }

        return points;
    }

    /// <summary>The clock text m:ss of a sim tick.</summary>
    public static string ClockText(int tick)
    {
        int seconds = tick / Rts.Sim.Core.SimConstants.TicksPerSecond;
        return $"{seconds / 60}:{seconds % 60:D2}";
    }
}
