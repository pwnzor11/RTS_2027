// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Xunit;

namespace Rts.Game.Tests.Ui2;

public sealed class HealthBarRuleTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, false, true)]
    public void Visibility_rule(bool always, bool selected, bool damaged, bool expected) =>
        Assert.Equal(expected, HealthBars.ShouldShow(always, selected, damaged));

    [Fact]
    public void Setting_defaults_to_damaged_or_selected_and_round_trips()
    {
        Assert.False(Settings.Default.HealthBarsAlways);
        Settings on = Settings.Default with { HealthBarsAlways = true };
        Assert.True(Settings.Parse(on.ToToml()).HealthBarsAlways);
        Assert.False(Settings.Parse("version = 2\nhealth_bars_always = \"yes\"").HealthBarsAlways);
    }
}
