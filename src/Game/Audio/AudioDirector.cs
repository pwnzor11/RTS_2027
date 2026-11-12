// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Audio;

/// <summary>
/// Plays the sounds for what happens in a game. Each step it derives events (<see cref="AudioEventDeriver"/>), looks each up in the
/// <see cref="SoundBank"/>, applies the per-event cooldown, picks a variant at random (game-side RNG, not the sim's), works out volume
/// and pan from the listener, and hands the result to <see cref="IAudio"/>. It also chooses the music state. No sim changes: it reads
/// snapshots and HUD only.
/// </summary>
public sealed class AudioDirector
{
    private const int MsPerTick = 1000 / SimConstants.TicksPerSecond;
    // ASSUMED (M3-09): the music stays on the battle track for this long after the last fight sound.
    private const int BattleHoldTicks = 20 * SimConstants.TicksPerSecond;
    // A sound quieter than this is not worth a voice.
    private const float AudibleGain = 0.02f;
    // S22: at most this many fight sounds (shots, explosions, deaths) start per sim step, so 50 units firing do not stack hundreds of voices.
    // Alerts, production and interface sounds are not counted. (The backend also caps the copies of one sound, SoundDef.MaxVoices.)
    private const int MaxCombatPlaysPerStep = 8;
    private const float WaterCheckSeconds = 0.5f;

    private readonly SoundBank _bank;
    private readonly IAudio _audio;
    private readonly Random _rng;
    private readonly Func<LeptonCoord?> _listener;
    private readonly AudioEventDeriver _deriver = new();
    private readonly Dictionary<string, long> _lastPlayedMs = new(StringComparer.Ordinal);   // S22: keyed by the resolved event name, so qualified events have their own cooldown
    private readonly Dictionary<string, long> _lastNotifiedMs = new(StringComparer.Ordinal);   // S18 GAMEREND-04: Notify cooldowns run on the wall clock
    private readonly Func<long> _wallClockMs;
    private readonly List<AudioEvent> _scratch = new();
    private readonly MusicPlayer? _music;
    private readonly AmbiencePlayer? _ambience;
    private float _waterTimer;
    private double _waterFraction;
    private string? _ambienceKey;
    private int _combatPlaysThisStep;
    private long _nowMs;
    private int _lastTick = -1;
    private int _lastCombatTick = int.MinValue / 2;
    private GameResult _result;
    private Faction _faction;
    private MusicState? _requested;
    private Faction _requestedFaction;

    /// <summary>Creates the director. <paramref name="listener"/> gives the camera centre (default <see cref="AudioListener.Position"/>); <paramref name="music"/> is optional.</summary>
    public AudioDirector(SoundBank bank, IAudio audio, Random rng, Func<LeptonCoord?>? listener = null, MusicPlayer? music = null,
        Func<long>? wallClockMs = null, AmbiencePlayer? ambience = null)
    {
        _ambience = ambience;
        _wallClockMs = wallClockMs ?? DefaultWallClock;
        _bank = bank;
        _audio = audio;
        _rng = rng;
        _listener = listener ?? (() => AudioListener.Position);
        _music = music;
    }

    private static readonly System.Diagnostics.Stopwatch WallWatch = System.Diagnostics.Stopwatch.StartNew();

    private static long DefaultWallClock() => WallWatch.ElapsedMilliseconds;

    /// <summary>The music state the director wants now (menu until a game frame has been processed).</summary>
    public MusicState MusicState { get; private set; } = MusicState.Menu;

    /// <summary>Handles one step of a game: derives events and plays them.</summary>
    public void Process(AudioFrame frame)
    {
        int tick = frame.Current.Tick;
        if (tick < _lastTick) _lastPlayedMs.Clear(); // a new game started
        _lastTick = tick;
        _nowMs = (long)tick * MsPerTick;
        _faction = frame.CurrentHud?.Faction ?? _faction;
        _result = frame.Result;

        _scratch.Clear();
        _combatPlaysThisStep = 0;
        _deriver.Derive(frame, _scratch);
        foreach (AudioEvent e in _scratch)
        {
            if (IsCombat(e.Kind)) _lastCombatTick = tick;
            Play(e);
        }

        MusicState = _result switch
        {
            GameResult.Won => MusicState.Victory,
            GameResult.Lost => MusicState.Defeat,
            _ => tick - _lastCombatTick < BattleHoldTicks ? MusicState.Battle : MusicState.Tension,
        };
    }

    /// <summary>
    /// Plays an event that has no snapshot trace, such as selecting a unit. Same lookup as derived events, but the cooldown runs on the wall clock,
    /// not the sim clock, so UI sounds still work while the game is paused (S18 GAMEREND-04).
    /// </summary>
    public void Notify(AudioEventKind kind, LeptonCoord? pos = null) => Play(new AudioEvent(kind, pos), wall: true);

