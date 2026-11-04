// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Game.Tests.Audio.AudioTestSupport;

namespace Rts.Game.Tests.Audio;

/// <summary>Event to sound mapping, cooldowns, positional gain, and the music player.</summary>
public sealed class AudioDirectorTests
{
    private const string Bank = """
        [settings]
        hearing_radius_cells = 48
        pan_radius_cells = 24
        [sound.boom_a]
        file = "a.wav"
        [sound.boom_b]
        file = "b.wav"
        [sound.alarm]
        file = "c.wav"
        [event.explosion_small]
        sounds = ["boom_a", "boom_b"]
        positional = true
        cooldown_ms = 0
        [event.under_attack]
        sounds = ["alarm"]
        cooldown_ms = 8000
        """;

    private static (AudioDirector Director, RecordingAudio Audio) Make(LeptonCoord? listener = null, int seed = 1)
    {
        var audio = new RecordingAudio();
        return (new AudioDirector(SoundBank.Parse(Bank), audio, new Random(seed), () => listener), audio);
    }

    private static AudioFrame Explosion(int tick, int cellX) =>
        Frame(Snap(tick - 1), Snap(tick, fx: new[] { new FxEvent(FxKind.Detonation, LeptonCoord.CenterOf(new CellCoord(cellX, 5)), 1) }));

    [Fact]
    public void ExplosionPlaysOneOfItsVariants()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        d.Process(Explosion(10, 5));
        (string id, float gain, float pan) = Assert.Single(audio.Sfx);
        Assert.Contains(id, new[] { "boom_a", "boom_b" });
        Assert.Equal(1f, gain);
        Assert.Equal(0f, pan);
    }

    [Fact]
    public void SameSeedPicksTheSameVariants()
    {
        var runs = new List<string[]>();
        for (int run = 0; run < 2; run++)
        {
            (AudioDirector d, RecordingAudio audio) = Make(seed: 42);
            for (int t = 2; t < 12; t++) d.Process(Explosion(t, 5));
            runs.Add(audio.Sfx.Select(s => s.Id).ToArray());
        }

        Assert.Equal(runs[0], runs[1]);
        Assert.Contains("boom_a", runs[0]);
        Assert.Contains("boom_b", runs[0]);
    }

    [Fact]
    public void PositionalSoundsFollowTheListener()
    {
        (AudioDirector d, RecordingAudio audio) = Make(LeptonCoord.CenterOf(new CellCoord(5, 5)));
        d.Process(Explosion(10, 25)); // 20 cells to the right
        d.Process(Explosion(11, 200)); // out of earshot
        (_, float gain, float pan) = Assert.Single(audio.Sfx);
        Assert.InRange(gain, 0.1f, 0.95f);
        Assert.True(pan > 0.5f);
    }

    [Fact]
    public void CooldownSuppressesRepeatsUntilItHasPassed()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        AudioFrame Hit(int tick) => Frame(Snap(tick - 1, new[] { Unit(1, 0, 3, 3, 100) }), Snap(tick, new[] { Unit(1, 0, 3, 3, 90) }));
        d.Process(Hit(10));
        d.Process(Hit(20));   // 0.5 s later: inside the 8 s cooldown
        Assert.Single(audio.Sfx, s => s.Id == "alarm");
        d.Process(Hit(10 + (8 * SimConstants.TicksPerSecond)));
        Assert.Equal(2, audio.Sfx.Count(s => s.Id == "alarm"));
    }

    [Fact]
    public void NotifyPlaysUiEventsAndAMissingEventIsSilent()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        d.Notify(AudioEventKind.UnitSelect); // not in this bank
        Assert.Empty(audio.Sfx);
        d.Notify(AudioEventKind.UnderAttack);
        Assert.Single(audio.Sfx);
    }

    [Fact]
    public void MusicStateFollowsCombatAndResult()
    {
        (AudioDirector d, _) = Make();
        d.Process(Frame(Snap(1), Snap(2)));
        Assert.Equal(MusicState.Tension, d.MusicState);
        d.Process(Explosion(3, 5));
        Assert.Equal(MusicState.Battle, d.MusicState);
        d.Process(Frame(Snap(3), Snap(4)));
        Assert.Equal(MusicState.Battle, d.MusicState);
        d.Process(Frame(Snap(500), Snap(501)));
        Assert.Equal(MusicState.Tension, d.MusicState);
        d.Process(Frame(Snap(501), Snap(502)) with { Result = GameResult.Lost });
        Assert.Equal(MusicState.Defeat, d.MusicState);
        d.UpdateMusic(0.1f, inGame: false);
        Assert.Equal(MusicState.Menu, d.MusicState);
    }

    private static string MusicDir(params string[] names)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts2027_music_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (string n in names) File.WriteAllBytes(Path.Combine(dir, n + ".ogg"), Array.Empty<byte>());
        return dir;
    }

    [Fact]
    public void MusicPlayerStaysSilentWithNoFiles()
    {
        var audio = new RecordingAudio();
        var player = new MusicPlayer(audio, MusicLibrary.Scan(Path.Combine(Path.GetTempPath(), "no_such_music_dir_x")), new Random(1));
        for (int i = 0; i < 20; i++) player.Update(0.5f, MusicState.Battle, Faction.Authority);
        Assert.Empty(audio.Music);
        Assert.Null(player.CurrentTrack);
    }

    [Fact]
    public void MusicPlayerFadesOutThenInOnAStateChangeAndFallsBackToTension()
    {
        string dir = MusicDir("menu", "tension", "battle_directorate_1", "battle_directorate_2");
        try
        {
            var audio = new RecordingAudio();
            var player = new MusicPlayer(audio, MusicLibrary.Scan(dir), new Random(3), fadeSeconds: 1f);
            player.Update(0.1f, MusicState.Menu, Faction.Authority);
            Assert.Equal("menu", player.CurrentTrack);
            for (int i = 0; i < 12; i++) player.Update(0.1f, MusicState.Menu, Faction.Authority);
            Assert.Equal(1f, audio.Fade);

            // Authority has no battle track here, so battle falls back to tension.
            player.Update(0.1f, MusicState.Battle, Faction.Authority);
            Assert.True(audio.Fade < 1f);
            Assert.Equal("menu", player.CurrentTrack); // still fading out
            for (int i = 0; i < 12; i++) player.Update(0.1f, MusicState.Battle, Faction.Authority);
            Assert.Equal("tension", player.CurrentTrack);
            Assert.Equal(1, audio.Stops);

            // Directorate battle uses its own tracks, avoiding an immediate repeat.
            for (int i = 0; i < 25; i++) player.Update(0.1f, MusicState.Battle, Faction.Directorate);
            Assert.StartsWith("battle_directorate", player.CurrentTrack);
            Assert.True(audio.MusicLoops.All(l => l));

            for (int i = 0; i < 25; i++) player.Update(0.1f, MusicState.Victory, Faction.Directorate);
            Assert.Null(player.CurrentTrack); // no victory file: the music fades out and stays silent
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
