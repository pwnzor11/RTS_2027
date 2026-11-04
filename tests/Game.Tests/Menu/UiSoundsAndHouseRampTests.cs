// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Audio;
using Rts.Game.Menu;
using Rts.Game.Sprites;

namespace Rts.Game.Tests.Menu;

public sealed class UiSoundsAndHouseRampTests
{
    [Theory]
    [InlineData(MenuSounds.Move)]
    [InlineData(MenuSounds.Select)]
    [InlineData(MenuSounds.Back)]
    [InlineData(MenuSounds.Deny)]
    public void Every_menu_sound_id_is_defined_in_sounds_toml(string id)
    {
        string audioDir = Path.Combine(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!, "..", "audio");
        SoundBank bank = SoundBank.Load(Path.Combine(audioDir, "sounds.toml"));
        Assert.NotNull(bank.Sound(id));
        Assert.Empty(bank.MissingFiles(audioDir));
    }

    [Fact]
    public void Every_building_sprite_manifest_declares_the_standard_house_ramp()
    {
        string dir = Path.Combine(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!, "..", "sprites", "buildings");
        string[] files = Directory.GetFiles(dir, "*.sprite.toml");
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            SpriteManifest manifest = SpriteManifest.Load(file);
            Assert.True(manifest.HouseRamp.SequenceEqual(HouseRamp.Standard), $"{Path.GetFileName(file)} lacks the standard [house] ramp");
        }
    }
}
