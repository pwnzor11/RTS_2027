// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Audio;

/// <summary>What the audio layer needs to know about one unit type: its class key, faction key and the few rule values the deriver reads.</summary>
public readonly record struct AudioUnitInfo(string Class, string? Faction, int PrimaryWeapon, Locomotion Locomotion, ArmorClass Armor, int Speed, int UnpacksInto, bool Submarine = false);

/// <summary>What the audio layer needs to know about one building type.</summary>
public readonly record struct AudioBuildingInfo(string? Faction, int PrimaryWeapon, int PacksInto);

/// <summary>
/// S22: the view of data/rules the audio code reads, built once per game from <see cref="SimRules"/> and the [weapon] table of
/// sounds.toml. It maps a unit or building type to its class (infantry, vehicle, ship, air) and faction key and a weapon index to its sound class
/// and faction. Read only; nothing here reaches the sim.
/// </summary>
public sealed class AudioRules
{
    /// <summary>Aircraft at least this fast (leptons per tick) sound as a flyby (jets, bombers).</summary>
    public const int FlybySpeed = 40;

    /// <summary>Aircraft at least this fast but slower than <see cref="FlybySpeed"/> sound as hovering rotors; slower ones (airships) make no sound.</summary>
    public const int HoverSpeed = 20;

    private readonly AudioUnitInfo[] _vehicles;
    private readonly AudioUnitInfo[] _infantry;
    private readonly AudioBuildingInfo[] _buildings;
    private readonly string?[] _weaponClass;
    private readonly string?[] _weaponFaction;
    private readonly int[] _weaponSpeed;
    private readonly int[] _weaponRange;

    private AudioRules(AudioUnitInfo[] vehicles, AudioUnitInfo[] infantry, AudioBuildingInfo[] buildings, string?[] weaponClass,
        string?[] weaponFaction, int[] weaponSpeed, int[] weaponRange)
    {
        _vehicles = vehicles;
        _infantry = infantry;
        _buildings = buildings;
        _weaponClass = weaponClass;
        _weaponFaction = weaponFaction;
        _weaponSpeed = weaponSpeed;
        _weaponRange = weaponRange;
    }

    /// <summary>The faction key used in sounds.toml (<c>authority</c>, <c>directorate</c>, <c>left_behind</c>, <c>expedition</c>); null for neutral.</summary>
    public static string? FactionKey(Faction f) => f switch
    {
        Faction.Authority => "authority",
        Faction.Directorate => "directorate",
        Faction.LeftBehind => "left_behind",
        Faction.Expedition => "expedition",
        _ => null,
    };

    /// <summary>Builds the lookup tables for <paramref name="rules"/>; weapon classes come from the [weapon] table of <paramref name="bank"/>.</summary>
    public static AudioRules Build(SimRules rules, SoundBank bank)
    {
        var vehicles = new AudioUnitInfo[rules.Vehicles.Count];
        for (int i = 0; i < vehicles.Length; i++)
        {
            VehicleType v = rules.Vehicles[i];
            string cls = v.Locomotion switch
            {
                Locomotion.Naval => "ship",
                Locomotion.Air => "air",
                _ => "vehicle",
            };
            vehicles[i] = new AudioUnitInfo(cls, FactionKey(v.Faction), v.PrimaryWeapon, v.Locomotion, v.Armor, v.Speed, v.UnpacksInto, v.Submerges);
        }

        var infantry = new AudioUnitInfo[rules.Infantry.Count];
        for (int i = 0; i < infantry.Length; i++)
        {
            InfantryType t = rules.Infantry[i];
            infantry[i] = new AudioUnitInfo("infantry", FactionKey(t.Faction), t.PrimaryWeapon, Locomotion.Foot, t.Armor, t.Speed, -1);
        }

        var buildings = new AudioBuildingInfo[rules.Buildings.Count];
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingType b = rules.Buildings[i];
            buildings[i] = new AudioBuildingInfo(FactionKey(b.Faction), b.PrimaryWeapon, b.PacksInto);
        }

        int n = rules.Weapons.Count;
        var cls2 = new string?[n];
        var fac = new string?[n];
        var speed = new int[n];
        var range = new int[n];
        for (int i = 0; i < n; i++)
        {
            WeaponType w = rules.Weapons[i];
            cls2[i] = bank.WeaponClassOf(w.Id);
            fac[i] = FactionKey(SimRules.FactionFromId(w.Id));
            speed[i] = w.ProjectileSpeed;
            range[i] = w.Range;
        }

        return new AudioRules(vehicles, infantry, buildings, cls2, fac, speed, range);
    }

    /// <summary>Class, faction and weapon of a unit type (a default unit info when the index is out of range).</summary>
    public AudioUnitInfo Unit(UnitKind kind, int typeIndex)
    {
        AudioUnitInfo[] table = kind == UnitKind.Infantry ? _infantry : _vehicles;
        return (uint)typeIndex < (uint)table.Length ? table[typeIndex] : new AudioUnitInfo(kind == UnitKind.Infantry ? "infantry" : "vehicle", null, -1, Locomotion.Foot, ArmorClass.Unarmored, 0, -1);
    }

    /// <summary>Faction and weapon of a building type.</summary>
    public AudioBuildingInfo Building(int typeIndex) =>
        (uint)typeIndex < (uint)_buildings.Length ? _buildings[typeIndex] : new AudioBuildingInfo(null, -1, -1);

    /// <summary>The sound class of a weapon index, or null when the weapon has no class (the generic fire sound plays).</summary>
    public string? WeaponClass(int weapon) => (uint)weapon < (uint)_weaponClass.Length ? _weaponClass[weapon] : null;

    /// <summary>The faction key of a weapon (from its id prefix), or null.</summary>
    public string? WeaponFaction(int weapon) => (uint)weapon < (uint)_weaponFaction.Length ? _weaponFaction[weapon] : null;

    /// <summary>True when the weapon hits at once (projectile speed 0), so no projectile appears for it.</summary>
    public bool IsInstant(int weapon) => (uint)weapon < (uint)_weaponSpeed.Length && _weaponSpeed[weapon] == 0;

    /// <summary>Range of a weapon in leptons (0 when unknown).</summary>
    public int WeaponRange(int weapon) => (uint)weapon < (uint)_weaponRange.Length ? _weaponRange[weapon] : 0;

    /// <summary>The air sound of an aircraft type: <see cref="AudioEventKind.AirFlyby"/>, <see cref="AudioEventKind.AirHover"/> or null for none.</summary>
    public static AudioEventKind? AirSound(AudioUnitInfo u) =>
        u.Locomotion != Locomotion.Air ? null
        : u.Speed >= FlybySpeed ? AudioEventKind.AirFlyby
        : u.Speed >= HoverSpeed ? AudioEventKind.AirHover
        : null;
}
