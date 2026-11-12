// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Game.Tests.Audio.AudioTestSupport;

namespace Rts.Game.Tests.Audio;

/// <summary>S22: ambience selection by biome and water, the cross-fading player, the Ambience volume setting.</summary>
public sealed class AmbienceTests
{
    private sealed class FakeVoice : IAmbienceVoice
    {
        public float Gain { get; private set; }
        public bool Stopped { get; private set; }
        public void SetGain(float gain) => Gain = gain;
        public void Stop() => Stopped = true;
    }

    private sealed class FakeAudio : IAudio
    {
        public List<(string Id, FakeVoice Voice)> Started { get; } = new();
        public int MusicVolume { get; set; }
        public int SfxVolume { get; set; }
        public int AmbienceVolume { get; set; } = 100;
        public void PlaySfx(string id) { }
        public void PlayMusic(string id) { }
        public void StopMusic() { }
        public IAmbienceVoice? StartAmbience(string soundId, float gain)
        {
            var v = new FakeVoice();
            v.SetGain(gain);
            Started.Add((soundId, v));
            return v;
        }
    }

    private const string Toml = """
        [sound.amb_a]
        file = "a.wav"
        [sound.amb_b]
        file = "b.wav"
        [ambience.glass_coast]
        sound = "amb_a"
        volume = 0.5
        [ambience.open_sea]
        sound = "amb_b"
        """;

    [Theory]
    [InlineData("rustbelt", "rust_belt")]
    [InlineData("rust_belt", "rust_belt")]
    [InlineData("glasscoast", "glass_coast")]
    [InlineData("Glass_Coast", "glass_coast")]
    [InlineData("ashplains", "ash_plains")]
    [InlineData("mountains", "mountains")]
    [InlineData("", "rust_belt")]
    [InlineData(null, "rust_belt")]
    [InlineData("moonbase", "rust_belt")]
    public void TheTheaterPicksTheBiome(string? theater, string biome) => Assert.Equal(biome, AmbienceSelector.BiomeOf(theater));

    [Fact]
    public void MostlyWaterIsOpenSea_WithHysteresis()
    {
        Assert.Equal("glass_coast", AmbienceSelector.Choose("glasscoast", 0.3, null));
        Assert.Equal("glass_coast", AmbienceSelector.Choose("glasscoast", 0.59, "glass_coast"));
        Assert.Equal("open_sea", AmbienceSelector.Choose("glasscoast", 0.61, "glass_coast"));
        Assert.Equal("open_sea", AmbienceSelector.Choose("glasscoast", 0.45, "open_sea"));   // stays until under 0.4
        Assert.Equal("glass_coast", AmbienceSelector.Choose("glasscoast", 0.39, "open_sea"));
    }

    private static SimSnapshot Land(int w, int h, Func<int, int, LandType> f)
    {
        var land = new LandType[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) land[(y * w) + x] = f(x, y);
        }

