// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>
/// The game's sound output. Game code calls this and never touches MonoGame audio directly, so tests and headless runs
/// use <see cref="NullAudio"/> (review C-9). Ids name files under data/audio once that folder exists.
/// </summary>
public interface IAudio
{
    /// <summary>Music volume 0..100.</summary>
    int MusicVolume { get; set; }

    /// <summary>Sound-effect volume 0..100.</summary>
    int SfxVolume { get; set; }

    /// <summary>Plays a one-shot sound effect.</summary>
    void PlaySfx(string id);

    /// <summary>Starts a music track, replacing the current one.</summary>
    void PlayMusic(string id);

    /// <summary>Stops the music.</summary>
    void StopMusic();
}
