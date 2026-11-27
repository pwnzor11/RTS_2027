// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;

namespace Rts.Game.Ui.Alerts;

/// <summary>QOL-12 (D-313): the groups of alerts the player can quieten or mute from Settings. Append only: the values are not saved, the names are.</summary>
public enum AlertCategory
{
    /// <summary>Base, unit and harvester under attack.</summary>
    Attack,
    /// <summary>A building or a unit was lost.</summary>
    Losses,
    /// <summary>Construction complete, unit ready, upgrade complete, production idle or blocked, harvester idle.</summary>
    Production,
    /// <summary>Low power.</summary>
    Power,
    /// <summary>Superweapon ready or launched.</summary>
    Superweapon,
}

/// <summary>
/// QOL-12 (D-313): the volume of each <see cref="AlertCategory"/> in percent (0 = muted, 100 = as before; the default). It scales the alert sound
/// of the category (see <see cref="AudioDirector"/>) and a muted category also shows no line in the alert stack and no minimap ping. View and
/// sound only: the sim is untouched. Immutable; <see cref="Active"/> holds the mix in force, which <c>GameServices.Settings</c> keeps in step.
/// </summary>
public sealed class AlertMix : IEquatable<AlertMix>
{
    /// <summary>The volumes of one step of the Settings row.</summary>
    public const int Step = 25;

    private readonly int[] _volumes;

    private AlertMix(int[] volumes) => _volumes = volumes;

    /// <summary>Every category at 100 percent: today's behaviour.</summary>
    public static AlertMix Default { get; } = new(Enumerable.Repeat(100, Enum.GetValues<AlertCategory>().Length).ToArray());

    private static AlertMix? _active;

    /// <summary>The mix in force (the default when none was set).</summary>
    public static AlertMix Active
    {
        get => _active ?? Default;
        set => _active = value;
    }

    /// <summary>True when every category is at 100 percent.</summary>
    public bool IsDefault => Equals(Default);

    /// <summary>The volume of a category, 0..100.</summary>
    public int Get(AlertCategory category) => _volumes[(int)category];

    /// <summary>True when the category is muted (volume 0).</summary>
    public bool IsMuted(AlertCategory category) => Get(category) == 0;

    /// <summary>The volume as a gain, 0..1.</summary>
    public float GainOf(AlertCategory category) => Get(category) / 100f;

    /// <summary>This mix with one category set to <paramref name="volume"/> (clamped to 0..100).</summary>
    public AlertMix With(AlertCategory category, int volume)
    {
        int v = Math.Clamp(volume, 0, 100);
        if (_volumes[(int)category] == v) return this;
        var copy = (int[])_volumes.Clone();
        copy[(int)category] = v;
        return new AlertMix(copy);
    }

    /// <summary>The volume after one step up or down (<see cref="Step"/> percent), clamped.</summary>
    public static int Stepped(int volume, int direction) => Math.Clamp(volume + direction * Step, 0, 100);

    /// <summary>The category of an alert line.</summary>
    public static AlertCategory CategoryOf(AlertKind kind) => kind switch
    {
        AlertKind.BaseUnderAttack or AlertKind.UnitUnderAttack or AlertKind.HarvesterUnderAttack or AlertKind.EnemySpotted => AlertCategory.Attack,
        AlertKind.BuildingLost or AlertKind.UnitLost => AlertCategory.Losses,
        AlertKind.LowPower => AlertCategory.Power,
        AlertKind.SuperweaponReady or AlertKind.SuperweaponLaunched => AlertCategory.Superweapon,
        _ => AlertCategory.Production,
    };

    /// <summary>The category of an alert sound, or null when the sound is not an alert (a shot, a click): those are never scaled.</summary>
    public static AlertCategory? CategoryOf(AudioEventKind kind) => kind switch
    {
        AudioEventKind.UnderAttack => AlertCategory.Attack,
        AudioEventKind.ConstructionComplete or AudioEventKind.UnitReady or AudioEventKind.UpgradeComplete or AudioEventKind.LowFunds => AlertCategory.Production,
        AudioEventKind.LowPower => AlertCategory.Power,
        _ => null,
    };

    /// <summary>The gain for an alert sound: 1 for a sound that is not an alert, else the category's gain.</summary>
    public float GainOf(AudioEventKind kind) => CategoryOf(kind) is { } c ? GainOf(c) : 1f;

    /// <summary>The text id of a category in the settings file.</summary>
    public static string IdOf(AlertCategory category) => category switch
    {
        AlertCategory.Attack => "attack",
        AlertCategory.Losses => "losses",
        AlertCategory.Production => "production",
        AlertCategory.Power => "power",
        _ => "superweapon",
    };

    /// <summary>The name shown in Settings.</summary>
    public static string NameOf(AlertCategory category) => category switch
    {
        AlertCategory.Attack => "Under attack",
        AlertCategory.Losses => "Losses",
        AlertCategory.Production => "Production",
        AlertCategory.Power => "Low power",
        _ => "Superweapons",
    };

    /// <summary>Builds a mix from (id, volume) pairs; unknown ids are ignored and volumes are clamped.</summary>
    public static AlertMix FromPairs(IEnumerable<(string Id, long Volume)> pairs)
    {
        AlertMix mix = Default;
        foreach ((string id, long volume) in pairs)
            foreach (AlertCategory c in Enum.GetValues<AlertCategory>())
                if (IdOf(c) == id) mix = mix.With(c, (int)Math.Clamp(volume, 0, 100));
        return mix;
    }

    /// <summary>The categories that are off 100 percent, as (id, volume) pairs.</summary>
    public IReadOnlyList<(string Id, int Volume)> Overrides()
    {
        var list = new List<(string, int)>();
        foreach (AlertCategory c in Enum.GetValues<AlertCategory>())
            if (Get(c) != 100) list.Add((IdOf(c), Get(c)));
        return list;
    }

    /// <inheritdoc />
    public bool Equals(AlertMix? other) => other is not null && _volumes.AsSpan().SequenceEqual(other._volumes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as AlertMix);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (int v in _volumes) h.Add(v);
        return h.ToHashCode();
    }
}
