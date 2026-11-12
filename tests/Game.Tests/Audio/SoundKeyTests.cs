// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Tests.Audio;

/// <summary>S22: the qualified sounds.toml keys, their fallback order, and the weapon class table.</summary>
public sealed class SoundKeyTests
{
    private const string Sounds = """
        [sound.a]
        file = "a.wav"
        [sound.b]
        file = "b.wav"
        [sound.c]
        file = "c.wav"
        [sound.d]
        file = "d.wav"
        [sound.e]
        file = "e.wav"
        """;

    private static SoundBank Bank(string events) => SoundBank.Parse(Sounds + "\n" + events);

    private static string First(SoundBank bank, AudioEvent e) => bank.Resolve(e)?.Sounds[0] ?? "(silence)";

    [Fact]
    public void LookupTriesClassAndFactionThenClassThenFactionThenThePlainEvent()
    {
        Assert.Equal(
            new[] { "unit_died.infantry.directorate", "unit_died.infantry", "unit_died.directorate", "unit_died" },
            SoundBank.LookupKeys(new AudioEvent(AudioEventKind.UnitDied, null, "infantry", "directorate")));
        Assert.Equal(new[] { "unit_died.infantry", "unit_died" }, SoundBank.LookupKeys(new AudioEvent(AudioEventKind.UnitDied, null, "infantry")));
        Assert.Equal(new[] { "building_placed.authority", "building_placed" }, SoundBank.LookupKeys(new AudioEvent(AudioEventKind.BuildingPlaced, null, null, "authority")));
        Assert.Equal(new[] { "unit_died" }, SoundBank.LookupKeys(new AudioEvent(AudioEventKind.UnitDied)));
    }

    [Fact]
    public void TheMostSpecificEntryWins_AndEachLevelFallsBack()
    {
        SoundBank bank = Bank("""
            [event.unit_died]
            sounds = ["a"]
            [event.unit_died.infantry]
            sounds = ["b"]
            [event.unit_died.infantry.directorate]
            sounds = ["c"]
            """);
        Assert.Equal("c", First(bank, new AudioEvent(AudioEventKind.UnitDied, null, "infantry", "directorate")));
        Assert.Equal("b", First(bank, new AudioEvent(AudioEventKind.UnitDied, null, "infantry", "authority")));
        Assert.Equal("b", First(bank, new AudioEvent(AudioEventKind.UnitDied, null, "infantry")));
        Assert.Equal("a", First(bank, new AudioEvent(AudioEventKind.UnitDied, null, "vehicle", "directorate")));
        Assert.Equal("a", First(bank, new AudioEvent(AudioEventKind.UnitDied)));
    }

    [Fact]
    public void FactionOnlyEntryIsUsedWhenThereIsNoClassEntry()
    {
        SoundBank bank = Bank("""
            [event.building_placed]
            sounds = ["a"]
            [event.building_placed.authority]
            sounds = ["b"]
            """);
        Assert.Equal("b", First(bank, new AudioEvent(AudioEventKind.BuildingPlaced, null, null, "authority")));
        Assert.Equal("a", First(bank, new AudioEvent(AudioEventKind.BuildingPlaced, null, null, "directorate")));
        Assert.Equal("a", First(bank, new AudioEvent(AudioEventKind.BuildingPlaced)));
    }

    [Fact]
    public void QuotedDottedKeysWorkLikeNestedTables()
    {
        SoundBank bank = Bank("""
            [event.fire_shell]
            sounds = ["a"]
            [event."fire_shell.cannon.authority"]
            sounds = ["b"]
            """);
        Assert.Equal("b", First(bank, new AudioEvent(AudioEventKind.FireShell, null, "cannon", "authority")));
        Assert.Equal("a", First(bank, new AudioEvent(AudioEventKind.FireShell, null, "cannon", "expedition")));
    }

    [Fact]
    public void WithoutAnyUnitDiedEntryADeathIsASmallExplosion()
    {
        SoundBank bank = Bank("""
            [event.explosion_small]
            sounds = ["a"]
            [event.explosion_large]
            sounds = ["b"]
            """);
        Assert.Equal("a", First(bank, new AudioEvent(AudioEventKind.UnitDied, null, "ship", "expedition")));
        Assert.Equal("b", First(bank, new AudioEvent(AudioEventKind.BuildingDestroyed, null, null, "directorate")));
        Assert.Equal("a", First(bank, new AudioEvent(AudioEventKind.WaterImpact)));
        Assert.Equal("b", First(bank, new AudioEvent(AudioEventKind.WaterImpact, null, "large")));
    }

