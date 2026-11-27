// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Game.Menu;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13 (cnc#35): sound is muted while the window is in the background, when the setting is on.</summary>
public sealed class S47QolMuteTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void Muted_only_when_the_setting_is_on_and_the_window_is_inactive(bool setting, bool active, bool muted)
    {
        Assert.Equal(muted, AudioSettings.MutedNow(Settings.Default with { MuteInBackground = setting }, active));
    }

    [Fact]
    public void A_muted_apply_zeroes_the_levels_a_plain_apply_restores_them()
    {
        var audio = new NullAudio();
        Settings s = Settings.Default with { MasterVolume = 80, MusicVolume = 50, SfxVolume = 100 };
        AudioSettings.Apply(audio, s, muted: true);
        Assert.Equal(0, audio.SfxVolume);
        AudioSettings.Apply(audio, s);
        Assert.Equal(80, audio.SfxVolume);
        Assert.Equal(40, audio.MusicVolume);
    }

    [Fact]
    public void The_setting_is_off_by_default_and_round_trips()
    {
        Assert.False(Settings.Default.MuteInBackground);
        Assert.True(Settings.Parse((Settings.Default with { MuteInBackground = true }).ToToml()).MuteInBackground);
    }
}
