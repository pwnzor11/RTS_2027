// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>Parses building entries (buildings.toml).</summary>
internal static class StructureRulesLoader
{
    /// <summary>Largest allowed footprint side, in cells.</summary>
    public const int MaxFootprint = 6;

    private static readonly string[] BuildingKeys =
    [
        "footprint_w", "footprint_h", "max_health", "armor", "sight", "primary_weapon", "cost", "power",
        "prerequisites", "provides",
    ];

    /// <summary>Parses one building. <paramref name="weapons"/> must already be sorted by id.</summary>
    public static BuildingType? ParseBuilding(RulesFields f, IReadOnlyList<WeaponType> weapons, RulesReferences refs)
    {
        f.CheckKeys(BuildingKeys);
        int? width = f.Int("footprint_w", 1, MaxFootprint);
        int? height = f.Int("footprint_h", 1, MaxFootprint);
        int? maxHealth = f.Int("max_health", 1, int.MaxValue);
        ArmorClass? armor = f.Choice("armor", RulesEnums.ParseArmor, RulesEnums.ArmorKeys);
        int? sight = f.Int("sight", 0, SimConstants.MaxMapCells);
        int? weapon = f.WeaponIndex(weapons);
        int? cost = f.Int("cost", 0, int.MaxValue);
        int? power = f.Int("power", -10000, 10000);
        List<string>? prerequisites = f.StrList("prerequisites");
        List<string>? provides = f.StrList("provides");
        if (width is null || height is null || maxHealth is null || armor is null || sight is null || weapon is null
            || cost is null || power is null || prerequisites is null || provides is null)
        {
            return null;
        }
        if (provides.Any(string.IsNullOrEmpty))
        {
            f.Problem("provides", "provides names must not be empty");
            return null;
        }
        refs.AddAll(f, "prerequisites", prerequisites);
        return new BuildingType(f.Id, width.Value, height.Value, maxHealth.Value, armor.Value, sight.Value,
            weapon.Value, cost.Value, power.Value, prerequisites, provides);
    }
}
