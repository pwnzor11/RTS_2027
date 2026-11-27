// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13 (cnc#2): Home again goes to the next base.</summary>
public sealed class S47QolBaseCycleTests
{
    private static readonly Vector2[] Bases = { new(1000, 1000), new(5000, 1000), new(1000, 6000) };

    [Fact]
    public void No_bases_gives_minus_one_and_the_first_press_goes_to_the_first_base()
    {
        Assert.Equal(-1, BaseCycle.Next(Array.Empty<Vector2>(), Vector2.Zero, -1));
        Assert.Equal(0, BaseCycle.Next(Bases, new Vector2(3000, 3000), -1));
    }

    [Fact]
    public void Pressing_again_on_the_last_base_moves_on_and_wraps()
    {
        Assert.Equal(1, BaseCycle.Next(Bases, new Vector2(1010, 1000), 0));
        Assert.Equal(2, BaseCycle.Next(Bases, Bases[1], 1));
        Assert.Equal(0, BaseCycle.Next(Bases, Bases[2], 2));
    }

    [Fact]
    public void After_scrolling_away_the_next_press_returns_to_the_first_base_and_one_base_stays_put()
    {
        Assert.Equal(0, BaseCycle.Next(Bases, new Vector2(3000, 3000), 1));
        Assert.Equal(0, BaseCycle.Next(new[] { Bases[0] }, Bases[0], 0));
    }
}
