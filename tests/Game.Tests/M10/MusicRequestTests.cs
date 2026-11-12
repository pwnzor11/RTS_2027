// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Game.Tests.Audio;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Game.Tests.Audio.AudioTestSupport;

namespace Rts.Game.Tests.M10;

/// <summary>M10-09: screens request the briefing and credits music; the request applies only while no game is on the stack.</summary>
public sealed class MusicRequestTests
{
    private const string Bank = """
        [settings]
        hearing_radius_cells = 48
        pan_radius_cells = 24
        """;

    private static readonly string[] Tracks =
    {
        "menu", "credits", "victory", "defeat",
        "battle_authority_1", "battle_directorate_1", "battle_leftbehind_1", "battle_expedition_1",
        "tension_authority", "tension_directorate", "tension_leftbehind", "tension_expedition",
        "briefing_authority", "briefing_directorate", "briefing_leftbehind", "briefing_expedition",
    };

    private static (AudioDirector Director, RecordingAudio Audio) Make()
    {
        var audio = new RecordingAudio();
        var rng = new Random(5);
        var music = new MusicPlayer(audio, MusicLibrary.FromIds(Tracks), rng);
        return (new AudioDirector(SoundBank.Parse(Bank), audio, rng, music: music), audio);
    }

    [Fact]
    public void BriefingPlaysTheMissionsFactionTrack()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        d.RequestMusic(MusicState.Briefing, Faction.Expedition);
        d.UpdateMusic(0.1f, inGame: false);
        Assert.Equal(MusicState.Briefing, d.MusicState);
        Assert.Equal("briefing_expedition", audio.Music.Last());
    }

    [Fact]
    public void CreditsRequestPlaysTheCreditsTrack()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        d.RequestMusic(MusicState.Credits);
        d.UpdateMusic(0.1f, inGame: false);
        Assert.Equal(MusicState.Credits, d.MusicState);
        Assert.Equal("credits", audio.Music.Last());
    }

    [Fact]
    public void RequestIsIgnoredWhileAGameIsOnTheStack()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        d.RequestMusic(MusicState.Briefing, Faction.Authority);
        d.Process(Frame(Snap(1), Snap(2)));
        d.UpdateMusic(0.1f, inGame: true);
        Assert.Equal(MusicState.Tension, d.MusicState);
        Assert.Equal("tension_authority", audio.Music.Last());
    }

    [Fact]
    public void ReleasingTheRequestReturnsToMenuMusic()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        d.RequestMusic(MusicState.Briefing, Faction.LeftBehind);
        d.UpdateMusic(0.1f, inGame: false);
        Assert.Equal("briefing_leftbehind", audio.Music.Last());

        d.ReleaseMusic();
        for (int i = 0; i < 12; i++) d.UpdateMusic(0.5f, inGame: false);
        Assert.Equal(MusicState.Menu, d.MusicState);
        Assert.Equal("menu", audio.Music.Last());
    }

    [Fact]
    public void MissionFactionIdsMapToFactions()
    {
        Assert.Equal(Faction.Authority, AudioDirector.FactionOf("authority"));
        Assert.Equal(Faction.Directorate, AudioDirector.FactionOf("directorate"));
        Assert.Equal(Faction.LeftBehind, AudioDirector.FactionOf("left_behind"));
        Assert.Equal(Faction.Expedition, AudioDirector.FactionOf("expedition"));
        Assert.Equal(Faction.Neutral, AudioDirector.FactionOf(""));
        Assert.Equal(Faction.Neutral, AudioDirector.FactionOf("neutral"));
        Assert.Equal(Faction.Neutral, AudioDirector.FactionOf("nonsense"));
    }
}
