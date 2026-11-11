// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Game.Tests.Audio;

namespace Rts.Game.Tests.S18;

/// <summary>S18 GAMEREND-04: UI Notify sounds keep their cooldown on the wall clock, so a paused (frozen sim clock) game still hears clicks.</summary>
public sealed class AudioNotifyClockTests
{
    private const string Bank = """
        [settings]
        hearing_radius_cells = 48
        pan_radius_cells = 24
        [sound.click]
        file = "a.wav"
        [event.unit_select]
        sounds = ["click"]
        cooldown_ms = 60
        positional = false
        """;

    [Fact]
    public void Notify_clicks_after_the_cooldown_play_even_though_no_sim_tick_has_passed()
    {
        var audio = new RecordingAudio();
        long wall = 1000;
        var d = new AudioDirector(SoundBank.Parse(Bank), audio, new Random(1), () => null, null, () => wall);
        d.Notify(AudioEventKind.UnitSelect);
        wall += 10;
        d.Notify(AudioEventKind.UnitSelect);   // inside the 60 ms cooldown: dropped
        Assert.Single(audio.Sfx);
        wall += 100;
        d.Notify(AudioEventKind.UnitSelect);   // 110 ms after the first: plays
        Assert.Equal(2, audio.Sfx.Count);
    }

    [Fact]
    public void A_new_game_does_not_reset_or_block_notify_cooldowns()
    {
        var audio = new RecordingAudio();
        long wall = 5;
        var d = new AudioDirector(SoundBank.Parse(Bank), audio, new Random(1), () => null, null, () => wall);
        d.Process(AudioTestSupport.Frame(AudioTestSupport.Snap(99), AudioTestSupport.Snap(100)));   // sim clock at 5000 ms
        d.Notify(AudioEventKind.UnitSelect);
        wall += 200;
        d.Notify(AudioEventKind.UnitSelect);
        Assert.Equal(2, audio.Sfx.Count);
    }
}
