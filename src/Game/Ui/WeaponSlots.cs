// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Ui;

/// <summary>
/// S44 (M-04): the weapon slots of a vehicle type as the UI lists them. Only vehicles carry a second and third slot. Slot order is primary,
/// secondary, tertiary; empty (-1) and out-of-range slots are skipped, so a one-weapon unit yields exactly its primary weapon and every
/// reader keeps its old layout for it. Pure and display only.
/// </summary>
public static class WeaponSlots
{
    /// <summary>The armed weapon indexes of a vehicle, in slot order (empty when it has none).</summary>
    public static IReadOnlyList<int> Of(SimRules rules, VehicleType vehicle)
    {
        var list = new List<int>(3);
        Add(rules, list, vehicle.PrimaryWeapon);
        Add(rules, list, vehicle.SecondaryWeapon);
        Add(rules, list, vehicle.TertiaryWeapon);
        return list;
    }

    /// <summary>The armed weapon indexes of a vehicle type index, or empty for a bad index.</summary>
    public static IReadOnlyList<int> OfVehicle(SimRules rules, int typeIndex) =>
        (uint)typeIndex < (uint)rules.Vehicles.Count ? Of(rules, rules.Vehicles[typeIndex]) : Array.Empty<int>();

    /// <summary>What a weapon may fire at, in player words.</summary>
    public static string TargetsText(WeaponTargets targets) => targets switch
    {
        WeaponTargets.Air => "air",
        WeaponTargets.Both => "ground and air",
        WeaponTargets.Naval => "ships",
        WeaponTargets.Submerged => "submarines",
        WeaponTargets.NavalAndSubmerged => "ships and submarines",
        _ => "ground",
    };

    private static void Add(SimRules rules, List<int> list, int weapon)
    {
        if (weapon >= 0 && weapon < rules.Weapons.Count) list.Add(weapon);
    }
}
