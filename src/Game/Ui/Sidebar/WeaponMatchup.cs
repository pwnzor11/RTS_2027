// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// The "Strong vs / Weak vs" tooltip lines (M13-15, D-205), generated from the warhead x armour table of the type's primary weapon: the two
/// armour classes it hurts most and the two it hurts least. Only the five land classes (unarmoured, light, heavy, structure, fortified) are
/// listed; the ship classes are padded with 100 in most warheads and would only add noise. A weapon that does the same damage to every land
/// class has no matchup lines. Pure, so it is unit-tested.
/// </summary>
public static class WeaponMatchup
{
    /// <summary>How many classes each line lists.</summary>
    public const int PerLine = 2;

    private static readonly ArmorClass[] Land =
        { ArmorClass.Unarmored, ArmorClass.Light, ArmorClass.Heavy, ArmorClass.Structure, ArmorClass.Fortified };

    /// <summary>The player-facing name of an armour class.</summary>
    public static string ClassName(ArmorClass armor) => armor switch
    {
        ArmorClass.Unarmored => "unarmoured",
        ArmorClass.Light => "light armour",
        ArmorClass.Heavy => "heavy armour",
        ArmorClass.Structure => "structures",
        ArmorClass.Fortified => "fortified",
        ArmorClass.ShipLight => "light ships",
        ArmorClass.ShipHeavy => "heavy ships",
        _ => "submarines",
    };

    /// <summary>
    /// The strong and weak armour classes of weapon <paramref name="weapon"/> (an index into <see cref="SimRules.Weapons"/>), best and worst
    /// first; both empty when the weapon is missing or has no spread. A class never appears in both lists.
    /// </summary>
    public static (IReadOnlyList<ArmorClass> Strong, IReadOnlyList<ArmorClass> Weak) Classes(SimRules rules, int weapon)
    {
        var none = (Array.Empty<ArmorClass>(), Array.Empty<ArmorClass>());
        if ((uint)weapon >= (uint)rules.Weapons.Count) return none;
        int warhead = rules.Weapons[weapon].Warhead;
        if ((uint)warhead >= (uint)rules.Warheads.Count) return none;
        IReadOnlyList<int> versus = rules.Warheads[warhead].VersusPercent;
        int Pct(ArmorClass c) => (int)c < versus.Count ? versus[(int)c] : 100;
        int max = Land.Max(Pct), min = Land.Min(Pct);
        if (max == min) return none;

        List<ArmorClass> strong = Land.Where(c => Pct(c) > min).OrderByDescending(Pct).ThenBy(c => (int)c).Take(PerLine).ToList();
        List<ArmorClass> weak = Land.Where(c => Pct(c) < max && !strong.Contains(c)).OrderBy(Pct).ThenBy(c => (int)c).Take(PerLine).ToList();
        return (strong, weak);
    }

    /// <summary>The primary weapon index of a type, or -1 (no weapon, bad index, or a category that fights nothing).</summary>
    public static int WeaponOf(SimRules rules, ProductionCategory category, int typeIndex) => category switch
    {
        ProductionCategory.Infantry when (uint)typeIndex < (uint)rules.Infantry.Count => rules.Infantry[typeIndex].PrimaryWeapon,
        ProductionCategory.Vehicle or ProductionCategory.Aircraft when (uint)typeIndex < (uint)rules.Vehicles.Count => rules.Vehicles[typeIndex].PrimaryWeapon,
        ProductionCategory.Defence or ProductionCategory.Structure when (uint)typeIndex < (uint)rules.Buildings.Count => rules.Buildings[typeIndex].PrimaryWeapon,
        _ => -1,
    };

    /// <summary>
    /// The tooltip lines for a type: "Strong vs: heavy armour 120%, structures 100%" and "Weak vs: fortified 5%, structures 10%". Empty for a
    /// type without a weapon or without a spread.
    /// </summary>
    public static List<string> Lines(SimRules rules, ProductionCategory category, int typeIndex)
    {
        var lines = new List<string>();
        int weapon = WeaponOf(rules, category, typeIndex);
        (IReadOnlyList<ArmorClass> strong, IReadOnlyList<ArmorClass> weak) = Classes(rules, weapon);
        if (strong.Count == 0 && weak.Count == 0) return lines;
        IReadOnlyList<int> versus = rules.Warheads[rules.Weapons[weapon].Warhead].VersusPercent;
        string Join(IReadOnlyList<ArmorClass> list) =>
            string.Join(", ", list.Select(c => string.Create(CultureInfo.InvariantCulture, $"{ClassName(c)} {((int)c < versus.Count ? versus[(int)c] : 100)}%")));
        if (strong.Count > 0) lines.Add(UiText.Format(UiText.StrongVs, "classes", Join(strong)));
        if (weak.Count > 0) lines.Add(UiText.Format(UiText.WeakVs, "classes", Join(weak)));
        return lines;
    }
}
