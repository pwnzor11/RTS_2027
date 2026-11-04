// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Host;

/// <summary>
/// Stopwatch-tick accumulators a perf benchmark attaches to <see cref="SimHost.Profile"/> (M4-16). Front end only; the sim never
/// sees a clock.
/// </summary>
public sealed class HostProfile
{
    /// <summary>Ticks spent in AI decisions (<c>AiDriver.Collect</c>).</summary>
    public long AiTicks;

    /// <summary>Ticks spent in <c>Simulation.Step</c> and the replay hash.</summary>
    public long StepTicks;

    /// <summary>Ticks spent building snapshots and capturing projectiles.</summary>
    public long SnapshotTicks;

    /// <summary>Sim ticks run.</summary>
    public int Steps;

    /// <summary>Snapshots built.</summary>
    public int Snapshots;
}
