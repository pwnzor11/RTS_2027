// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Spectator;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Game.Stats;

/// <summary>
/// M10-17: reads the sim's existing stats every 10 s of game time into a <see cref="MatchTimeline"/>. It only reads; the sim is
/// not changed. Call <see cref="Observe(Simulation, IncomeTracker?)"/> once per frame, in play and in replay playback. When the tick goes
/// backwards (a replay started over) it starts a fresh timeline.
/// </summary>
public sealed class StatsSampler
{
    /// <summary>Seconds of game time between samples.</summary>
    public const int IntervalSeconds = 10;

    /// <summary>Sim ticks between samples.</summary>
    public const int IntervalTicks = IntervalSeconds * SimConstants.TicksPerSecond;

    private int _nextTick;
    private int _lastSeen = -1;

    /// <summary>The timeline so far; null until the first sample.</summary>
    public MatchTimeline? Timeline { get; private set; }

    /// <summary>Samples <paramref name="sim"/> when a sample is due.</summary>
    public void Observe(Simulation sim, IncomeTracker? income = null) =>
        Observe(sim.Tick, () => SpectatorTable.Collect(sim, income));

    /// <summary>Takes a sample at <paramref name="tick"/> if one is due; <paramref name="collect"/> is called only then.</summary>
    public void Observe(int tick, Func<IReadOnlyList<SpectatorRow>> collect)
    {
        if (tick < _lastSeen)
        {
            Timeline = null;   // the replay was restarted
            _nextTick = 0;
        }

        _lastSeen = tick;
        if (tick < _nextTick) return;
        Take(tick, collect());
        _nextTick = (tick / IntervalTicks + 1) * IntervalTicks;
    }

    /// <summary>Takes a last sample at <paramref name="tick"/> (the end of the match) unless that tick was already sampled.</summary>
    public void Finish(int tick, Func<IReadOnlyList<SpectatorRow>> collect)
    {
        if (Timeline is { } t && tick <= t.LastTick) return;
        Take(tick, collect());
    }

    /// <summary>Takes a last sample of <paramref name="sim"/>.</summary>
    public void Finish(Simulation sim, IncomeTracker? income = null) => Finish(sim.Tick, () => SpectatorTable.Collect(sim, income));

    private void Take(int tick, IReadOnlyList<SpectatorRow> rows)
    {
        Timeline ??= new MatchTimeline(rows.Select(r => r.Player).ToArray());
        var row = new PlayerSample[Timeline.Slots.Count];
        for (int c = 0; c < row.Length; c++)
        {
            SpectatorRow? r = rows.FirstOrDefault(x => x.Player == Timeline.Slots[c]);
            row[c] = r is null ? default
                : new PlayerSample(r.ArmyValue, r.Stats.CreditsHarvested + r.Stats.ShipmentIncome, r.Stats.Kills, r.Structures, r.Upgrades);
        }

        Timeline.Add(tick, row);
    }
}
