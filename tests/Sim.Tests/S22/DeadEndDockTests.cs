// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Tests.S20;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.S22;

/// <summary>S22 (Q-76, M13-18): harvesters waiting for a dock at the end of a dead-end corridor stand back outside it, so the one that has
/// unloaded can leave. Same world as the S20 test: a Left Behind yard, the field at (14..16, 8..10), 6000 ticks, income must not stall.</summary>
public sealed class DeadEndDockTests
{
    private readonly YardDockLivelockTests _play;

    public DeadEndDockTests(ITestOutputHelper output) => _play = new YardDockLivelockTests(output);

    /// <summary>Two, three and five gleaners on one dock behind a six-cell one-wide corridor.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void SeveralGleaners_OneDeadEndDock_EveryoneDelivers(int count) => _play.Play(count, 7);

    /// <summary>A corridor of two cells only (rock beside the dock cell and the cell south of it).</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void TwoCellCorridor_EveryoneDelivers(int count) => _play.Play(count, 3);
}
