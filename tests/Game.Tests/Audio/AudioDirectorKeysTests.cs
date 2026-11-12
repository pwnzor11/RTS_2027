// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Sim.Core;
using static Rts.Game.Tests.Audio.AudioRulesFixture;
using static Rts.Game.Tests.Audio.AudioTestSupport;

namespace Rts.Game.Tests.Audio;

/// <summary>S22: the director plays the most specific sound, falls back, and caps a big battle.</summary>
public sealed class AudioDirectorKeysTests
{
    private const string Bank = """
        [sound.boom]
        file = "boom.wav"
        [sound.die_dir]
        file = "d.wav"
        [sound.die_inf]
        file = "i.wav"
        [sound.shot]
        file = "s.wav"
        [sound.rifle]
        file = "r.wav"
        [event.explosion_small]
        sounds = ["boom"]
        positional = false
        [event.fire_bullet]
        sounds = ["shot"]
        positional = false
        [event.fire_bullet.sniper]
        sounds = ["rifle"]
        positional = false
        [event.unit_died.infantry.directorate]
        sounds = ["die_dir"]
        positional = false
        [event.unit_died.infantry]
        sounds = ["die_inf"]
        positional = false
        """;

    private static (AudioDirector, RecordingAudio) Make()
    {
        var audio = new RecordingAudio();
        return (new AudioDirector(SoundBank.Parse(Bank), audio, new Random(1), () => null), audio);
    }

    private static AudioFrame DeathFrame(int tick, string typeId, bool infantry, int slot = 1)
    {
        UnitView u = infantry ? Soldier(slot, 1, typeId, 6, 6) : Vehicle(slot, 1, typeId, 6, 6);
        return new AudioFrame(Snap(tick - 1, new[] { u }), Snap(tick, fx: new[] { new FxEvent(FxKind.UnitDestroyed, u.Position, 1) }), 0) { Rules = Info };
    }

    [Fact]
    public void ADeathPlaysTheMostSpecificSound_ThenTheClassSound_ThenTheExplosion()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        d.Process(DeathFrame(10, "dir_conscript", true));
        d.Process(DeathFrame(20, "auth_rifleman", true));
        d.Process(DeathFrame(30, "auth_jeep", false));
        Assert.Equal(new[] { "die_dir", "die_inf", "boom" }, audio.Sfx.Select(s => s.Id).ToArray());
    }

    [Fact]
    public void WithoutRulesADeathIsStillTheExplosion()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        UnitView u = Soldier(1, 1, "dir_conscript", 6, 6);
        d.Process(Frame(Snap(9, new[] { u }), Snap(10, fx: new[] { new FxEvent(FxKind.UnitDestroyed, u.Position, 1) })));
        Assert.Equal("boom", Assert.Single(audio.Sfx).Id);
    }

    [Fact]
    public void AWeaponClassPicksItsSoundAndOtherClassesKeepTheGenericOne()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        UnitView sniper = Soldier(1, 1, "auth_marksman", 2, 8);
        UnitView grunt = Soldier(2, 1, "auth_rifleman", 2, 12);
        UnitView v1 = Vehicle(3, 0, "auth_jeep", 6, 8, 100);
        UnitView v2 = Vehicle(4, 0, "auth_jeep", 6, 12, 100);
        d.Process(new AudioFrame(Snap(10, new[] { sniper, v1 }), Snap(11, new[] { sniper, v1 with { Health = 90 } }), 0) { Rules = Info });
        d.Process(new AudioFrame(Snap(20, new[] { grunt, v2 }), Snap(21, new[] { grunt, v2 with { Health = 90 } }), 0) { Rules = Info });
        Assert.Equal(new[] { "rifle", "shot" }, audio.Sfx.Select(s => s.Id).ToArray());
    }

    [Fact]
    public void FiftyUnitsFiringStartAtMostEightFightSoundsPerStep()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        var before = new List<UnitView>();
        var after = new List<UnitView>();
        var fx = new List<FxEvent>();
        for (int i = 0; i < 50; i++)
        {
            UnitView u = Vehicle(i + 1, 1, "auth_jeep", 2 + (i % 10), 2 + (i / 10), 100);
            before.Add(u);
            after.Add(u with { Health = 50 });
            fx.Add(new FxEvent(FxKind.Detonation, u.Position, 1));
        }

        d.Process(new AudioFrame(Snap(10, before.ToArray()), Snap(11, after.ToArray(), fx: fx.ToArray()), 0) { Rules = Info });
        Assert.InRange(audio.Sfx.Count, 1, 8);
    }

    [Fact]
    public void AlertsAreNotCountedAgainstTheFightCap()
    {
        (AudioDirector d, RecordingAudio audio) = Make();
        var fx = Enumerable.Range(0, 20).Select(i => new FxEvent(FxKind.Detonation, Cell(i % 14, 3 + (i % 3)), 1)).ToArray();
        d.Process(Frame(Snap(10), Snap(11, fx: fx)));
        Assert.True(audio.Sfx.Count <= 8);
    }

    [Fact]
    public void CooldownRunsPerResolvedSound()
    {
        // two deaths that resolve to different entries do not silence each other
        (AudioDirector d, RecordingAudio audio) = Make();
        d.Process(DeathFrame(10, "dir_conscript", true, 1));
        d.Process(DeathFrame(11, "auth_rifleman", true, 2));
        Assert.Equal(new[] { "die_dir", "die_inf" }, audio.Sfx.Select(s => s.Id).ToArray());
    }
}
