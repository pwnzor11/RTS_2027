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

    /// <summary>Starts from a timeline restored from a save (U-09), so the graphs of a loaded game keep their earlier samples and the next sample is due at the next interval.</summary>
    public void Restore(MatchTimeline? timeline)
    {
        Timeline = timeline;
        _lastSeen = timeline?.LastTick ?? -1;
        _nextTick = timeline is null ? 0 : (timeline.LastTick / IntervalTicks + 1) * IntervalTicks;
    }

    /// <summary>Largest sample or slot count read back from a save (guards against a damaged block).</summary>
    private const int MaxCount = 1 << 20;

    private static readonly GraphMetric[] Metrics = Enum.GetValues<GraphMetric>();

    /// <summary>Writes <paramref name="timeline"/> (slots, then each sample's tick and every player's numbers) for the game save's trailing block.</summary>
    public static void Write(BinaryWriter w, MatchTimeline timeline)
    {
        w.Write(timeline.Slots.Count);
        foreach (int slot in timeline.Slots) w.Write(slot);
        w.Write(timeline.Count);
        for (int i = 0; i < timeline.Count; i++)
        {
            w.Write(timeline.Ticks[i]);
            for (int c = 0; c < timeline.Slots.Count; c++)
                foreach (GraphMetric m in Metrics) w.Write(timeline.Value(i, c, m));
        }
    }

    /// <summary>Reads what <see cref="Write"/> wrote.</summary>
    /// <exception cref="InvalidDataException">The counts are out of range.</exception>
    public static MatchTimeline Read(BinaryReader r)
    {
        int slotCount = r.ReadInt32();
        if (slotCount < 0 || slotCount > 64) throw new InvalidDataException("Stats block: bad player count.");
        var slots = new int[slotCount];
        for (int i = 0; i < slotCount; i++) slots[i] = r.ReadInt32();
        var timeline = new MatchTimeline(slots);
        int count = r.ReadInt32();
        if (count < 0 || count > MaxCount) throw new InvalidDataException("Stats block: bad sample count.");
        for (int i = 0; i < count; i++)
        {
            int tick = r.ReadInt32();
            var row = new PlayerSample[slotCount];
            for (int c = 0; c < slotCount; c++)
            {
                var v = new int[Metrics.Length];
                for (int k = 0; k < v.Length; k++) v[k] = r.ReadInt32();
                row[c] = new PlayerSample(v[(int)GraphMetric.ArmyValue], v[(int)GraphMetric.Income], v[(int)GraphMetric.Kills],
                    v[(int)GraphMetric.Buildings], v[(int)GraphMetric.Upgrades]);
            }
            timeline.Add(tick, row);
        }
        return timeline;
    }

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
