// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Sim.Core;

namespace Rts.Game.Audio;

/// <summary>
/// Builds the game's audio in one call: the MonoGame backend with the sound bank, or <see cref="NullAudio"/> if the sound bank
/// or the audio device fails, and (when sound works) an <see cref="AudioComponent"/> that feeds the <see cref="AudioDirector"/> from the screen stack.
/// </summary>
public static class AudioBootstrap
{
    /// <summary>The director built by the last successful <see cref="Create"/>, or null (no audio device, or not created yet). Screens use it to <see cref="AudioDirector.Notify"/> UI sounds.</summary>
    public static AudioDirector? Director { get; private set; }

    /// <summary>
    /// Creates the audio for <paramref name="game"/>. <paramref name="dataDir"/> is the data folder (holding audio/ and music/). Volumes
    /// come from <paramref name="settings"/>. Never throws.
    /// </summary>
    public static IAudio Create(Microsoft.Xna.Framework.Game game, string dataDir, Settings settings, ScreenStack screens)
    {
        try
        {
            string audioDir = Path.Combine(dataDir, "audio");
            SoundBank bank = SoundBank.Load(Path.Combine(audioDir, "sounds.toml"));
            MusicLibrary library = MusicLibrary.Scan(Path.Combine(dataDir, "music"));
            var audio = new MonoGameAudio(bank, audioDir, library)
            {
                MusicVolume = settings.MusicVolume,
                SfxVolume = settings.SfxVolume,
            };
            var rng = new Random();
            var director = new AudioDirector(bank, audio, rng, music: new MusicPlayer(audio, library, rng));
            game.Components.Add(new AudioComponent(game, director, screens));
            Director = director;
            return audio;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException
                                      or Microsoft.Xna.Framework.Audio.NoAudioHardwareException or DllNotFoundException)
        {
            Console.Error.WriteLine($"audio disabled: {e.Message}");
            Director = null;
            return NullAudio.Instance;
        }
    }
}

/// <summary>
/// A game component that, each frame, finds the running game on the screen stack and gives the audio director each new tick's snapshots.
/// With no game on the stack the music is the menu music. Everything it reads is read-only.
/// </summary>
public sealed class AudioComponent : GameComponent
{
    private readonly AudioDirector _director;
    private readonly ScreenStack _screens;
    private SimSnapshotSource? _source;
    private int _lastTick = -1;
    private IReadOnlyList<LeptonCoord> _previousProjectiles = Array.Empty<LeptonCoord>();
    private PlayerHud? _previousHud;
    private Rts.Game.Veterancy.PromotionTracker _promotions = new();   // VET-5: rank rises for the Promotion sound
    private readonly List<Rts.Sim.View.ViewActor> _rankScratch = new();
    private readonly List<(Rts.Game.Veterancy.RankKey Key, int Rank)> _ranks = new();
    private readonly List<Rts.Game.Veterancy.RankKey> _promoted = new();

    /// <summary>Creates the component.</summary>
    public AudioComponent(Microsoft.Xna.Framework.Game game, AudioDirector director, ScreenStack screens)
        : base(game)
    {
        _director = director;
        _screens = screens;
    }

    /// <inheritdoc />
    public override void Update(GameTime gameTime)
    {
        SimSnapshotSource? source = null;
        foreach (IScreen screen in _screens.Screens)
        {
            if (screen is GameScreen { Sim: { } sim }) source = sim;
        }

        if (!ReferenceEquals(source, _source))
        {
            _source = source;
            _lastTick = -1;
            _promotions = new Rts.Game.Veterancy.PromotionTracker();   // VET-5: a new game starts with no remembered ranks
            _previousHud = null;
            _previousProjectiles = Array.Empty<LeptonCoord>();
        }

        if (source is not null && source.Current.Tick != _lastTick)
        {
            byte local = source.Local;
            PlayerHud hud = source.Sim.HudFor(local);
            int winner = source.Sim.WinningTeam;
            GameResult result = hud.Defeated ? GameResult.Lost
                : winner >= 0 && winner == source.Sim.Setup.TeamOf(local) ? GameResult.Won
                : GameResult.Running;
            // VET-5: rank rises at this tick (the view is read between Steps, as the renderer does).
            Rts.Game.Veterancy.RankLookup.Fill(source.Sim.ViewFor(local), _rankScratch, _ranks);
            _promotions.Update(source.Current.Tick, _ranks, _promoted);
            if (_lastTick >= 0 || source.Current.Tick == source.Previous.Tick)
            {
                _director.Process(new AudioFrame(source.Previous, source.Current, local)
                {
                    PreviousHud = _previousHud ?? hud,
                    CurrentHud = hud,
                    Pending = source.Host.Pending,
                    PreviousProjectiles = _previousProjectiles,
                    Projectiles = source.Projectiles,
                    Result = result,
                    Promotions = _promoted,
                });
            }

            _lastTick = source.Current.Tick;
            _previousHud = hud;
            _previousProjectiles = source.Projectiles.ToArray();
        }

        _director.UpdateMusic((float)gameTime.ElapsedGameTime.TotalSeconds, source is not null);
        base.Update(gameTime);
    }
}
