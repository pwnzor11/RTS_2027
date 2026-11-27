// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;

namespace Rts.Game.Menu;

/// <summary>Applies the volume settings to the audio backend.</summary>
public static class AudioSettings
{
    /// <summary>
    /// Sets music, effects, ambience (S22) and master volume from <paramref name="settings"/>. The master volume is a feature of the MonoGame backend
    /// (<see cref="MonoGameAudio.MasterVolume"/>); other backends (the silent one) have only music and effects, so for them the master
    /// level is folded into those two (music and effects scaled by master/100), which keeps the loudness the player chose.
    /// </summary>
    public static void Apply(IAudio audio, Settings settings) => Apply(audio, settings, false);

    /// <summary>S47 cnc#35: whether all sound is muted now: the setting is on and the window is in the background.</summary>
    public static bool MutedNow(Settings settings, bool windowActive) => settings.MuteInBackground && !windowActive;

    /// <summary>As <see cref="Apply(IAudio, Settings)"/>; with <paramref name="muted"/> the master level is 0 (sound off) without touching the saved volumes.</summary>
    public static void Apply(IAudio audio, Settings settings, bool muted)
    {
        int master = muted ? 0 : settings.MasterVolume;
        if (audio is MonoGameAudio mono)
        {
            mono.MusicVolume = settings.MusicVolume;
            mono.SfxVolume = settings.SfxVolume;
            mono.AmbienceVolume = settings.AmbienceVolume;
            mono.MasterVolume = master;
            return;
        }
        audio.AmbienceVolume = settings.AmbienceVolume * master / 100;
        audio.MusicVolume = settings.MusicVolume * master / 100;
        audio.SfxVolume = settings.SfxVolume * master / 100;
    }
}
