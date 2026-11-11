// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Tests.S18;

/// <summary>S18 GAMEREND-12: a settings file this build cannot read is kept as .bad instead of being overwritten by the next Save.</summary>
public sealed class UiSettingsLoadTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_s18_settings_" + Guid.NewGuid().ToString("N"));

    public UiSettingsLoadTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    [Fact]
    public void A_newer_version_file_loads_as_defaults_and_is_kept_as_bad()
    {
        string path = Path.Combine(_dir, "settings.toml");
        const string text = "version = 99\nfullscreen = true\n";
        File.WriteAllText(path, text);
        Assert.Equal(Settings.Default, Settings.Load(path));
        Assert.True(File.Exists(path + ".bad"));
        Assert.Equal(text, File.ReadAllText(path + ".bad"));
        Assert.True(Settings.Default.Save(path));   // the next Save no longer destroys the player's file
        Assert.Equal(text, File.ReadAllText(path + ".bad"));
    }

    [Fact]
    public void An_unparsable_file_is_kept_as_bad()
    {
        string path = Path.Combine(_dir, "settings.toml");
        File.WriteAllText(path, "fullscreen = = [[ not toml");
        Assert.Equal(Settings.Default, Settings.Load(path));
        Assert.Equal("fullscreen = = [[ not toml", File.ReadAllText(path + ".bad"));
    }

    [Fact]
    public void A_good_or_missing_file_leaves_no_bad_copy()
    {
        string path = Path.Combine(_dir, "settings.toml");
        Settings.Load(path);
        Assert.False(File.Exists(path + ".bad"));
        Assert.True(Settings.Default.Save(path));
        Settings.Load(path);
        Assert.False(File.Exists(path + ".bad"));
    }
}
