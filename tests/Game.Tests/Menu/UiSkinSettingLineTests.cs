// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;

namespace Rts.Game.Tests.Menu;

/// <summary>M10-15 (s27): the settings screen line for the faction UI skin, and that both values survive a save and load.</summary>
public sealed class UiSkinSettingLineTests
{
    [Fact]
    public void Line_names_the_current_skin()
    {
        Assert.Equal("Interface skin: Faction art", SettingsScreen.SkinLine(Rts.Game.Settings.Default));
        Assert.Equal("Interface skin: Classic", SettingsScreen.SkinLine(Rts.Game.Settings.Default with { UiSkin = Rts.Game.Settings.UiSkinClassic }));
    }

    [Theory]
    [InlineData("faction")]
    [InlineData("classic")]
    public void Skin_value_round_trips_through_the_settings_file(string value)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts2027-uiskin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "settings.toml");
            (Rts.Game.Settings.Default with { UiSkin = value }).Save(path);
            Assert.Equal(value, Rts.Game.Settings.Load(path).UiSkin);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