        return new SimSnapshot(1, w, h, Array.Empty<UnitView>(), land, 0UL, Array.Empty<BuildingView>(), 0, Array.Empty<byte>());
    }

    [Fact]
    public void WaterFractionLooksAtTheCellsAroundTheCamera()
    {
        SimSnapshot s = Land(120, 60, (x, y) => x >= 60 ? LandType.Water : LandType.Clear);
        Assert.True(AmbienceSelector.WaterFraction(s, LeptonCoord.CenterOf(new CellCoord(100, 30))) > 0.95);
        Assert.True(AmbienceSelector.WaterFraction(s, LeptonCoord.CenterOf(new CellCoord(10, 30))) < 0.05);
        double edge = AmbienceSelector.WaterFraction(s, LeptonCoord.CenterOf(new CellCoord(60, 30)));
        Assert.InRange(edge, 0.4, 0.6);
        Assert.Equal(0, AmbienceSelector.WaterFraction(Snap(1), null));   // no land grid
    }

    [Fact]
    public void PlayerFadesInOneLoopAndCrossFadesToTheNext()
    {
        var audio = new FakeAudio();
        var player = new AmbiencePlayer(audio, SoundBank.Parse(Toml), fadeSeconds: 2f);
        player.Update(0.1f, "glass_coast");
        FakeVoice a = Assert.Single(audio.Started).Voice;
        Assert.InRange(a.Gain, 0.01f, 0.1f);
        for (int i = 0; i < 30; i++) player.Update(0.1f, "glass_coast");
        Assert.Equal(0.5f, a.Gain, 3);   // the entry's volume is the ceiling
        Assert.Single(audio.Started);

        player.Update(0.5f, "open_sea");
        Assert.Equal(2, audio.Started.Count);
        Assert.True(player.IsCrossFading);
        FakeVoice b = audio.Started[1].Voice;
        Assert.True(a.Gain < 0.5f && b.Gain > 0f && !a.Stopped);
        for (int i = 0; i < 25; i++) player.Update(0.1f, "open_sea");
        Assert.True(a.Stopped);
        Assert.False(player.IsCrossFading);
        Assert.Equal(1f, b.Gain, 3);
    }

    [Fact]
    public void NoWantedKeyFadesOutAndStops_AndAKeyWithoutAnEntryIsSilent()
    {
        var audio = new FakeAudio();
        var player = new AmbiencePlayer(audio, SoundBank.Parse(Toml), fadeSeconds: 1f);
        for (int i = 0; i < 20; i++) player.Update(0.1f, "glass_coast");
        FakeVoice a = audio.Started[0].Voice;
        for (int i = 0; i < 20; i++) player.Update(0.1f, null);
        Assert.True(a.Stopped);
        Assert.Null(player.CurrentKey);

        player.Update(0.1f, "mountains");   // no [ambience.mountains]
        Assert.Single(audio.Started);
        Assert.Equal(0f, player.CurrentGain);
    }

    [Fact]
    public void DirectorPlaysAmbienceInGameFromTheMapTheater_AndNotInMenus()
    {
        var audio = new FakeAudio();
        SoundBank bank = SoundBank.Parse(Toml);
        var d = new AudioDirector(bank, audio, new Random(1), () => LeptonCoord.CenterOf(new CellCoord(10, 30)), ambience: new AmbiencePlayer(audio, bank, 1f));
        SimSnapshot land = Land(120, 60, (x, y) => LandType.Clear);
        d.UpdateAmbience(0.1f, false, "glasscoast", null);
        Assert.Empty(audio.Started);   // off in menus

        d.UpdateAmbience(0.1f, true, "glasscoast", land);
        Assert.Equal("glass_coast", d.AmbienceKey);
        Assert.Equal("amb_a", Assert.Single(audio.Started).Id);

        d.UpdateAmbience(0.1f, false, "glasscoast", null);
        Assert.Null(d.AmbienceKey);
    }

    [Fact]
    public void DirectorSwitchesToTheSeaWhenTheCameraIsOverWater()
    {
        var audio = new FakeAudio();
        SoundBank bank = SoundBank.Parse(Toml);
        LeptonCoord? camera = LeptonCoord.CenterOf(new CellCoord(10, 30));
        var d = new AudioDirector(bank, audio, new Random(1), () => camera, ambience: new AmbiencePlayer(audio, bank, 1f));
        SimSnapshot land = Land(120, 60, (x, y) => x >= 60 ? LandType.Water : LandType.Clear);
        d.UpdateAmbience(0.1f, true, "glasscoast", land);
        Assert.Equal("glass_coast", d.AmbienceKey);
        camera = LeptonCoord.CenterOf(new CellCoord(100, 30));
        d.UpdateAmbience(1f, true, "glasscoast", land);   // the water check runs twice a second
        Assert.Equal("open_sea", d.AmbienceKey);
    }

    [Fact]
    public void AmbienceVolumeIsASettingThatRoundTripsAndIsAppliedToTheBackend()
    {
        Assert.Equal(100, Settings.Default.AmbienceVolume);
        Assert.Equal(100, Settings.Parse("version = 2\nmusic_volume = 30").AmbienceVolume);   // an older file
        Settings saved = Settings.Default with { AmbienceVolume = 35 };
        Assert.Equal(35, Settings.Parse(saved.ToToml()).AmbienceVolume);
        Assert.Equal(100, Settings.Parse("ambience_volume = 900").AmbienceVolume);

        var audio = new FakeAudio();
        Rts.Game.Menu.AudioSettings.Apply(audio, Settings.Default with { AmbienceVolume = 80, MasterVolume = 50 });
        Assert.Equal(40, audio.AmbienceVolume);
    }
}
