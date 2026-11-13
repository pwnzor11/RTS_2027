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

    // S22: events that exist only when a generated sound file does (no plain entry, and no stand-in event): silent without it.
    private static readonly AudioEventKind[] SilentWithoutSound =
    {
        AudioEventKind.Sonar, AudioEventKind.AirFlyby, AudioEventKind.AirHover, AudioEventKind.RigDeploy, AudioEventKind.RigPack,
    };

    [Fact]
    public void EveryEventKindIsMappedOrHasAStandIn()
    {
        SoundBank bank = Load();
        foreach (AudioEventKind kind in Enum.GetValues<AudioEventKind>())
        {
            if (SilentWithoutSound.Contains(kind)) continue;
            Assert.True(bank.Resolve(new AudioEvent(kind)) is { Sounds.Count: > 0 }, $"no event or stand-in for {AudioEventNames.Of(kind)}");
        }
    }

    [Fact]
    public void TheOriginalEventsStillHavePlainEntries()
    {
        SoundBank bank = Load();
        foreach (AudioEventKind kind in Enum.GetValues<AudioEventKind>().Where(k => k <= AudioEventKind.Promotion))
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
        Assert.Equal(AudioEventKind.FireShell, bank.EventForWeapon("auth_tank_cannon"));
        Assert.Equal(AudioEventKind.FireBullet, bank.EventForWeapon("auth_rifle"));
        Assert.Null(bank.EventForWeapon("auth_bomb"));   // a bomb sounds at its detonation
        Assert.Null(bank.EventForWeapon("no_such_weapon"));
    }

    [Fact]
    public void UnknownSoundInAnEventIsRejected()
    {
        const string text = "[sound.a]\nfile = \"a.wav\"\n[event.fire_bullet]\nsounds = [\"b\"]\n";
        Assert.Throws<InvalidDataException>(() => SoundBank.Parse(text));
    }

    // S22 (owner decision A): one-shots are 22.05 kHz mono (a 6 s clip is about 264 KB), ambience loops (amb_*.wav) may be stereo
    // (a 30 s loop at 22.05 kHz is about 2.65 MB). All of data/audio goes through Git LFS, so a pointer file here means LFS was not checked out.
    private const long OneShotMaxBytes = 270_000;
    private const long LoopMaxBytes = 2_700_000;
    private const long TotalMaxBytes = 40_000_000;

    [Fact]
    public void AllWavFilesAreSmall16Bit_MonoOneShotsAndStereoLoops()
    {
        string dir = Path.Combine(AudioTestSupport.AudioDir(), "sfx");
        string[] files = Directory.GetFiles(dir, "*.wav");
        Assert.True(files.Length >= 20);
        long total = 0;
        foreach (string f in files)
        {
            byte[] b = File.ReadAllBytes(f);
            total += b.Length;
            bool loop = Path.GetFileName(f).StartsWith("amb_", StringComparison.Ordinal);
            Assert.True(b.Length > 1000 && System.Text.Encoding.ASCII.GetString(b, 0, 4) == "RIFF", $"{f} is not a WAV (an LFS pointer? check out with git lfs)");
            Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(b, 8, 4));
            int channels = BitConverter.ToInt16(b, 22);
            Assert.Equal(16, BitConverter.ToInt16(b, 34));  // bits
            if (loop)
            {
                Assert.InRange(channels, 1, 2);
                Assert.True(BitConverter.ToInt32(b, 24) <= 32000, f);
                Assert.True(b.Length <= LoopMaxBytes, f);
            }
            else
            {
                Assert.Equal(1, channels);
                Assert.True(BitConverter.ToInt32(b, 24) <= 22050, f);
                Assert.True(b.Length <= OneShotMaxBytes, f);
            }
        }

        Assert.True(total < TotalMaxBytes);
    }
}
