// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Audio;

/// <summary>
/// Picks and fades music for a <see cref="MusicState"/>. When the state changes the current track fades out, the next one (random
/// among those that match, never the same twice running) starts and fades in. With no matching file it stays silent; a battle with
/// no battle track falls back to the tension track, and a briefing or credits with no track of its own falls back to menu music.
/// Fades go through <see cref="IAudio.SetMusicFade"/>.
/// </summary>
public sealed class MusicPlayer
{
    private enum Phase
    {
        Idle,
        FadingOut,
        FadingIn,
        Playing,
    }

    private readonly IAudio _audio;
    private readonly MusicLibrary _library;
    private readonly Random _rng;
    private readonly float _fadeSeconds;
    private Phase _phase = Phase.Idle;
    private string? _group;       // the group of the track now loaded (null = nothing)
    private string? _lastTrack;
    private float _gain;

    /// <summary>Creates a player. <paramref name="fadeSeconds"/> is the length of each fade (out, then in).</summary>
    public MusicPlayer(IAudio audio, MusicLibrary library, Random rng, float fadeSeconds = 2f)
    {
        _audio = audio;
        _library = library;
        _rng = rng;
        _fadeSeconds = Math.Max(0.05f, fadeSeconds);
    }

    /// <summary>The track playing now, or null.</summary>
    public string? CurrentTrack { get; private set; }

    /// <summary>The current fade factor 0..1.</summary>
    public float Gain => _gain;

    // S18 GAMEREND-10: the library never changes after it is scanned, so each (state, faction) answer is worked out once.
    private readonly Dictionary<(MusicState, Faction), (IReadOnlyList<string> Ids, string Group)> _candidateCache = new();

    /// <summary>The ids that can play for a state and faction, in the order of preference (empty = silence).</summary>
    public IReadOnlyList<string> Candidates(MusicState state, Faction faction, out string group)
    {
        if (_candidateCache.TryGetValue((state, faction), out var hit))
        {
            group = hit.Group;
            return hit.Ids;
        }

        IReadOnlyList<string> ids = ComputeCandidates(state, faction, out group);
        _candidateCache[(state, faction)] = (ids, group);
        return ids;
    }

    // The four factions named in music file names (lower case, no underscore: battle_leftbehind_1.ogg).
    private static readonly string[] NamedFactions = { "authority", "directorate", "leftbehind", "expedition" };

    private IReadOnlyList<string> ComputeCandidates(MusicState state, Faction faction, out string group)
    {
        string f = faction.ToString().ToLowerInvariant();
        string battle = "battle_" + f;
        string tension = "tension_" + f;
        string briefing = "briefing_" + f;

        switch (state)
        {
            case MusicState.Menu:
                return Choose(out group, ("menu", WithPrefix("menu")));
            case MusicState.Battle:
                return Choose(out group,
                    (battle, WithPrefix(battle)),
                    ("battle", GenericBattle()),
                    (tension, WithPrefix(tension)),
                    ("tension", WithPrefix("tension")));
            case MusicState.Tension:
                return Choose(out group, (tension, WithPrefix(tension)), ("tension", WithPrefix("tension")));
            case MusicState.Victory:
                return Choose(out group, ("victory", WithPrefix("victory")));
            case MusicState.Defeat:
                return Choose(out group, ("defeat", WithPrefix("defeat")));
            case MusicState.Briefing:
                return Choose(out group, (briefing, WithPrefix(briefing)), ("menu", WithPrefix("menu")));
            case MusicState.Credits:
                return Choose(out group, ("credits", WithPrefix("credits")), ("menu", WithPrefix("menu")));
            default:
                return Choose(out group);
        }
    }

    private IReadOnlyList<string> WithPrefix(string prefix) => _library.WithPrefix(prefix);

    // The generic battle tracks: never a track named for any faction (so no faction borrows another's battle music).
    private IReadOnlyList<string> GenericBattle() =>
        WithPrefix("battle").Where(id => !NamedFactions.Any(n => id.StartsWith("battle_" + n, StringComparison.OrdinalIgnoreCase))).ToList();

    // The first option with any track wins; its group names the fade target. Nothing at all is silence.
    private static IReadOnlyList<string> Choose(out string group, params (string Group, IReadOnlyList<string> Ids)[] options)
    {
        foreach ((string g, IReadOnlyList<string> ids) in options)
        {
            if (ids.Count > 0)
            {
                group = g;
                return ids;
            }
        }

        group = "";
        return Array.Empty<string>();
    }

    /// <summary>Advances the fades by <paramref name="dt"/> seconds and switches track if <paramref name="want"/> asks for another group.</summary>
    public void Update(float dt, MusicState want, Faction faction)
    {
        IReadOnlyList<string> candidates = Candidates(want, faction, out string target);
        bool hasTarget = candidates.Count > 0;
        bool same = hasTarget && target == _group;

        switch (_phase)
        {
            case Phase.Idle:
                if (hasTarget) Start(candidates, target, want);
                break;
            case Phase.FadingOut:
                if (same)
                {
                    _phase = Phase.FadingIn;
                    break;
                }

                SetGain(_gain - dt / _fadeSeconds);
                if (_gain <= 0f)
                {
                    _audio.StopMusic();
                    CurrentTrack = null;
                    _group = null;
                    _phase = Phase.Idle;
                    if (hasTarget) Start(candidates, target, want);
                }

                break;
            default:
                if (!same)
                {
                    _phase = Phase.FadingOut;
                    goto case Phase.FadingOut;
                }

                if (_phase == Phase.FadingIn)
                {
                    SetGain(_gain + dt / _fadeSeconds);
                    if (_gain >= 1f) _phase = Phase.Playing;
                }

                break;
        }
    }

    private void Start(IReadOnlyList<string> candidates, string group, MusicState state)
    {
        var pool = candidates.Where(c => c != _lastTrack).ToList();
        if (pool.Count == 0) pool = candidates.ToList();
        string track = pool[pool.Count == 1 ? 0 : _rng.Next(pool.Count)];
        SetGain(0f);
        _audio.PlayMusic(track, state is not (MusicState.Victory or MusicState.Defeat));
        CurrentTrack = track;
        _lastTrack = track;
        _group = group;
        _phase = Phase.FadingIn;
    }

    private void SetGain(float g)
    {
        _gain = Math.Clamp(g, 0f, 1f);
        _audio.SetMusicFade(_gain);
    }
}
