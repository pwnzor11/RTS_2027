// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>
/// The sound classes a weapon can have (the [weapon] table of sounds.toml maps every weapon id of data/rules/weapons.toml to one). The class
/// picks the event family (<c>fire_bullet</c>, <c>fire_shell</c> or <c>fire_missile</c>) and is the first qualifier of the lookup, for example
/// <c>fire_shell.cannon.authority</c>. Two classes make no fire sound: <c>silent</c> (death and self-destruct weapons, whose sound is the
/// explosion) and <c>bomb</c> (the sound is the detonation, <c>explosion_large.bomb</c>).
/// </summary>
public static class WeaponClasses
{
    /// <summary>A weapon whose own shot makes no sound.</summary>
    public const string Silent = "silent";

    /// <summary>A bomb: no shot sound, the detonation plays <c>explosion_large.bomb</c>.</summary>
    public const string Bomb = "bomb";

    private static readonly Dictionary<string, AudioEventKind?> Families = new(StringComparer.Ordinal)
    {
        ["small_arms"] = AudioEventKind.FireBullet,
        ["sniper"] = AudioEventKind.FireBullet,
        ["mg"] = AudioEventKind.FireBullet,
        ["flame"] = AudioEventKind.FireBullet,
        ["spray"] = AudioEventKind.FireBullet,
        ["cannon"] = AudioEventKind.FireShell,
        ["rail"] = AudioEventKind.FireShell,
        ["energy"] = AudioEventKind.FireShell,
        ["mortar"] = AudioEventKind.FireShell,
        ["howitzer"] = AudioEventKind.FireShell,
        ["flak"] = AudioEventKind.FireShell,
        ["naval_shell"] = AudioEventKind.FireShell,
        ["naval_heavy"] = AudioEventKind.FireShell,
        ["depth_charge"] = AudioEventKind.FireShell,
        ["rocket"] = AudioEventKind.FireMissile,
        ["salvo"] = AudioEventKind.FireMissile,
        ["sam"] = AudioEventKind.FireMissile,
        ["harpoon"] = AudioEventKind.FireMissile,
        [Bomb] = null,
        [Silent] = null,
    };

    /// <summary>Every class name, ordered.</summary>
    public static IEnumerable<string> All => Families.Keys.OrderBy(k => k, StringComparer.Ordinal);

    /// <summary>True when <paramref name="cls"/> is a known class.</summary>
    public static bool IsKnown(string cls) => Families.ContainsKey(cls);

    /// <summary>The event family a shot of this class raises, or null for a class that makes no shot sound (and for an unknown class).</summary>
    public static AudioEventKind? FamilyOf(string? cls) => cls is not null && Families.TryGetValue(cls, out AudioEventKind? k) ? k : null;

    /// <summary>True for <see cref="Silent"/> and <see cref="Bomb"/>: the shot raises no event.</summary>
    public static bool MakesNoShotSound(string? cls) => cls is Silent or Bomb;
}
