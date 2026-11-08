// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Controls;

/// <summary>
/// Detects a double tap (the same number key twice, or a double-click on the same unit) within a time window. The clock is injected as a
/// millisecond value by the caller, so the rule is pure and testable. After a double tap the tracker forgets, so a third tap starts afresh.
/// </summary>
/// <typeparam name="T">What was tapped (a group number, a unit).</typeparam>
public sealed class TapTracker<T> where T : struct, IEquatable<T>
{
    /// <summary>Longest gap between two taps that still counts as a double tap, in milliseconds.</summary>
    public const long WindowMs = 350;

    private T? _last;
    private long _lastMs;

    /// <summary>Registers a tap at <paramref name="nowMs"/>; true when it is the second tap on the same target within <see cref="WindowMs"/>.</summary>
    public bool Tap(T target, long nowMs)
    {
        bool dbl = _last is { } previous && previous.Equals(target) && nowMs >= _lastMs && nowMs - _lastMs <= WindowMs;
        if (dbl)
        {
            _last = null;
            return true;
        }

        _last = target;
        _lastMs = nowMs;
        return false;
    }

    /// <summary>Forgets the last tap (a tap on something else, or a drag, breaks the pair).</summary>
    public void Reset() => _last = null;
}