    [Fact]
    public void ADeathEntryOfAnotherClassDoesNotHideTheStandIn()
    {
        // only unit_died.infantry exists: a vehicle death falls through the unit_died keys to the explosion
        SoundBank bank = Bank("""
            [event.explosion_small]
            sounds = ["a"]
            [event.unit_died.infantry]
            sounds = ["b"]
            """);
        Assert.Equal("b", First(bank, new AudioEvent(AudioEventKind.UnitDied, null, "infantry", "authority")));
        Assert.Equal("a", First(bank, new AudioEvent(AudioEventKind.UnitDied, null, "vehicle", "authority")));
    }

    [Fact]
    public void ShipHornFallsBackToUnitReadyOfShipsThenPlainUnitReady()
    {
        SoundBank bank = Bank("""
            [event.unit_ready]
            sounds = ["a"]
            [event.unit_ready.expedition]
            sounds = ["b"]
            """);
        Assert.Equal("b", First(bank, new AudioEvent(AudioEventKind.ShipHorn, null, "ship", "expedition")));
        Assert.Equal("a", First(bank, new AudioEvent(AudioEventKind.ShipHorn, null, "ship", "authority")));
        SoundBank withHorn = Bank("""
            [event.unit_ready]
            sounds = ["a"]
            [event.ship_horn]
            sounds = ["c"]
            """);
        Assert.Equal("c", First(withHorn, new AudioEvent(AudioEventKind.ShipHorn, null, "ship", "expedition")));
    }

    [Fact]
    public void EventsWithoutAStandInAreSilent()
    {
        SoundBank bank = Bank("[event.explosion_small]\nsounds = [\"a\"]\n");
        foreach (AudioEventKind kind in new[] { AudioEventKind.Sonar, AudioEventKind.AirFlyby, AudioEventKind.AirHover, AudioEventKind.RigDeploy, AudioEventKind.RigPack })
        {
            Assert.Null(bank.Resolve(new AudioEvent(kind, null, null, "authority")));
        }
    }

