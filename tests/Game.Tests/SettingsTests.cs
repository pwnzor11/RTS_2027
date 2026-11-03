// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Tests;

public sealed class SettingsTests
{
    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts2027-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        string dir = TempDir();
        try
        {
            Assert.Equal(Settings.Default, Settings.Load(Path.Combine(dir, "nope.toml")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("this is = = not toml [[[")]
    [InlineData("version = \"x\"\nwindow_width = true")]
    public void Bad_text_gives_defaults_or_ignores_bad_fields(string text)
    {
        Assert.Equal(Settings.Default, Settings.Parse(text));
    }

    [Fact]
    public void Valid_fields_are_read_and_unknown_keys_ignored()
    {
        Settings s = Settings.Parse("version = 1\nfullscreen = true\nwindow_width = 1920\nwindow_height = 1080\nui_scale = 3\nmusic_volume = 10\nsfx_volume = 20\nfuture_key = 5");
        Assert.Equal(new Settings(1, true, 1920, 1080, 3, 10, 20), s);
    }

    [Fact]
    public void Wrong_types_fall_back_per_field_and_out_of_range_is_clamped()
    {
        Settings s = Settings.Parse("fullscreen = \"yes\"\nwindow_width = 5\nmusic_volume = 500\nsfx_volume = -4\nui_scale = 1.5");
        Assert.False(s.Fullscreen);
        Assert.Equal(320, s.WindowWidth);
        Assert.Equal(100, s.MusicVolume);
        Assert.Equal(0, s.SfxVolume);
        Assert.Equal(Settings.Default.UiScale, s.UiScale);
        Assert.Equal(Settings.Default.WindowHeight, s.WindowHeight);
    }

    [Fact]
    public void A_newer_version_gives_defaults()
    {
        Assert.Equal(Settings.Default, Settings.Parse("version = 99\nfullscreen = true"));
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        string dir = TempDir();
        try
        {
            string path = Path.Combine(dir, "sub", "settings.toml");
            var saved = new Settings(1, true, 1600, 900, 2, 33, 44);
            Assert.True(saved.Save(path));
            Assert.Equal(saved, Settings.Load(path));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Auto_ui_scale_grows_with_window_height_and_manual_wins()
    {
        Assert.Equal(2, Settings.Default.EffectiveUiScale(720));
        Assert.Equal(3, Settings.Default.EffectiveUiScale(1080));
        Assert.Equal(4, Settings.Default.EffectiveUiScale(2160));
        Assert.Equal(5, (Settings.Default with { UiScale = 5 }).EffectiveUiScale(720));
    }
}
