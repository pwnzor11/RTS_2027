// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>
/// S22: plays the looping biome ambience and cross-fades when the wanted ambience changes. <see cref="Update"/> is told each frame which
/// ambience key is wanted (null = none, for menus); the old loop fades out while the new one fades in. A key with no [ambience] entry in sounds.toml
/// (or whose loop the backend cannot start) is silent, so the game runs without the ambience files. The loudness setting is the backend's
/// <see cref="IAudio.AmbienceVolume"/>; the per-entry <c>volume</c> of sounds.toml is applied here.
/// </summary>
public sealed class AmbiencePlayer
{
    private sealed class Voice
    {
        public IAmbienceVoice? Handle;
        public float Gain;        // current fade factor 0..1
        public float Ceiling;     // the sounds.toml volume of the entry
    }

    private readonly IAudio _audio;
    private readonly SoundBank _bank;
    private readonly float _fadeSeconds;
    private Voice? _current;
    private Voice? _leaving;
    private string? _currentKey;

    /// <summary>Creates the player. <paramref name="fadeSeconds"/> is the length of a full cross-fade.</summary>
    public AmbiencePlayer(IAudio audio, SoundBank bank, float fadeSeconds = 3f)
    {
        _audio = audio;
        _bank = bank;
        _fadeSeconds = Math.Max(0.05f, fadeSeconds);
    }

    /// <summary>The key of the ambience that is playing or fading in, or null.</summary>
    public string? CurrentKey => _currentKey;

    /// <summary>The fade factor (0..1) of the current ambience, 0 when there is none.</summary>
    public float CurrentGain => _current?.Gain ?? 0f;

    /// <summary>True while an older loop is still fading out.</summary>
    public bool IsCrossFading => _leaving is not null;

    /// <summary>Advances the fades by <paramref name="dt"/> seconds; <paramref name="wantedKey"/> is the ambience to play (null = none).</summary>
    public void Update(float dt, string? wantedKey)
    {
        float step = dt / _fadeSeconds;
        if (wantedKey != _currentKey)
        {
            if (_leaving is not null) Stop(_leaving);   // a second change mid-fade drops the oldest loop at once
            _leaving = _current;
            _current = null;
            _currentKey = wantedKey;
            if (wantedKey is not null && _bank.AmbienceFor(wantedKey) is { } def)
            {
                IAmbienceVoice? handle = _audio.StartAmbience(def.SoundId, 0f);
                _current = new Voice { Handle = handle, Gain = 0f, Ceiling = def.Volume };
            }
        }

        if (_leaving is not null)
        {
            _leaving.Gain = Math.Max(0f, _leaving.Gain - step);
            Apply(_leaving);
            if (_leaving.Gain <= 0f)
            {
                Stop(_leaving);
                _leaving = null;
            }
        }

        if (_current is not null)
        {
            _current.Gain = Math.Min(1f, _current.Gain + step);
            Apply(_current);
        }
    }

    private static void Apply(Voice v) => v.Handle?.SetGain(v.Gain * v.Ceiling);

    private static void Stop(Voice v)
    {
        v.Handle?.Stop();
        v.Handle = null;
    }
}
