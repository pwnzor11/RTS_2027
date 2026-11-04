// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Game.Ui.Spectator;

/// <summary>
/// Front-end ring buffer of one sample per second of game time (20 ticks) of each player's <c>CreditsHarvested</c>; the income per
/// minute is the change over the last 60 s of samples. Integer arithmetic only. Never touches the sim.
/// </summary>
public sealed class IncomeTracker
{
    /// <summary>Samples kept: 60 s plus the one at the start of the window.</summary>
    public const int Samples = 61;

    private readonly int[] _ticks = new int[Samples];
    private readonly int[][] _harvested = new int[Samples][];
    private int _count;
    private int _head;     // index of the next write
    private int _lastTick = -1;

    /// <summary>Records a sample if <paramref name="sim"/> is on a whole-second tick not yet seen.</summary>
    public void Observe(Simulation sim)
    {
        int tick = sim.Tick;
        if (tick == _lastTick || tick % SimConstants.TicksPerSecond != 0) return;
        int players = sim.Setup.PlayerCount;
        var row = new int[players];
        for (int p = 0; p < players; p++) row[p] = sim.StatsOf((byte)p).CreditsHarvested;
        Observe(tick, row);
    }

    /// <summary>Records a sample of each player's total credits harvested at <paramref name="tick"/> (ignored if the tick was seen).</summary>
    public void Observe(int tick, int[] harvestedByPlayer)
    {
        if (tick == _lastTick) return;
        _lastTick = tick;
        _ticks[_head] = tick;
        _harvested[_head] = (int[])harvestedByPlayer.Clone();
        _head = (_head + 1) % Samples;
        if (_count < Samples) _count++;
    }

    /// <summary>Credits harvested per minute by <paramref name="player"/> over the samples held (0 with fewer than two samples).</summary>
    public int IncomePerMinute(int player)
    {
        if (_count < 2) return 0;
        int newest = (_head - 1 + Samples) % Samples;
        int oldest = (_head - _count + Samples) % Samples;
        int span = _ticks[newest] - _ticks[oldest];
        if (span <= 0 || player >= _harvested[newest].Length) return 0;
        long gained = _harvested[newest][player] - (long)_harvested[oldest][player];
        return (int)(gained * 60 * SimConstants.TicksPerSecond / span);
    }
}
