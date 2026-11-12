// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Net.Lockstep;
using Rts.Sim.Core;

namespace Net.Tests.Lockstep;

/// <summary>M9-03: a peer steps tick T only with bundle T; the first delay ticks are implicitly empty.</summary>
public sealed class TurnBufferTests
{
    private static Command[] One(int tick) =>
        new[] { new Command(tick, 0, 0, CommandKind.Stop, EntityId.None, EntityId.None, default, tick) };

    [Fact]
    public void The_first_delay_ticks_are_empty_and_need_no_bundle()
    {
        var b = new TurnBuffer(3);
        for (int t = 0; t < 3; t++)
        {
            Assert.True(b.CanStep(t));
            Assert.Empty(b.Take());
        }
        Assert.False(b.CanStep(3));
        Assert.Throws<InvalidOperationException>(() => b.Take());
    }

    [Fact]
    public void Bundles_out_of_order_are_held_until_their_tick()
    {
        var b = new TurnBuffer(2);
        Assert.True(b.Add(4, One(4)));
        Assert.Equal(1, b.ReadyThrough);
        Assert.True(b.Add(2, One(2)));
        Assert.Equal(2, b.ReadyThrough);
        Assert.True(b.Add(3, One(3)));
        Assert.Equal(4, b.ReadyThrough);
        b.Take(); b.Take();
        Assert.Equal(2, b.Take()[0].Arg);
        Assert.Equal(3, b.Take()[0].Arg);
        Assert.Equal(4, b.Take()[0].Arg);
        Assert.Equal(5, b.NextTick);
        Assert.Equal(0, b.Count);
    }

    [Fact]
    public void Duplicates_implicit_and_old_bundles_are_ignored()
    {
        var b = new TurnBuffer(2);
        Assert.False(b.Add(1, One(1)));   // implicit
        Assert.True(b.Add(2, One(2)));
        Assert.False(b.Add(2, One(2)));   // duplicate
        b.Take(); b.Take(); b.Take();
        Assert.False(b.Add(2, One(2)));   // already stepped
        Assert.False(b.CanStep(2));
    }
}
