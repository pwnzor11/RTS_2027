// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Game.Menu;
using Rts.Game.Screens;

namespace Rts.Game.Tests.Menu;

public sealed class SettingsMasterVolumeTests
{
    [Fact]
    public void Master_volume_defaults_to_full()
    {
        Assert.Equal(100, Settings.Default.MasterVolume);
        Assert.Equal(100, Settings.Parse("version = 1\nmusic_volume = 30").MasterVolume);   // a file from before M3-16
    }

    [Theory]
    [InlineData(0)]
    [InlineData(35)]
    [InlineData(100)]
    public void Master_volume_round_trips_through_toml_text(int master)
    {
        Settings saved = Settings.Default with { MasterVolume = master, MusicVolume = 40, SfxVolume = 55 };

        Settings loaded = Settings.Parse(saved.ToToml());

        Assert.Equal(master, loaded.MasterVolume);
        Assert.Equal(saved, loaded);
        Assert.Contains($"master_volume = {master}", saved.ToToml());
    }

    [Fact]
    public void Master_volume_round_trips_through_a_file_and_is_clamped_on_load()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts2027-master-" + Guid.NewGuid().ToString("N"));
        try
        {
            string file = Path.Combine(dir, "settings.toml");
            Assert.True((Settings.Default with { MasterVolume = 42 }).Save(file));
            Assert.Equal(42, Settings.Load(file).MasterVolume);

            File.WriteAllText(file, "master_volume = 900");
            Assert.Equal(100, Settings.Load(file).MasterVolume);
            File.WriteAllText(file, "master_volume = \"loud\"");
            Assert.Equal(100, Settings.Load(file).MasterVolume);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Applying_settings_without_a_master_channel_folds_master_into_music_and_effects()
    {
        var audio = new NullAudio();

        AudioSettings.Apply(audio, Settings.Default with { MasterVolume = 50, MusicVolume = 80, SfxVolume = 60 });

        Assert.Equal(40, audio.MusicVolume);
        Assert.Equal(30, audio.SfxVolume);
    }

    [Theory]
    [InlineData(0, "[--------------------]   0%")]
    [InlineData(50, "[##########----------]  50%")]
    [InlineData(100, "[####################] 100%")]
    public void Volume_bar_shows_the_level(int volume, string expected) => Assert.Equal(expected, SettingsScreen.VolumeBar(volume));

    [Fact]
    public void Window_size_cycles_through_the_presets_and_wraps()
    {
        Assert.Equal((1600, 900), SettingsScreen.NextWindowSize(1280, 720, 1));
        Assert.Equal((1280, 720), SettingsScreen.NextWindowSize(2560, 1440, 1));
        Assert.Equal((2560, 1440), SettingsScreen.NextWindowSize(1280, 720, -1));
        Assert.Equal((1280, 720), SettingsScreen.NextWindowSize(999, 555, 1));   // not a preset: start at the first
    }
}
