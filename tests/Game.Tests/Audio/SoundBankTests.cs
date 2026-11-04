// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;

namespace Rts.Game.Tests.Audio;

/// <summary>data/audio/sounds.toml: it loads, maps every game event, and every file it names exists and is a small mono WAV.</summary>
public sealed class SoundBankTests
{
    private static SoundBank Load() => SoundBank.Load(Path.Combine(AudioTestSupport.AudioDir(), "sounds.toml"));

    [Fact]
    public void SoundsTomlLoadsAndEveryReferencedFileExists()
    {
        SoundBank bank = Load();
        Assert.NotEmpty(bank.Sounds);
        Assert.Empty(bank.MissingFiles(AudioTestSupport.AudioDir()));
    }

    [Fact]
    public void EveryEventKindIsMapped()
    {
        SoundBank bank = Load();
        foreach (AudioEventKind kind in Enum.GetValues<AudioEventKind>())
        {
            Assert.True(bank.Event(kind) is { Sounds.Count: > 0 }, $"no event for {AudioEventNames.Of(kind)}");
        }
    }

    [Fact]
    public void EventNamesAreSnakeCase()
    {
        Assert.Equal("fire_bullet", AudioEventNames.Of(AudioEventKind.FireBullet));
        Assert.Equal("harvester_unload", AudioEventNames.Of(AudioEventKind.HarvesterUnload));
    }

    [Fact]
    public void SomeEventsHaveVariantsAndPickStaysInTheList()
    {
        SoundBank bank = Load();
        EventDef def = bank.Event(AudioEventKind.ExplosionSmall)!;
        Assert.True(def.Sounds.Count >= 2);
        var rng = new Random(7);
        var seen = new HashSet<string>();
        for (int i = 0; i < 50; i++) seen.Add(bank.Pick(def, rng).Id);
        Assert.Equal(def.Sounds.Count, seen.Count);
        Assert.All(seen, id => Assert.Contains(id, def.Sounds));
    }

    [Fact]
    public void WeaponTableMapsToClassEvents()
    {
        SoundBank bank = Load();
        Assert.Equal(AudioEventKind.FireMissile, bank.EventForWeapon("auth_rocket"));
        Assert.Null(bank.EventForWeapon("no_such_weapon"));
    }

    [Fact]
    public void UnknownSoundInAnEventIsRejected()
    {
        const string text = "[sound.a]\nfile = \"a.wav\"\n[event.fire_bullet]\nsounds = [\"b\"]\n";
        Assert.Throws<InvalidDataException>(() => SoundBank.Parse(text));
    }

    [Fact]
    public void AllWavFilesAreSmall16BitMono()
    {
        string dir = Path.Combine(AudioTestSupport.AudioDir(), "sfx");
        string[] files = Directory.GetFiles(dir, "*.wav");
        Assert.True(files.Length >= 20);
        long total = 0;
        foreach (string f in files)
        {
            byte[] b = File.ReadAllBytes(f);
            total += b.Length;
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(b, 0, 4));
            Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(b, 8, 4));
            Assert.Equal(1, BitConverter.ToInt16(b, 22));   // channels
            Assert.Equal(16, BitConverter.ToInt16(b, 34));  // bits
            Assert.True(BitConverter.ToInt32(b, 24) <= 22050);
            Assert.True(b.Length < 100_000, f);
        }

        Assert.True(total < 1_000_000);
    }
}
