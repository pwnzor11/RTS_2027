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
public sealed class MonoGameAudio : IAudio, IDisposable
{
    /// <summary>V-06: how many decoded voice clips stay in memory (least recently played are dropped).</summary>
    public const int VoiceCacheSize = 32;

    private readonly SoundBank _bank;
    private readonly string _audioDir;
    private readonly MusicLibrary _music;
    private readonly VoiceLimiter _voices = new();
    private readonly BoundedCache<string, SoundEffect?> _voiceClips = new(VoiceCacheSize);   // V-06: decoded once, then reused
    private readonly VoiceSlots _voiceSlots = new();                                       // V-06: at most two voice lines at once
    private readonly Dictionary<string, SoundEffect?> _effects = new(StringComparer.Ordinal);
    private readonly List<SoundEffectInstance> _active = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Song? _song;
    private int _musicVolume = 100;
    private int _sfxVolume = 100;
    private int _masterVolume = 100;
    private int _ambienceVolume = 100;
    private readonly List<AmbienceVoice> _ambience = new();
    private const int MaxSimultaneousSfx = 40;   // S22: no new one-shot starts while this many are playing
    private float _fade = 1f;
    private bool _broken;
    private readonly HashSet<string> _voiceReported = new(StringComparer.Ordinal);   // one diagnostic line per voice and kind per session

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
            ApplyAmbienceVolume();
        }
    }

    /// <inheritdoc />
    public int AmbienceVolume
    {
        get => _ambienceVolume;
        set
        {
            _ambienceVolume = Math.Clamp(value, 0, 100);
            ApplyAmbienceVolume();
        }
    }

    /// <inheritdoc />
    public IAmbienceVoice? StartAmbience(string soundId, float gain)
    {
        if (_broken) return null;
        SoundDef? def = _bank.Sound(soundId);
        if (def is null) return null;
        try
        {
            SoundEffect? effect = Effect(def);
            if (effect is null) return null;
            SoundEffectInstance instance = effect.CreateInstance();
            instance.IsLooped = true;
            var voice = new AmbienceVoice(this, instance, def.Volume) { Gain = Math.Clamp(gain, 0f, 1f) };
            voice.Apply();
            instance.Play();
            _ambience.Add(voice);
            return voice;
        }
        catch (Exception e) when (e is NoAudioHardwareException or InvalidOperationException or IOException)
        {
            Fail(e);
            return null;
        }
    }

    private float AmbienceScale => _ambienceVolume / 100f * (_masterVolume / 100f);

    private void ApplyAmbienceVolume()
    {
        if (_broken) return;
        foreach (AmbienceVoice v in _ambience) v.Apply();
    }

    private sealed class AmbienceVoice : IAmbienceVoice
    {
        private readonly MonoGameAudio _owner;
        private readonly SoundEffectInstance _instance;
        private readonly float _base;
        private bool _stopped;

        public AmbienceVoice(MonoGameAudio owner, SoundEffectInstance instance, float baseVolume)
        {
            _owner = owner;
            _instance = instance;
            _base = baseVolume;
        }

        public float Gain { get; set; }

        public void SetGain(float gain)
        {
            Gain = Math.Clamp(gain, 0f, 1f);
            Apply();
        }

        public void Apply()
        {
            if (_stopped || _owner._broken) return;
            try
            {
                _instance.Volume = Math.Clamp(Gain * _base * _owner.AmbienceScale, 0f, 1f);
            }
            catch (Exception e) when (e is NoAudioHardwareException or InvalidOperationException or ObjectDisposedException)
            {
                _stopped = true;
            }
        }

        public void Stop()
        {
            if (_stopped) return;
            _stopped = true;
            _owner._ambience.Remove(this);
            try
            {
                _instance.Stop();
                _instance.Dispose();
            }
            catch (Exception e) when (e is NoAudioHardwareException or InvalidOperationException or ObjectDisposedException)
            {
                // the device went away; nothing left to stop
            }
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

            if (_active.Count >= MaxSimultaneousSfx) return;   // S22: a hard ceiling on the voices of a big battle

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
    public bool HasVoice(string relativePath)
    {
        if (_broken) return false;
        if (File.Exists(Path.Combine(_audioDir, relativePath))) return true;
        if (_voiceReported.Add("missing:" + relativePath))
            Console.Error.WriteLine($"audio: voice file missing {relativePath} (looked in {_audioDir})");
        return false;
    }

    /// <inheritdoc />
    public void PlayVoice(string relativePath)
    {
        if (_broken || _sfxVolume == 0 || _masterVolume == 0)
        {
            if (_voiceReported.Add("muted:" + relativePath))
                Console.Error.WriteLine($"audio: voice {relativePath} not played (broken={_broken}, sfx volume {_sfxVolume}, master volume {_masterVolume})");
            return;
        }

        if (!HasVoice(relativePath)) return;
        try
        {
            // V-06: the clip is decoded on first use and kept (a clip that will not decode is kept as null, so it is not retried).
            SoundEffect? effect = _voiceClips.GetOrAdd(relativePath, LoadVoice);
            if (effect is null) return;
            if (!_voiceSlots.TryTake(_clock.ElapsedMilliseconds, (int)effect.Duration.TotalMilliseconds))
            {
                if (_voiceReported.Add("dropped:" + relativePath))
                    Console.Error.WriteLine($"audio: voice {relativePath} not played, {VoiceSlots.DefaultMax} voice lines are already playing (the text still shows)");
                return;
            }

            SoundEffectInstance voice = effect.CreateInstance();
            voice.Volume = Math.Clamp(_sfxVolume / 100f * (_masterVolume / 100f), 0f, 1f);
            voice.Play();
            _active.Add(voice);
            if (_voiceReported.Add("played:" + relativePath))
                Console.Error.WriteLine($"audio: voice {relativePath} {(int)effect.Duration.TotalMilliseconds} ms (volume {voice.Volume:0.##}, state {voice.State})");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or NotSupportedException or InvalidDataException)
        {
            // A voice file the backend cannot decode (including a corrupt OGG) is text-only, not a broken audio device.
            Console.Error.WriteLine($"audio: cannot play voice {relativePath}: {e.Message}");
        }
    }

    // V-06: decodes one voice file. Returns null (after a log line) when the file cannot be read or decoded.
    private SoundEffect? LoadVoice(string relativePath)
    {
        try
        {
            using FileStream stream = File.OpenRead(Path.Combine(_audioDir, relativePath));
            if (!relativePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) return SoundEffect.FromStream(stream);

            // Voice lines are OGG (MissionLocale); FromStream reads WAV only, so decode with NVorbis first.
            if (!VoiceDecoder.TryDecode(stream, relativePath, out byte[] pcm, out int rate, out int channels))
            {
                Console.Error.WriteLine($"audio: cannot play voice {relativePath}: only mono or stereo OGG is supported");
                return null;
            }

            return new SoundEffect(pcm, rate, channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or NotSupportedException or InvalidDataException)
        {
            Console.Error.WriteLine($"audio: cannot play voice {relativePath}: {e.Message}");
            return null;
        }
    }

    /// <summary>V-06: releases the music track and the sound instances still playing. Called once at shutdown; never throws.</summary>
    public void Dispose()
    {
        try
        {
            if (!_broken) MediaPlayer.Stop();
        }
        catch (Exception e) when (e is NoAudioHardwareException or InvalidOperationException)
        {
            // the device went away; nothing left to stop
        }

        foreach (AmbienceVoice v in _ambience.ToList()) v.Stop();
        foreach (SoundEffectInstance instance in _active)
        {
            try
            {
                instance.Dispose();
            }
            catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException)
            {
                // already gone
            }
        }

        _active.Clear();
        _song?.Dispose();
        _song = null;
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
            Song next = Song.FromUri(id, new Uri(path, UriKind.RelativeOrAbsolute));
            MediaPlayer.IsRepeating = loop;
            ApplyMusicVolume();
            MediaPlayer.Play(next);
            Song? previous = _song;   // V-06: the track this one replaces is released (it is no longer playing)
            _song = next;
            previous?.Dispose();
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
