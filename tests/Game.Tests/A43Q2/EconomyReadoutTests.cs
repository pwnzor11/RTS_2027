// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui;
using Xunit;

namespace Rts.Game.Tests.A43Q2;

public sealed class EconomyReadoutTests
{
    [Fact]
    public void The_line_shows_income_and_working_over_total_harvesters()
    {
        Assert.Equal("Income +380/min   Harvesters 2/3", EconomyReadout.Line(380, 2, 3));
    }

    [Fact]
    public void Counts_never_go_negative_or_above_the_total()
    {
        Assert.Equal("Income +0/min   Harvesters 0/0", EconomyReadout.Line(-5, -1, 0));
        Assert.Equal("Income +10/min   Harvesters 3/3", EconomyReadout.Line(10, 9, 3));
    }
}
