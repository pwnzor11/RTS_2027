// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>
/// V-06: the most voice lines (unit and mission voice files) that play at once. A line that finds every slot taken is dropped, not queued:
/// a queued line would play late, after the moment it refers to. Its text is still shown. Time is passed in, so this is testable
/// without a clock.
/// </summary>
public sealed class VoiceSlots
{
    /// <summary>The number of voice lines that may play at once (ASSUMED, S45: two).</summary>
    public const int DefaultMax = 2;

    private readonly int _max;
    private readonly List<long> _endTimes = new();

    /// <summary>Creates a limiter for <paramref name="max"/> voices (at least 1).</summary>
    public VoiceSlots(int max = DefaultMax)
    {
        if (max < 1) throw new ArgumentOutOfRangeException(nameof(max), "at least one voice must be allowed");
        _max = max;
    }

    /// <summary>Takes a slot for a line that lasts <paramref name="durationMs"/> from <paramref name="nowMs"/>; false (and nothing is taken) when every slot is still in use.</summary>
    public bool TryTake(long nowMs, int durationMs)
    {
        _endTimes.RemoveAll(end => end <= nowMs);
        if (_endTimes.Count >= _max) return false;
        _endTimes.Add(nowMs + Math.Max(1, durationMs));
        return true;
    }

    /// <summary>The voice lines still playing at <paramref name="nowMs"/>.</summary>
    public int Active(long nowMs) => _endTimes.Count(end => end > nowMs);
}
