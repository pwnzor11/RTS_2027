// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>S22: one looping ambience voice. <see cref="SetGain"/> scales it (0..1, on top of the ambience volume setting); <see cref="Stop"/> ends it.</summary>
public interface IAmbienceVoice
{
    /// <summary>Sets the fade factor 0..1.</summary>
    void SetGain(float gain);

    /// <summary>Stops the loop and frees the voice.</summary>
    void Stop();
}

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

    /// <summary>Ambience volume 0..100 (S22). Backends without ambience just remember it.</summary>
    int AmbienceVolume
    {
        get => 100;
        set
        {
        }
    }

    /// <summary>
    /// Starts a looping ambience sound (the sound id of an [ambience] entry) at fade factor <paramref name="gain"/> and returns its voice,
    /// or null when the backend cannot play it (no device, file missing). The caller fades by <see cref="IAmbienceVoice.SetGain"/>.
    /// </summary>
    IAmbienceVoice? StartAmbience(string soundId, float gain) => null;

    /// <summary>Plays a one-shot sound effect.</summary>
    void PlaySfx(string id);

    /// <summary>Plays a one-shot sound effect scaled by <paramref name="gain"/> (0..1) and panned (-1 left .. 1 right).</summary>
    void PlaySfx(string id, float gain, float pan) => PlaySfx(id);

    /// <summary>True when the backend can play the voice file at <paramref name="relativePath"/> (under the audio folder, e.g. "voice/strand/m1.warning.ogg").</summary>
    bool HasVoice(string relativePath) => false;

    /// <summary>Plays a voice file (M8-05 dialogue) by path under the audio folder. Only called after <see cref="HasVoice"/> said yes.</summary>
    void PlayVoice(string relativePath)
    {
    }

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
