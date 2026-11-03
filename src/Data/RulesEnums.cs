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
    public static readonly string[] LandKeys = ["clear", "road", "rough", "ash", "water", "rock", "cliff"];

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

    /// <summary>Joins names for a message.</summary>
    public static string Join(string[] names) => string.Join(", ", names);

    private static int? Index(string[] names, string v)
    {
        int i = Array.IndexOf(names, v);
        return i < 0 ? null : i;
    }
}
