// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>
/// Enum names as written in the TOML files (lowercase). Each array is in enum-value order, so the index of a
/// name is its enum value.
/// </summary>
internal static class RulesEnums
{
    /// <summary>Locomotion names, in <see cref="Locomotion"/> order.</summary>
    public static readonly string[] LocomotionKeys = ["foot", "wheeled", "tracked", "hover", "air", "burrow"];

    /// <summary>Armour class names, in <see cref="ArmorClass"/> order.</summary>
    public static readonly string[] ArmorKeys = ["unarmored", "light", "heavy", "structure", "fortified"];

    /// <summary>Land type names, in <see cref="LandType"/> order.</summary>
    public static readonly string[] LandKeys = ["clear", "road", "rough", "ash", "water", "rock", "cliff", "shallow"];

    /// <summary>Faction names, in <see cref="Faction"/> order.</summary>
    public static readonly string[] FactionKeys = ["authority", "directorate", "neutral"];

    /// <summary>Tunnel size names, in <see cref="TunnelSize"/> order (design 05 writes them none | S | M | L).</summary>
    public static readonly string[] TunnelSizeKeys = ["none", "S", "M", "L"];

    /// <summary>Entrance sizes a building may declare: none, S or M (L never uses the network).</summary>
    public static readonly string[] EntranceKeys = ["none", "S", "M"];

    /// <summary>Weapon target names, in <see cref="WeaponTargets"/> order.</summary>
    public static readonly string[] WeaponTargetKeys = ["ground", "air", "both"];

    /// <summary>Effect operations.</summary>
    public static readonly string[] EffectOps = ["enable", "set", "add", "mul"];

    /// <summary>Class names accepted after the <c>class:</c> prefix in prerequisites and effect targets.</summary>
    public static readonly string[] ClassNames = ["vehicle", "infantry", "building"];

    /// <summary>Prefix of a class reference.</summary>
    public const string ClassPrefix = "class:";

    /// <summary>Parses a locomotion name.</summary>
    public static Locomotion? ParseLocomotion(string v) => Index(LocomotionKeys, v) is int i ? (Locomotion)i : null;

    /// <summary>Parses an armour class name.</summary>
    public static ArmorClass? ParseArmor(string v) => Index(ArmorKeys, v) is int i ? (ArmorClass)i : null;

    /// <summary>Parses a land type name.</summary>
    public static LandType? ParseLand(string v) => Index(LandKeys, v) is int i ? (LandType)i : null;

    /// <summary>Parses a faction name.</summary>
    public static Faction? ParseFaction(string v) => Index(FactionKeys, v) is int i ? (Faction)i : null;

    /// <summary>Parses a tunnel size name.</summary>
    public static TunnelSize? ParseTunnelSize(string v) => Index(TunnelSizeKeys, v) is int i ? (TunnelSize)i : null;

    /// <summary>Parses an entrance size name (none, S or M).</summary>
    public static TunnelSize? ParseEntrance(string v) => Index(EntranceKeys, v) is int i ? (TunnelSize)i : null;

    /// <summary>Parses a weapon target name.</summary>
    public static WeaponTargets? ParseWeaponTargets(string v) => Index(WeaponTargetKeys, v) is int i ? (WeaponTargets)i : null;

    /// <summary>Joins names for a message.</summary>
    public static string Join(string[] names) => string.Join(", ", names);

    private static int? Index(string[] names, string v)
    {
        int i = Array.IndexOf(names, v);
        return i < 0 ? null : i;
    }
}
