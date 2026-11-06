// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace Rts.Game.Audio;

/// <summary>
/// The MonoGame (OpenAL) sound output. Sounds are the WAV files named in data/audio/sounds.toml, loaded on first use. At most
/// <see cref="SoundDef.MaxVoices"/> copies of one sound play at once (<see cref="VoiceLimiter"/>). Music is an OGG from data/music through
/// MediaPlayer. If the audio device fails at any point the backend goes silent instead of throwing.
/// </summary>
public sealed class MonoGameAudio : IAudio
{
    private readonly SoundBank _bank;
    private readonly string _audioDir;
    private readonly MusicLibrary _music;
    private readonly VoiceLimiter _voices = new();
    private readonly Dictionary<string, SoundEffect?> _effects = new(StringComparer.Ordinal);
    private readonly List<SoundEffectInstance> _active = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Song? _song;
    private int _musicVolume = 100;
    private int _sfxVolume = 100;
    private int _masterVolume = 100;
    private float _fade = 1f;
    private bool _broken;

    /// <summary>Creates the backend and checks that the audio device works; throws if it does not (use <see cref="AudioBootstrap"/> for the fallback).</summary>
    public MonoGameAudio(SoundBank bank, string audioDir, MusicLibrary music)
    {
        _bank = bank;
        _audioDir = audioDir;
        _music = music;
        _ = SoundEffect.MasterVolume; // touches the device; throws NoAudioHardwareException without one
    }

    /// <summary>Overall volume 0..100, applied to sound and music. Settings has no master slider yet, so it stays at 100.</summary>
    public int MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0, 100);
            ApplyMusicVolume();
        }
    }

    /// <inheritdoc />
    public int MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Math.Clamp(value, 0, 100);
            ApplyMusicVolume();
        }
    }

    /// <inheritdoc />
    public int SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = Math.Clamp(value, 0, 100);
    }

    /// <inheritdoc />
    public void PlaySfx(string id) => PlaySfx(id, 1f, 0f);

    /// <inheritdoc />
    public void PlaySfx(string id, float gain, float pan)
    {
        if (_broken || _sfxVolume == 0 || _masterVolume == 0) return;
        SoundDef? def = _bank.Sound(id);
        if (def is null) return;
        try
        {
            SoundEffect? effect = Effect(def);
            if (effect is null) return;
            if (!_voices.TryAcquire(id, _clock.ElapsedMilliseconds, (int)effect.Duration.TotalMilliseconds, def.MaxVoices)) return;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].State == SoundState.Stopped)
                {
                    _active[i].Dispose();
                    _active.RemoveAt(i);
                }
            }

            SoundEffectInstance voice = effect.CreateInstance();
            voice.Volume = Math.Clamp(gain * def.Volume * (_sfxVolume / 100f) * (_masterVolume / 100f), 0f, 1f);
            voice.Pan = Math.Clamp(pan, -1f, 1f);
            voice.Play();
            _active.Add(voice);
        }
        catch (Exception e) when (e is NoAudioHardwareException or InvalidOperationException or IOException)
        {
            Fail(e);
        }
    }

    /// <inheritdoc />
    public bool HasVoice(string relativePath) => !_broken && File.Exists(Path.Combine(_audioDir, relativePath));

    /// <inheritdoc />
    public void PlayVoice(string relativePath)
    {
        if (_broken || _sfxVolume == 0 || _masterVolume == 0 || !HasVoice(relativePath)) return;
        try
        {
            using FileStream stream = File.OpenRead(Path.Combine(_audioDir, relativePath));
            SoundEffectInstance voice = SoundEffect.FromStream(stream).CreateInstance();
            voice.Volume = Math.Clamp(_sfxVolume / 100f * (_masterVolume / 100f), 0f, 1f);
            voice.Play();
            _active.Add(voice);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            // A voice file the backend cannot decode is text-only, not a broken audio device.
            Console.Error.WriteLine($"audio: cannot play voice {relativePath}: {e.Message}");
        }
    }

    /// <inheritdoc />
    public void PlayMusic(string id) => PlayMusic(id, true);

    /// <inheritdoc />
    public void PlayMusic(string id, bool loop)
    {
        if (_broken) return;
        string? path = _music.PathOf(id);
        if (path is null) return;
        try
        {
            _song = Song.FromUri(id, new Uri(path, UriKind.RelativeOrAbsolute));
            MediaPlayer.IsRepeating = loop;
            ApplyMusicVolume();
            MediaPlayer.Play(_song);
        }
        catch (Exception e) when (e is NoAudioHardwareException or InvalidOperationException or IOException or NotSupportedException)
        {
            Fail(e);
        }
    }

    /// <inheritdoc />
    public void SetMusicFade(float gain)
    {
        _fade = Math.Clamp(gain, 0f, 1f);
        ApplyMusicVolume();
    }

    /// <inheritdoc />
    public void StopMusic()
    {
        if (_broken) return;
        try
        {
            MediaPlayer.Stop();
        }
        catch (Exception e) when (e is NoAudioHardwareException or InvalidOperationException)
        {
            Fail(e);
        }
    }

    private SoundEffect? Effect(SoundDef def)
    {
        if (_effects.TryGetValue(def.Id, out SoundEffect? cached)) return cached;
        SoundEffect? effect = null;
        string path = Path.Combine(_audioDir, def.File);
        try
        {
            using FileStream stream = File.OpenRead(path);
            effect = SoundEffect.FromStream(stream);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"audio: cannot load {path}: {e.Message}");
        }

        _effects[def.Id] = effect;
        return effect;
    }

    private void ApplyMusicVolume()
    {
        if (_broken) return;
        try
        {
            MediaPlayer.Volume = Math.Clamp(_musicVolume / 100f * (_masterVolume / 100f) * _fade, 0f, 1f);
        }
        catch (Exception e) when (e is NoAudioHardwareException or InvalidOperationException)
        {
            Fail(e);
        }
    }

    private void Fail(Exception e)
    {
        _broken = true;
        Console.Error.WriteLine($"audio: device failed, continuing without sound: {e.Message}");
    }
}
