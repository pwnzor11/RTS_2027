// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>
/// Caps how many copies of the same sound play at once. A play is admitted if fewer than <c>max</c> earlier plays of that id are
/// still running at <c>nowMs</c>; it then counts until <c>nowMs + durationMs</c>. Time is passed in, so this is testable without a clock.
/// </summary>
public sealed class VoiceLimiter
{
    private readonly Dictionary<string, List<long>> _endTimes = new(StringComparer.Ordinal);

    /// <summary>Tries to start a copy of <paramref name="id"/>; false when <paramref name="max"/> copies are already playing.</summary>
    public bool TryAcquire(string id, long nowMs, int durationMs, int max)
    {
        if (!_endTimes.TryGetValue(id, out List<long>? ends))
        {
            ends = new List<long>();
            _endTimes[id] = ends;
        }

        ends.RemoveAll(end => end <= nowMs);
        if (ends.Count >= max) return false;
        ends.Add(nowMs + Math.Max(1, durationMs));
        return true;
    }

    /// <summary>Copies of <paramref name="id"/> still running at <paramref name="nowMs"/>.</summary>
    public int Active(string id, long nowMs) => _endTimes.TryGetValue(id, out List<long>? ends) ? ends.Count(e => e > nowMs) : 0;
}
