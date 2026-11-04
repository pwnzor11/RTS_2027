// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>
/// The game's sound output. Game code calls this and never touches MonoGame audio directly, so tests and headless runs
/// use <see cref="NullAudio"/> (review C-9). Sound ids are the ids in data/audio/sounds.toml; music ids are the file
/// names (without extension) in data/music.
/// </summary>
public interface IAudio
{
    /// <summary>Music volume 0..100.</summary>
    int MusicVolume { get; set; }

    /// <summary>Sound-effect volume 0..100.</summary>
    int SfxVolume { get; set; }

    /// <summary>Plays a one-shot sound effect.</summary>
    void PlaySfx(string id);

    /// <summary>Plays a one-shot sound effect scaled by <paramref name="gain"/> (0..1) and panned (-1 left .. 1 right).</summary>
    void PlaySfx(string id, float gain, float pan) => PlaySfx(id);

    /// <summary>Starts a music track, replacing the current one.</summary>
    void PlayMusic(string id);

    /// <summary>Starts a music track that repeats (<paramref name="loop"/>) or plays once.</summary>
    void PlayMusic(string id, bool loop) => PlayMusic(id);

    /// <summary>Scales the music volume by a fade factor 0..1 (used by the music player to cross-fade).</summary>
    void SetMusicFade(float gain)
    {
    }

    /// <summary>Stops the music.</summary>
    void StopMusic();
}
