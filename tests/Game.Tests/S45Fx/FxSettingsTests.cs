// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Tests.S45Fx;

/// <summary>S45 V-04: the battle effect toggles default on, round-trip through the settings file and ignore a wrong-typed value.</summary>
public sealed class FxSettingsTests
{
    [Fact]
    public void Tread_marks_default_on_and_round_trip()
    {
        Assert.True(Settings.Default.TreadMarks);
        Settings off = Settings.Parse((Settings.Default with { TreadMarks = false }).ToToml());
        Assert.False(off.TreadMarks);
        Assert.True(Settings.Parse("version = 2\n").TreadMarks);
        Assert.True(Settings.Parse("version = 2\ntread_marks = \"no\"\n").TreadMarks);
    }
}
