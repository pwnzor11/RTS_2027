// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13 (cnc#41): the VSync setting keeps today's behaviour by default and is saved.</summary>
public sealed class S47QolVsyncTests
{
    [Fact]
    public void VSync_is_on_by_default_and_off_survives_a_save()
    {
        Assert.True(Settings.Default.VSync);
        Assert.False(Settings.Parse((Settings.Default with { VSync = false }).ToToml()).VSync);
        Assert.True(Settings.Parse("version = 2\n").VSync);
    }
}
