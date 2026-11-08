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
        Settings s = Settings.Parse("version = 1\nfullscreen = true\nwindow_width = 1920\nwindow_height = 1080\nui_scale_percent = 150\nmusic_volume = 10\nsfx_volume = 20\nfuture_key = 5");
        Assert.Equal(new Settings(Settings.CurrentVersion, true, 1920, 1080, 150, 10, 20), s);
    }

    [Fact]
    public void Wrong_types_fall_back_per_field_and_out_of_range_is_clamped()
    {
        Settings s = Settings.Parse("fullscreen = \"yes\"\nwindow_width = 5\nmusic_volume = 500\nsfx_volume = -4\nui_scale_percent = 1.5");
        Assert.False(s.Fullscreen);
        Assert.Equal(320, s.WindowWidth);
        Assert.Equal(100, s.MusicVolume);
        Assert.Equal(0, s.SfxVolume);
        Assert.Equal(Settings.Default.UiScalePercent, s.UiScalePercent);
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
            var saved = new Settings(Settings.CurrentVersion, true, 1600, 900, 125, 33, 44);
            Assert.True(saved.Save(path));
            Assert.Equal(saved, Settings.Load(path));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Ui_scale_percent_and_text_size_round_trip_and_bad_values_fall_back()
    {
        Settings s = Settings.Default with { UiScalePercent = 175, TextSize = Rts.Game.Ui.TextSize.Large };
        Settings back = Settings.Parse(s.ToToml());
        Assert.Equal(175, back.UiScalePercent);
        Assert.Equal(Rts.Game.Ui.TextSize.Large, back.TextSize);
        Assert.Equal(125, Settings.Parse("ui_scale_percent = 130").UiScalePercent);
        Assert.Equal(200, Settings.Parse("ui_scale_percent = 900").UiScalePercent);
        Assert.Equal(Rts.Game.Ui.TextSize.Normal, Settings.Parse("text_size = \"big\"").TextSize);
        Assert.Equal(100, Settings.Parse("ui_scale = 3").UiScalePercent);   // the old integer key is ignored
    }

    [Fact]
    public void The_scale_rule_multiplies_the_auto_scale_by_the_setting()
    {
        Assert.Equal(1f, Settings.Default.ResolveUiScale(1280, 720).Scale);
        Assert.Equal(1.5f, Settings.Default.ResolveUiScale(1920, 1080).Scale);
        Assert.Equal(0.75f, (Settings.Default with { UiScalePercent = 75 }).ResolveUiScale(1280, 720).Scale);
    }

    [Fact]
    public void An_old_file_without_the_camera_keys_gets_edge_scroll_on_and_the_default_speed()
    {
        Settings s = Settings.Parse("version = 2\nfullscreen = true\nllm_call_cap = 5");
        Assert.True(s.EdgeScroll);
        Assert.Equal(Settings.DefaultScrollSpeed, s.ScrollSpeed);
        Assert.True(s.Fullscreen);
    }

    [Fact]
    public void Camera_keys_round_trip_and_the_speed_is_clamped()
    {
        Settings s = Settings.Parse("edge_scroll = false\nscroll_speed = 1400");
        Assert.False(s.EdgeScroll);
        Assert.Equal(1400, s.ScrollSpeed);
        Assert.Equal(s, Settings.Parse(s.ToToml()));
        Assert.Equal(Settings.MaxScrollSpeed, Settings.Parse("scroll_speed = 99999").ScrollSpeed);
        Assert.Equal(Settings.MinScrollSpeed, Settings.Parse("scroll_speed = 1").ScrollSpeed);
        Assert.Equal(Settings.DefaultScrollSpeed, Settings.Parse("scroll_speed = \"fast\"").ScrollSpeed);
    }

    [Fact]
    public void Diagnostics_default_off_for_an_old_file_and_round_trip()
    {
        Assert.False(Settings.Parse("version = 2\nfullscreen = true").ShowDiagnostics);
        Assert.False(Settings.Default.ShowDiagnostics);
        Settings on = Settings.Parse("show_diagnostics = true");
        Assert.True(on.ShowDiagnostics);
        Assert.Equal(on, Settings.Parse(on.ToToml()));
        Assert.False(Settings.Parse("show_diagnostics = 1").ShowDiagnostics);
    }
}