    [Fact]
    public void AnUnknownSoundInAQualifiedEventIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => Bank("[event.unit_died.infantry]\nsounds = [\"nope\"]\n"));
    }

    [Fact]
    public void AmbienceEntriesParseAndNeedAKnownSound()
    {
        SoundBank bank = Bank("[ambience.glass_coast]\nsound = \"a\"\nvolume = 0.4\n");
        Assert.Equal(new AmbienceDef("glass_coast", "a", 0.4f), bank.AmbienceFor("glass_coast"));
        Assert.Null(bank.AmbienceFor("rust_belt"));
        Assert.Throws<InvalidDataException>(() => Bank("[ambience.glass_coast]\nsound = \"missing\"\n"));
        Assert.Throws<InvalidDataException>(() => Bank("[ambience.glass_coast]\nvolume = 0.4\n"));
    }

    [Fact]
    public void TheShippedSoundsTomlResolvesEveryOriginalEventExactlyAsBefore()
    {
        // with only today's files the new keys all fall back: a death is explosion_small, a destroyed building explosion_large, and so on
        SoundBank bank = AudioRulesFixture.Bank;
        Assert.Equal(AudioEventNames.Of(AudioEventKind.ExplosionSmall), bank.Resolve(new AudioEvent(AudioEventKind.UnitDied, null, "infantry", "directorate"))!.Name);
        Assert.Equal(AudioEventNames.Of(AudioEventKind.ExplosionLarge), bank.Resolve(new AudioEvent(AudioEventKind.BuildingDestroyed, null, null, "expedition"))!.Name);
        Assert.Equal(AudioEventNames.Of(AudioEventKind.UnitReady), bank.Resolve(new AudioEvent(AudioEventKind.ShipHorn, null, "ship", "expedition"))!.Name);
        Assert.Equal(AudioEventNames.Of(AudioEventKind.FireShell), bank.Resolve(new AudioEvent(AudioEventKind.FireShell, null, "cannon", "authority"))!.Name);
        Assert.Equal(AudioEventNames.Of(AudioEventKind.BuildingPlaced), bank.Resolve(new AudioEvent(AudioEventKind.BuildingPlaced, null, null, "left_behind"))!.Name);
    }

    [Fact]
    public void EveryWeaponInTheRulesHasASoundClass()
    {
        string path = Path.Combine(AudioTestSupport.RepoRoot(), "data", "rules", "weapons.toml");
        TomlTable weapons = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!;
        SoundBank bank = AudioRulesFixture.Bank;
        var missing = new List<string>();
        foreach (string id in weapons.Keys)
        {
            string? cls = bank.WeaponClassOf(id);
            if (cls is null) missing.Add(id);
        }

        Assert.True(missing.Count == 0, "weapons with no class in the [weapon] table of data/audio/sounds.toml (add one, see WeaponClasses): " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryWeaponClassInTheTableIsKnown()
    {
        foreach (KeyValuePair<string, string> w in AudioRulesFixture.Bank.WeaponClassTable)
        {
            Assert.True(WeaponClasses.IsKnown(w.Value), $"{w.Key} = {w.Value} is not a weapon class");
        }
    }

    [Fact]
    public void EveryClassMapsToAFamilyOrMakesNoShotSound()
    {
        foreach (string cls in WeaponClasses.All)
        {
            Assert.True(WeaponClasses.FamilyOf(cls) is not null || WeaponClasses.MakesNoShotSound(cls), cls);
        }

        Assert.Equal(AudioEventKind.FireBullet, WeaponClasses.FamilyOf("sniper"));
        Assert.Equal(AudioEventKind.FireShell, WeaponClasses.FamilyOf("naval_heavy"));
        Assert.Equal(AudioEventKind.FireMissile, WeaponClasses.FamilyOf("sam"));
        Assert.Null(WeaponClasses.FamilyOf("bomb"));
        Assert.Null(WeaponClasses.FamilyOf("nonsense"));
    }

    [Fact]
    public void WeaponClassesFollowTheKindOfWeapon()
    {
        AudioRules a = AudioRulesFixture.Info;
        int W(string id) => AudioRulesFixture.Wpn(id);
        Assert.Equal("rocket", a.WeaponClass(W("auth_rocket")));
        Assert.Equal("sniper", a.WeaponClass(W("auth_marksman_rifle")));
        Assert.Equal("flame", a.WeaponClass(W("lb_flame_hammer")));
        Assert.Equal("depth_charge", a.WeaponClass(W("exp_launch_charge")));
        Assert.Equal("flak", a.WeaponClass(W("auth_flak_gun")));
        Assert.Equal("bomb", a.WeaponClass(W("exp_bomb_run")));
        Assert.Equal("authority", a.WeaponFaction(W("auth_rocket")));
        Assert.Equal("left_behind", a.WeaponFaction(W("lb_organ_rack")));
        Assert.Equal("expedition", a.WeaponFaction(W("exp_destroyer_gun")));
        Assert.True(a.IsInstant(W("auth_rifle")));
        Assert.False(a.IsInstant(W("auth_rocket")));
    }

    [Fact]
    public void TheFullyGeneratedSoundsTomlParsesAndResolvesTheQualifiedKeys()
    {
        // Fixtures/sounds_all_generated.toml = what `make_sfx.py --pick` writes with all 80 sounds present (nested [event.a.b.c] tables and [ambience.x]).
        string path = Path.Combine(AudioTestSupport.RepoRoot(), "tests", "Game.Tests", "Audio", "Fixtures", "sounds_all_generated.toml");
        SoundBank bank = SoundBank.Load(path);
        Assert.Equal("die_inf_dir", First(bank, new AudioEvent(AudioEventKind.UnitDied, null, "infantry", "directorate")));
        Assert.Equal("die_ship", First(bank, new AudioEvent(AudioEventKind.UnitDied, null, "ship", "expedition")));
        Assert.Equal("bld_place_lb", First(bank, new AudioEvent(AudioEventKind.BuildingPlaced, null, null, "left_behind")));
        Assert.Equal("bld_destroyed_exp", First(bank, new AudioEvent(AudioEventKind.BuildingDestroyed, null, null, "expedition")));
        Assert.Equal("wpn_cannon_auth", First(bank, new AudioEvent(AudioEventKind.FireShell, null, "cannon", "authority")));
        Assert.Equal("wpn_rail_dir", First(bank, new AudioEvent(AudioEventKind.FireShell, null, "rail", "directorate")));
        Assert.Equal("wpn_sniper_long", First(bank, new AudioEvent(AudioEventKind.FireBullet, null, "sniper", "expedition")));
        Assert.Equal("wpn_flame", First(bank, new AudioEvent(AudioEventKind.FireBullet, null, "flame", "left_behind")));
        Assert.Equal("nav_ship_horn", First(bank, new AudioEvent(AudioEventKind.ShipHorn, null, "ship", "expedition")));
        Assert.Equal("nav_sonar_ping", First(bank, new AudioEvent(AudioEventKind.Sonar)));
        Assert.Equal("air_jet_flyby", First(bank, new AudioEvent(AudioEventKind.AirFlyby, null, null, "expedition")));
        Assert.Equal("rig_deploy_lb", First(bank, new AudioEvent(AudioEventKind.RigPack, null, null, "left_behind")));
        Assert.Equal("exp_bomb_heavy", First(bank, new AudioEvent(AudioEventKind.ExplosionLarge, null, "bomb")));
        Assert.Equal("amb_open_sea", bank.AmbienceFor("open_sea")!.SoundId);
        Assert.Equal(5, bank.Ambience.Count());
        // an unlisted faction / class still finds the plain or class-level sound
        Assert.NotNull(bank.Resolve(new AudioEvent(AudioEventKind.UnitDied, null, "vehicle", "authority")));
    }
}
