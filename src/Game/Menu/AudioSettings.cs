// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;

namespace Rts.Game.Menu;

/// <summary>Applies the volume settings to the audio backend.</summary>
public static class AudioSettings
{
    /// <summary>
    /// Sets music, effects and master volume from <paramref name="settings"/>. The master volume is a feature of the MonoGame backend
    /// (<see cref="MonoGameAudio.MasterVolume"/>); other backends (the silent one) have only music and effects, so for them the master
    /// level is folded into those two (music and effects scaled by master/100), which keeps the loudness the player chose.
    /// </summary>
    public static void Apply(IAudio audio, Settings settings)
    {
        if (audio is MonoGameAudio mono)
        {
            mono.MusicVolume = settings.MusicVolume;
            mono.SfxVolume = settings.SfxVolume;
            mono.MasterVolume = settings.MasterVolume;
            return;
        }
        audio.MusicVolume = settings.MusicVolume * settings.MasterVolume / 100;
        audio.SfxVolume = settings.SfxVolume * settings.MasterVolume / 100;
    }
}
