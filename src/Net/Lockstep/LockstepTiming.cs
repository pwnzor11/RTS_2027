// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Net.Lockstep;

/// <summary>Pacing constants shared by <see cref="LockstepHost"/> and <see cref="LockstepClient"/> (M9-03). All times in microseconds.</summary>
public static class LockstepTiming
{
    /// <summary>One tick: 50 ms at 20 Hz.</summary>
    public const long TickMicroseconds = 1_000_000 / SimConstants.TicksPerSecond;

    /// <summary>Most ticks one Update may step (catch-up cap).</summary>
    public const int MaxStepsPerUpdate = 5;

    /// <summary>Most clock debt kept, so a long stall is not followed by an endless burst.</summary>
    public const long MaxDebtMicroseconds = MaxStepsPerUpdate * TickMicroseconds;

    /// <summary>How often the host repeats the waiting notice while it waits.</summary>
    public const long WaitingRepeatMicroseconds = 500_000;

    /// <summary>The default lead limit for a delay: how many ticks the host may run ahead of the slowest active client.</summary>
    public static int DefaultMaxLead(int delay) => delay * 2 + 2;
}
