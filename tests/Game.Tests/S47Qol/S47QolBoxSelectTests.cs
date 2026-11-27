// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13 (cnc#8): a plain box select favours fighters by default; Alt turns it round.</summary>
public sealed class S47QolBoxSelectTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public void The_setting_and_alt_decide_whether_the_box_takes_fighters_only(bool setting, bool alt, bool fightersOnly)
    {
        Assert.Equal(fightersOnly, Selection.BoxTakesFightersOnly(setting, alt));
    }

    [Fact]
    public void The_setting_is_on_by_default_and_round_trips()
    {
        Assert.True(Settings.Default.BoxSelectFightersFirst);
        Assert.False(Settings.Parse((Settings.Default with { BoxSelectFightersFirst = false }).ToToml()).BoxSelectFightersFirst);
    }
}
