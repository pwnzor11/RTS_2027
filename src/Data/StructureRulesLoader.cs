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
        "category", "produces", "build_time", "storage", "refinery", "dock", "exit", "free_unit", "wall", "needs_power", "buildable",
    ];

    private static readonly string[] CategoryNames = ["structure", "defence"];
    private static readonly string[] ProducesNames = ["", "infantry", "vehicle", "aircraft"];

    /// <summary>Parses one building. <paramref name="weapons"/> must already be sorted by id.</summary>
    public static BuildingType? ParseBuilding(RulesFields f, IReadOnlyList<WeaponType> weapons,
        IReadOnlyList<VehicleType> vehicles, RulesReferences refs)
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
        string? category = f.OptStr("category", "structure");
        if (category is not null && Array.IndexOf(CategoryNames, category) < 0)
        {
            f.Problem("category", $"must be one of {RulesEnums.Join(CategoryNames)}, got '{category}'");
            category = null;
        }
        string? produces = f.OptStr("produces", "");
        if (produces is not null && Array.IndexOf(ProducesNames, produces) < 0)
        {
            f.Problem("produces", $"must be one of {RulesEnums.Join(ProducesNames)}, got '{produces}'");
            produces = null;
        }
        int? buildTime = f.OptInt("build_time", 0, int.MaxValue, 0);
        int? storage = f.OptInt("storage", 0, int.MaxValue, 0);
        bool? refinery = f.OptBool("refinery", false);
        int[]? dock = f.OptIntArray("dock", 2, -1, MaxFootprint, new[] { 0, 0 });
        int[]? exit = f.OptIntArray("exit", 2, -1, MaxFootprint, null);
        bool? wall = f.OptBool("wall", false);
        bool? needsPower = f.OptBool("needs_power", false);
        bool? buildable = f.OptBool("buildable", true);
        string? freeName = f.OptStr("free_unit", "");
        int freeUnit = -1;
        if (freeName is { Length: > 0 })
        {
            freeUnit = SimRules.IndexOf(vehicles, freeName, v => v.Id);
            if (freeUnit < 0)
            {
                f.Problem("free_unit", $"unknown vehicle '{freeName}'");
                freeName = null;
            }
        }
        if (category is null || produces is null || buildTime is null || storage is null || refinery is null
            || dock is null || wall is null || needsPower is null || buildable is null || freeName is null
            || (f.Has("exit") && exit is null))
        {
            return null;
        }
        if (wall.Value && width is { } ww && height is { } wh && (ww != 1 || wh != 1))
        {
            f.Problem("wall", "a wall must have a 1x1 footprint");
            return null;
        }
        if (wall.Value && produces.Length > 0)
        {
            f.Problem("wall", "a wall must not produce units");
            return null;
        }
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
        var building = new BuildingType(f.Id, width.Value, height.Value, maxHealth.Value, armor.Value, sight.Value,
            weapon.Value, cost.Value, power.Value, prerequisites, provides)
        {
            Category = category == "defence" ? ProductionCategory.Defence : ProductionCategory.Structure,
            Produces = produces.Length == 0 ? null : produces == "infantry" ? ProductionCategory.Infantry
                : produces == "vehicle" ? ProductionCategory.Vehicle : ProductionCategory.Aircraft,
            BuildTime = buildTime.Value,
            Storage = storage.Value,
            Refinery = refinery.Value,
            Dock = new CellCoord(dock[0], dock[1]),
            FreeUnit = freeUnit,
            Wall = wall.Value,
            NeedsPower = needsPower.Value,
            Buildable = buildable.Value,
        };
        return exit is null ? building : building with { Exit = new CellCoord(exit[0], exit[1]) };
    }
}
