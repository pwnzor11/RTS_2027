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
        // schema v3 (M3-01)
        "faction", "entrance", "ticks_per_point", "researches", "gate", "seismic_radius", "sees_through_blackout",
        "jam_radius", "superweapon", "superweapon_charge_ticks", "superweapon_entrance_range",
        "morphs_into", "morph_ticks",
        // schema v4 (M4-01)
        "tier", "garrison", "garrison_range_bonus", "garrison_rof_permille",
        // schema v5 (M6-02)
        "packs_into", "pack_ticks",
        // schema v7 (SUP-1)
        "supply_provided", "supply_unstocked",
    ];

    /// <summary>Most soldiers a building may hold (schema v4).</summary>
    public const int MaxGarrison = 10;

    private static readonly string[] CategoryNames = ["structure", "defence"];
    private static readonly string[] ProducesNames = ["", "infantry", "vehicle", "aircraft"];

    /// <summary>Parses one building. <paramref name="weapons"/> must already be sorted by id.</summary>
    public static BuildingType? ParseBuilding(RulesFields f, IReadOnlyList<WeaponType> weapons,
        IReadOnlyList<VehicleType> vehicles, RulesReferences refs, IReadOnlyList<string>? buildingIds = null)
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
        Faction? faction = f.FactionOrDefault();
        TunnelSize? entrance = f.OptChoice("entrance", RulesEnums.ParseEntrance, RulesEnums.EntranceKeys, TunnelSize.None);
        int? ticksPerPoint = f.OptInt("ticks_per_point", 1, 10_000, 20);
        bool? researches = f.OptBool("researches", false);
        bool? gate = f.OptBool("gate", false);
        int? seismic = f.OptInt("seismic_radius", 0, SimConstants.MaxMapCells, 0);
        bool? seesThroughBlackout = f.OptBool("sees_through_blackout", false);
        int? jam = f.OptInt("jam_radius", 0, SimConstants.MaxMapCells, 0);
        string? superweapon = f.OptStr("superweapon", "");
        int? swCharge = f.OptInt("superweapon_charge_ticks", 0, int.MaxValue, 0);
        int? swRange = f.OptInt("superweapon_entrance_range", 0, SimConstants.MaxMapCells, 0);
        int? morphsInto = buildingIds is null
            ? -1 : f.OptIndex("morphs_into", buildingIds, "building");
        int? morphTicks = f.OptInt("morph_ticks", 0, int.MaxValue, 0);
        int? tier = f.OptInt("tier", 1, RulesLoader.MaxTier, 1);
        int? garrison = f.OptInt("garrison", 0, MaxGarrison, 0);
        int? garrisonRange = f.OptInt("garrison_range_bonus", 0, SimConstants.MaxMapCells * SimConstants.LeptonsPerCell, 256);
        int? garrisonRof = f.OptInt("garrison_rof_permille", 1, 100_000, 750);
        int? packTicks = f.OptInt("pack_ticks", 0, 1_000_000, 0);   // M6-02
        int? supplyProvided = f.OptInt("supply_provided", 0, 1000, 0);   // SUP-1
        int? supplyUnstocked = f.OptInt("supply_unstocked", -1, 1000, -1);
        if (supplyProvided is null || supplyUnstocked is null) return null;
        string? packsName = f.OptStr("packs_into", "");
        int packsInto = -1;
        if (packsName is { Length: > 0 })
        {
            packsInto = SimRules.IndexOf(vehicles, packsName, v => v.Id);
            if (packsInto < 0)
            {
                f.Problem("packs_into", $"unknown vehicle '{packsName}'");
                packsName = null;
            }
        }
        if (packTicks is null || packsName is null) return null;
        if (tier is null || garrison is null || garrisonRange is null || garrisonRof is null) return null;
        if (garrison.Value > 0 && entrance is { } ent && ent != TunnelSize.None)
        {
            f.Problem("garrison", "a garrisonable building must not be a tunnel entrance (entrance must be \"none\")");
            return null;
        }
        if (morphsInto is null || morphTicks is null || faction is null || entrance is null || ticksPerPoint is null || researches is null || gate is null || seismic is null
            || seesThroughBlackout is null || jam is null || superweapon is null || swCharge is null || swRange is null)
        {
            return null;
        }
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
        if (gate.Value && width is { } gw && height is { } gh && (gw != 1 || gh != 1))
        {
            f.Problem("gate", "a gate must have a 1x1 footprint");
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
            Wall = wall.Value || gate.Value,   // a gate is a wall piece (design 05 §2)
            NeedsPower = needsPower.Value,
            Buildable = buildable.Value,
            Faction = faction.Value,
            Entrance = entrance.Value,
            TicksPerPoint = ticksPerPoint.Value,
            Researches = researches.Value,
            Gate = gate.Value,
            SeismicRadius = seismic.Value,
            SeesThroughBlackout = seesThroughBlackout.Value,
            JamRadius = jam.Value,
            Superweapon = superweapon,
            SuperweaponChargeTicks = swCharge.Value,
            SuperweaponEntranceRange = swRange.Value,
            MorphsInto = morphsInto.Value,
            MorphTicks = morphTicks.Value,
            Tier = tier.Value,
            Garrison = garrison.Value,
            GarrisonRangeBonus = garrisonRange.Value,
            GarrisonRofPermille = garrisonRof.Value,
            PacksInto = packsInto,
            PackTicks = packTicks.Value,
            SupplyProvided = supplyProvided.Value,
            SupplyUnstocked = supplyUnstocked.Value,
        };
        return exit is null ? building : building with { Exit = new CellCoord(exit[0], exit[1]) };
    }
}
