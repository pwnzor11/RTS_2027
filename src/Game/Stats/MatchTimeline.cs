// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Stats;

/// <summary>M10-17: the samples of one match, one row per sample time and one <see cref="PlayerSample"/> per player. Front end only.</summary>
public sealed class MatchTimeline
{
    private readonly List<int> _ticks = new();
    private readonly List<PlayerSample[]> _rows = new();

    /// <summary>Creates a timeline for the players (sim slots) in <paramref name="slots"/>, in the order their samples are given.</summary>
    public MatchTimeline(IReadOnlyList<int> slots) => Slots = slots.ToArray();

    /// <summary>The sim slot of each timeline column.</summary>
    public IReadOnlyList<int> Slots { get; }

    /// <summary>Number of samples taken.</summary>
    public int Count => _ticks.Count;

    /// <summary>Sim tick of each sample, ascending.</summary>
    public IReadOnlyList<int> Ticks => _ticks;

    /// <summary>The tick of the last sample, or -1 when empty.</summary>
    public int LastTick => _ticks.Count == 0 ? -1 : _ticks[^1];

    /// <summary>Adds a row; <paramref name="row"/> has one entry per <see cref="Slots"/> entry. Ignored when the tick is not after the last one or the row is the wrong length.</summary>
    public void Add(int tick, PlayerSample[] row)
    {
        if (tick <= LastTick || row.Length != Slots.Count) return;
        _ticks.Add(tick);
        _rows.Add((PlayerSample[])row.Clone());
    }

    /// <summary>The value of <paramref name="metric"/> for timeline column <paramref name="column"/> at sample <paramref name="index"/>.</summary>
    public int Value(int index, int column, GraphMetric metric) => _rows[index][column].Get(metric);

    /// <summary>The largest value of the metric over all samples and players (0 when empty).</summary>
    public int Max(GraphMetric metric)
    {
        int max = 0;
        foreach (PlayerSample[] row in _rows)
            foreach (PlayerSample s in row) max = Math.Max(max, s.Get(metric));
        return max;
    }

    /// <summary>The largest army value the player in <paramref name="column"/> reached at any sample.</summary>
    public int PeakArmy(int column)
    {
        int max = 0;
        foreach (PlayerSample[] row in _rows) max = Math.Max(max, row[column].ArmyValue);
        return max;
    }

    /// <summary>The column of sim slot <paramref name="slot"/>, or -1.</summary>
    public int ColumnOf(int slot)
    {
        for (int i = 0; i < Slots.Count; i++)
            if (Slots[i] == slot) return i;
        return -1;
    }
}