    /// <summary>
    /// Asks for a music state from a screen outside a game (the briefing, the credits roll). It applies only while no game is on the
    /// stack, so a mission started from a briefing plays its game music, and the briefing music returns when the game is left.
    /// Call <see cref="ReleaseMusic"/> when the screen leaves.
    /// </summary>
    public void RequestMusic(MusicState state, Faction faction = Faction.Neutral)
    {
        _requested = state;
        _requestedFaction = faction;
    }

    /// <summary>Ends a <see cref="RequestMusic"/> request; the music goes back to what the game or menus want.</summary>
    public void ReleaseMusic() => _requested = null;

    /// <summary>
    /// The faction for a faction id as the campaign and mission files write it ("authority", "directorate", "left_behind",
    /// "expedition"). Anything else (empty, neutral, unknown) is <see cref="Faction.Neutral"/>, which has no faction music.
    /// </summary>
    public static Faction FactionOf(string factionId) => factionId switch
    {
        "authority" => Faction.Authority,
        "directorate" => Faction.Directorate,
        "left_behind" => Faction.LeftBehind,
        "expedition" => Faction.Expedition,
        _ => Faction.Neutral,
    };

    /// <summary>Advances the music by <paramref name="dt"/> seconds. <paramref name="inGame"/> false means no game is on the stack (menus and screens).</summary>
    public void UpdateMusic(float dt, bool inGame)
    {
        if (!inGame && _requested is { } requested)
        {
            MusicState = requested;
            _music?.Update(dt, requested, _requestedFaction);
            return;
        }

        if (!inGame) MusicState = MusicState.Menu;
        _music?.Update(dt, MusicState, _faction);
    }

    /// <summary>
    /// Advances the biome ambience by <paramref name="dt"/> seconds (S22). <paramref name="inGame"/> false (menus, briefings) fades it out. In a game the
    /// biome comes from <paramref name="theaterId"/> (the map's theater: rust_belt, glass_coast, ash_plains, mountains), or open_sea when most of
    /// the cells around the camera are water (checked twice a second). Nothing plays for a biome with no [ambience] entry in sounds.toml.
    /// </summary>
    public void UpdateAmbience(float dt, bool inGame, string? theaterId, SimSnapshot? snapshot)
    {
        if (_ambience is null) return;
        if (!inGame || snapshot is null)
        {
            _ambienceKey = null;
            _ambience.Update(dt, null);
            return;
        }

        _waterTimer -= dt;
        if (_waterTimer <= 0f)
        {
            _waterTimer = WaterCheckSeconds;
            _waterFraction = AmbienceSelector.WaterFraction(snapshot, _listener());
        }

        _ambienceKey = AmbienceSelector.Choose(theaterId, _waterFraction, _ambienceKey);
        _ambience.Update(dt, _ambienceKey);
    }

    /// <summary>The ambience key playing or fading in now (glass_coast, open_sea ...), or null.</summary>
    public string? AmbienceKey => _ambienceKey;

    private static bool IsCombat(AudioEventKind k) =>
        k is AudioEventKind.FireBullet or AudioEventKind.FireShell or AudioEventKind.FireMissile
            or AudioEventKind.ExplosionSmall or AudioEventKind.ExplosionLarge or AudioEventKind.UnderAttack
            or AudioEventKind.UnitDied or AudioEventKind.BuildingDestroyed or AudioEventKind.WaterImpact;

    // The sounds the per-step cap counts: shots, blasts, deaths and splashes (not the alert).
    private static bool IsFightSound(AudioEventKind k) => IsCombat(k) && k != AudioEventKind.UnderAttack;

    private void Play(AudioEvent e, bool wall = false)
    {
        EventDef? def = _bank.Resolve(e);   // S22: most specific key first, then the plain event, then the stand-in event
        if (def is null) return;
        Dictionary<string, long> played = wall ? _lastNotifiedMs : _lastPlayedMs;
        long now = wall ? _wallClockMs() : _nowMs;
        if (played.TryGetValue(def.Name, out long last) && now - last < def.CooldownMs) return;
        bool fight = !wall && IsFightSound(e.Kind);
        if (fight && _combatPlaysThisStep >= MaxCombatPlaysPerStep) return;

        float gain = def.Volume;
        float pan = 0f;
        if (def.Positional && e.Pos is { } pos && _listener() is { } listener)
        {
            (float g, float p) = Spatializer.Compute(listener, pos, _bank.HearingRadiusCells, _bank.PanRadiusCells);
            gain *= g;
            pan = p;
        }

        if (gain < AudibleGain) return;
        played[def.Name] = now;
        if (fight) _combatPlaysThisStep++;
        _audio.PlaySfx(_bank.Pick(def, _rng).Id, gain, pan);
    }
}
