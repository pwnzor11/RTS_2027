// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>An <see cref="IAudio"/> that makes no sound. Remembers the volumes so settings round-trip.</summary>
public sealed class NullAudio : IAudio
{
    private int _music = 100;
    private int _sfx = 100;

    /// <summary>The shared instance.</summary>
    public static NullAudio Instance { get; } = new();

    /// <inheritdoc />
    public int MusicVolume
    {
        get => _music;
        set => _music = Math.Clamp(value, 0, 100);
    }

    /// <inheritdoc />
    public int SfxVolume
    {
        get => _sfx;
        set => _sfx = Math.Clamp(value, 0, 100);
    }

    /// <inheritdoc />
    public void PlaySfx(string id)
    {
    }

    /// <inheritdoc />
    public void PlayMusic(string id)
    {
    }

    /// <inheritdoc />
    public void StopMusic()
    {
    }
}
