// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Tests.Ui.Skin;

public sealed class UiSkinSettingTests
{
    [Fact]
    public void DefaultIsFaction()
    {
        Assert.Equal("faction", Settings.Default.UiSkin);
        Assert.True(Settings.Default.UseFactionSkin);
        Assert.Equal("faction", Settings.Parse("version = 2").UiSkin);
    }

    [Fact]
    public void ClassicRoundTrips()
    {
        Settings s = Settings.Default with { UiSkin = "classic" };
        Settings back = Settings.Parse(s.ToToml());
        Assert.Equal("classic", back.UiSkin);
        Assert.False(back.UseFactionSkin);
        Assert.Equal(s, back);
    }

    [Fact]
    public void UnknownValueGivesFaction()
    {
        Assert.Equal("faction", Settings.Parse("ui_skin = \"gold\"").UiSkin);
        Assert.Equal("faction", Settings.Parse("ui_skin = 3").UiSkin);
    }
}
