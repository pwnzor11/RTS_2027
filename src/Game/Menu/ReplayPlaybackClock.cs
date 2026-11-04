// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Host;
using Rts.Sim.Core;

namespace Rts.Game.Menu;

/// <summary>
/// The pure timing model of the replay viewer: pause, the 1x/2x/4x/8x speeds, single step, scrub-forward and the end of the replay. It
/// counts ticks and says how many to run; <see cref="ReplayPlayback"/> runs them. There is no way back (a replay only plays forward).
/// </summary>
public sealed class ReplayPlaybackClock
{
    /// <summary>The playback speeds, slowest first.</summary>
    public static readonly IReadOnlyList<int> Speeds = new[] { 1, 2, 4, 8 };

    /// <summary>Seconds of game time one scrub jumps forward.</summary>
    public const int ScrubSeconds = 10;

    private int _speedIndex;
    private long _accumulatedUs;

    /// <summary>Creates a clock for a replay of <paramref name="finalTick"/> ticks, starting running at 1x on tick 0.</summary>
    public ReplayPlaybackClock(int finalTick) => FinalTick = Math.Max(0, finalTick);

    /// <summary>Last tick of the replay.</summary>
    public int FinalTick { get; }

    /// <summary>Ticks run so far.</summary>
    public int Tick { get; private set; }

    /// <summary>True while paused (single steps still work).</summary>
    public bool Paused { get; set; }

    /// <summary>The current speed, 1, 2, 4 or 8.</summary>
    public int Speed => Speeds[_speedIndex];

    /// <summary>True once <see cref="Tick"/> reached <see cref="FinalTick"/>: nothing more runs.</summary>
    public bool Ended => Tick >= FinalTick;

    /// <summary>How far through the replay, 0..1.</summary>
    public float Progress => FinalTick == 0 ? 1f : Math.Clamp(Tick / (float)FinalTick, 0f, 1f);

    /// <summary>0..1 from the previous tick to the current one, for drawing between ticks. 1 while paused or ended.</summary>
    public float Alpha => Paused || Ended ? 1f : Math.Clamp(_accumulatedUs / (float)SimHost.TickMicroseconds, 0f, 1f);

    /// <summary>Pauses or resumes.</summary>
    public void TogglePause() => Paused = !Paused;

    /// <summary>Picks a speed; a value that is not one of <see cref="Speeds"/> picks the nearest lower one (at least 1x).</summary>
    public void SetSpeed(int speed)
    {
        int index = 0;
        for (int i = 0; i < Speeds.Count; i++)
            if (Speeds[i] <= speed) index = i;
        _speedIndex = index;
    }

    /// <summary>Steps the speed up (+1) or down (-1), stopping at 1x and 8x.</summary>
    public void ChangeSpeed(int delta) => _speedIndex = Math.Clamp(_speedIndex + delta, 0, Speeds.Count - 1);

    /// <summary>
    /// Adds elapsed wall time and returns how many ticks to run now (whole tick intervals times <see cref="Speed"/>, never past the end).
    /// Zero while paused or ended. Like <see cref="SimHost.Advance"/>, a stall is capped so it does not cause a burst.
    /// </summary>
    public int Advance(long elapsedMicroseconds)
    {
        if (Paused || Ended)
        {
            _accumulatedUs = 0;
            return 0;
        }

        _accumulatedUs = Math.Min(_accumulatedUs + Math.Max(0, elapsedMicroseconds), SimHost.MaxAccumulatedMicroseconds);
        long intervals = _accumulatedUs / SimHost.TickMicroseconds;
        _accumulatedUs -= intervals * SimHost.TickMicroseconds;
        return Take((int)Math.Min(intervals * Speed, int.MaxValue));
    }

    /// <summary>One tick now, whether paused or not (0 at the end).</summary>
    public int StepOne() => Take(1);

    /// <summary>Jumps forward <see cref="ScrubSeconds"/> of game time (fewer near the end).</summary>
    public int ScrubForward() => Take(ScrubSeconds * SimConstants.TicksPerSecond);

    /// <summary>Jumps to <paramref name="tick"/> if it is ahead of the current tick (never backward, never past the end). Returns the ticks to run.</summary>
    public int SeekForward(int tick) => Take(Math.Min(tick, FinalTick) - Tick);

    private int Take(int wanted)
    {
        int n = Math.Clamp(wanted, 0, FinalTick - Tick);
        Tick += n;
        return n;
    }
}
